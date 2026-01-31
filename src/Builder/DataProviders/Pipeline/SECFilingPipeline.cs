using System.Diagnostics;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Pipeline;

/// <summary>
/// Pipeline for SEC filing data ingestion and processing.
/// Handles streaming new filings and periodic checks for monitored symbols.
/// </summary>
public sealed class SECFilingPipeline : IDataPipeline, IDisposable
{
    private readonly ISECFilingProvider _provider;
    private readonly SECFilingPipelineOptions _options;
    private readonly DataPipelineOrchestrator _orchestrator;
    private readonly HashSet<string> _monitoredSymbols = new();
    private readonly HashSet<string> _processedAccessionNumbers = new();
    private CancellationTokenSource? _streamingCts;
    private Task? _streamingTask;
    private DateTime _lastCheckTime = DateTime.MinValue;
    private DateTime _lastScheduledRun = DateTime.MinValue;
    private DateTime? _startTime;
    private string? _lastError;
    private bool _disposed;

    // Metrics
    private long _itemsProcessed;
    private long _itemsErrored;
    private long _totalProcessingTimeMs;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Creates a new SEC filing pipeline.
    /// </summary>
    public SECFilingPipeline(
        ISECFilingProvider provider,
        SECFilingPipelineOptions options,
        DataPipelineOrchestrator orchestrator)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <inheritdoc />
    public string Name => "sec";

    /// <inheritdoc />
    public bool IsRunning => _streamingTask is not null && !_streamingTask.IsCompleted;

    /// <summary>
    /// Adds symbols to monitor.
    /// </summary>
    public void AddSymbols(IEnumerable<string> symbols)
    {
        lock (_monitoredSymbols)
        {
            foreach (var symbol in symbols)
            {
                _monitoredSymbols.Add(symbol.ToUpperInvariant());
            }
        }
    }

    /// <summary>
    /// Removes symbols from monitoring.
    /// </summary>
    public void RemoveSymbols(IEnumerable<string> symbols)
    {
        lock (_monitoredSymbols)
        {
            foreach (var symbol in symbols)
            {
                _monitoredSymbols.Remove(symbol.ToUpperInvariant());
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        _startTime = DateTime.UtcNow;
        _lastError = null;

        if (_options.EnableStreaming && _monitoredSymbols.Count > 0)
        {
            _streamingCts = new CancellationTokenSource();
            _streamingTask = StreamFilingsAsync(_streamingCts.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_streamingCts is not null)
        {
            _streamingCts.Cancel();
            _streamingCts.Dispose();
            _streamingCts = null;
        }

        if (_streamingTask is not null)
        {
            try
            {
                await _streamingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            _streamingTask = null;
        }

        _startTime = null;
    }

    /// <inheritdoc />
    public async Task ExecuteScheduledWorkAsync(CancellationToken cancellationToken = default)
    {
        // Check if it's time to check for new filings
        if (DateTime.UtcNow - _lastCheckTime < _options.CheckInterval)
            return;

        _lastScheduledRun = DateTime.UtcNow;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get recent filings across all companies
            var recentFilings = await _provider.GetRecentFilingsAsync(
                _options.FormTypes,
                100,
                cancellationToken).ConfigureAwait(false);

            foreach (var filing in recentFilings)
            {
                // Skip already processed filings
                if (_processedAccessionNumbers.Contains(filing.AccessionNumber))
                    continue;

                // Check if we're monitoring this symbol
                bool isMonitored;
                lock (_monitoredSymbols)
                {
                    isMonitored = _monitoredSymbols.Count == 0 || // Monitor all if none specified
                                  _monitoredSymbols.Contains(filing.Symbol.ToUpperInvariant());
                }

                if (!isMonitored)
                    continue;

                // Publish filing detected event
                var filingEvent = new FilingDetectedEvent
                {
                    Filing = filing,
                    DetectedAt = DateTime.UtcNow,
                    SourcePipeline = Name
                };

                await _orchestrator.PublishEventAsync(filingEvent, cancellationToken)
                    .ConfigureAwait(false);

                // Mark as processed
                _processedAccessionNumbers.Add(filing.AccessionNumber);

                // Limit size of processed set
                if (_processedAccessionNumbers.Count > 10000)
                {
                    // Remove oldest entries (this is a simple approximation)
                    var toRemove = _processedAccessionNumbers.Take(5000).ToList();
                    foreach (var accNum in toRemove)
                    {
                        _processedAccessionNumbers.Remove(accNum);
                    }
                }

                RecordProcessed(stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                // Check for material events (8-K)
                if (filing.FormType == "8-K")
                {
                    var materialEvent = new MaterialEventFiledEvent
                    {
                        Filing = filing,
                        DetectedAt = DateTime.UtcNow,
                        SourcePipeline = Name
                    };

                    await _orchestrator.PublishEventAsync(materialEvent, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Check for insider trading (Form 4)
                if (filing.FormType == "4")
                {
                    try
                    {
                        var insiderTxns = await _provider.GetInsiderTransactionsAsync(
                            filing.Symbol,
                            filing.FiledAt.AddDays(-1),
                            filing.FiledAt,
                            cancellationToken).ConfigureAwait(false);

                        if (insiderTxns.Count > 0)
                        {
                            var insiderEvent = new InsiderTradingDetectedEvent
                            {
                                Symbol = filing.Symbol,
                                Transactions = insiderTxns,
                                DetectedAt = DateTime.UtcNow,
                                SourcePipeline = Name
                            };

                            await _orchestrator.PublishEventAsync(insiderEvent, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // Log but continue
                    }
                }
            }

            _lastCheckTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            RecordError();
        }
    }

    /// <inheritdoc />
    public PipelineStatus GetStatus()
    {
        return new PipelineStatus
        {
            Name = Name,
            IsRunning = IsRunning,
            LastRunTime = _lastScheduledRun == DateTime.MinValue ? null : _lastScheduledRun,
            NextScheduledRun = _lastCheckTime == DateTime.MinValue
                ? DateTime.UtcNow
                : _lastCheckTime + _options.CheckInterval,
            LastError = _lastError,
            IsHealthy = _lastError is null && _provider.IsConnected
        };
    }

    /// <inheritdoc />
    public PipelineMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            var processed = Interlocked.Read(ref _itemsProcessed);
            var errored = Interlocked.Read(ref _itemsErrored);
            var totalTime = Interlocked.Read(ref _totalProcessingTimeMs);

            var uptime = _startTime.HasValue
                ? DateTime.UtcNow - _startTime.Value
                : (TimeSpan?)null;

            return new PipelineMetrics
            {
                ItemsProcessed = processed,
                ItemsErrored = errored,
                AverageProcessingTimeMs = processed > 0 ? (double)totalTime / processed : 0,
                ItemsPerSecond = uptime?.TotalSeconds > 0 ? processed / uptime.Value.TotalSeconds : 0,
                StartTime = _startTime,
                Uptime = uptime
            };
        }
    }

    private async Task StreamFilingsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<string> symbolsToStream;
            lock (_monitoredSymbols)
            {
                symbolsToStream = _monitoredSymbols.ToList();
            }

            if (symbolsToStream.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();

                await foreach (var filing in _provider.StreamNewFilingsAsync(
                    symbolsToStream,
                    _options.FormTypes,
                    cancellationToken))
                {
                    // Skip already processed filings
                    if (_processedAccessionNumbers.Contains(filing.AccessionNumber))
                        continue;

                    var filingEvent = new FilingDetectedEvent
                    {
                        Filing = filing,
                        DetectedAt = DateTime.UtcNow,
                        SourcePipeline = Name
                    };

                    await _orchestrator.PublishEventAsync(filingEvent, cancellationToken)
                        .ConfigureAwait(false);

                    _processedAccessionNumbers.Add(filing.AccessionNumber);

                    RecordProcessed(stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                RecordError();

                // Wait before retrying
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void RecordProcessed(long processingTimeMs)
    {
        Interlocked.Increment(ref _itemsProcessed);
        Interlocked.Add(ref _totalProcessingTimeMs, processingTimeMs);
    }

    private void RecordError()
    {
        Interlocked.Increment(ref _itemsErrored);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _streamingCts?.Cancel();
            _streamingCts?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Event published when a new SEC filing is detected.
/// </summary>
public sealed class FilingDetectedEvent : IDataEvent
{
    /// <summary>Gets or sets the filing that was detected.</summary>
    public SECFilingData Filing { get; set; } = new();

    /// <summary>Gets or sets when the filing was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "FilingDetected";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "sec";
}

/// <summary>
/// Event published when a material event (8-K) is filed.
/// </summary>
public sealed class MaterialEventFiledEvent : IDataEvent
{
    /// <summary>Gets or sets the 8-K filing.</summary>
    public SECFilingData Filing { get; set; } = new();

    /// <summary>Gets or sets when the event was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "MaterialEventFiled";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "sec";
}

/// <summary>
/// Event published when insider trading (Form 4) is detected.
/// </summary>
public sealed class InsiderTradingDetectedEvent : IDataEvent
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the insider transactions.</summary>
    public IReadOnlyList<InsiderTransaction> Transactions { get; set; } = Array.Empty<InsiderTransaction>();

    /// <summary>Gets or sets when the trading was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "InsiderTradingDetected";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "sec";

    /// <summary>Gets the total value of purchases.</summary>
    public decimal TotalPurchaseValue => Transactions
        .Where(t => t.TransactionType == InsiderTransactionType.Purchase)
        .Sum(t => t.Shares * (t.PricePerShare ?? 0));

    /// <summary>Gets the total value of sales.</summary>
    public decimal TotalSaleValue => Transactions
        .Where(t => t.TransactionType == InsiderTransactionType.Sale)
        .Sum(t => t.Shares * (t.PricePerShare ?? 0));

    /// <summary>Gets whether there was net buying.</summary>
    public bool IsNetBuying => TotalPurchaseValue > TotalSaleValue;
}
