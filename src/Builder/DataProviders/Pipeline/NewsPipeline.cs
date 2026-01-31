using System.Diagnostics;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Pipeline;

/// <summary>
/// Pipeline for news data ingestion and processing.
/// Handles streaming and periodic catch-up from news providers.
/// </summary>
public sealed class NewsPipeline : IDataPipeline, IDisposable
{
    private readonly INewsProvider _provider;
    private readonly NewsPipelineOptions _options;
    private readonly DataPipelineOrchestrator _orchestrator;
    private readonly HashSet<string> _monitoredSymbols = new();
    private CancellationTokenSource? _streamingCts;
    private Task? _streamingTask;
    private DateTime _lastCatchUpTime = DateTime.MinValue;
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
    /// Creates a new news pipeline.
    /// </summary>
    public NewsPipeline(
        INewsProvider provider,
        NewsPipelineOptions options,
        DataPipelineOrchestrator orchestrator)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <inheritdoc />
    public string Name => "news";

    /// <inheritdoc />
    public bool IsRunning => _streamingTask is not null && !_streamingTask.IsCompleted;

    /// <summary>
    /// Adds symbols to monitor.
    /// </summary>
    /// <param name="symbols">Symbols to add.</param>
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
    /// <param name="symbols">Symbols to remove.</param>
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
            _streamingTask = StreamNewsAsync(_streamingCts.Token);
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
        // Check if it's time for a catch-up
        if (DateTime.UtcNow - _lastCatchUpTime < _options.CatchUpInterval)
            return;

        _lastScheduledRun = DateTime.UtcNow;

        IReadOnlyList<string> symbolsToProcess;
        lock (_monitoredSymbols)
        {
            symbolsToProcess = _monitoredSymbols.ToList();
        }

        if (symbolsToProcess.Count == 0)
            return;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Fetch latest news for each symbol
            foreach (var symbol in symbolsToProcess)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var articles = await _provider.GetLatestNewsAsync(
                        symbol,
                        _options.MaxArticlesPerFetch,
                        cancellationToken).ConfigureAwait(false);

                    foreach (var article in articles)
                    {
                        var newsEvent = new NewsPublishedEvent
                        {
                            Article = article,
                            DetectedAt = DateTime.UtcNow,
                            SourcePipeline = Name
                        };

                        await _orchestrator.PublishEventAsync(newsEvent, cancellationToken)
                            .ConfigureAwait(false);

                        RecordProcessed(stopwatch.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    RecordError();
                    _lastError = ex.Message;
                }
            }

            _lastCatchUpTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
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
            NextScheduledRun = _lastCatchUpTime == DateTime.MinValue
                ? DateTime.UtcNow
                : _lastCatchUpTime + _options.CatchUpInterval,
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

    private async Task StreamNewsAsync(CancellationToken cancellationToken)
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

                await foreach (var article in _provider.StreamNewsAsync(symbolsToStream, cancellationToken))
                {
                    var newsEvent = new NewsPublishedEvent
                    {
                        Article = article,
                        DetectedAt = DateTime.UtcNow,
                        SourcePipeline = Name
                    };

                    await _orchestrator.PublishEventAsync(newsEvent, cancellationToken)
                        .ConfigureAwait(false);

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
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
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
/// Event published when a new news article is detected.
/// </summary>
public sealed class NewsPublishedEvent : IDataEvent
{
    /// <summary>Gets or sets the article that was published.</summary>
    public NewsArticleData Article { get; set; } = new();

    /// <summary>Gets or sets when the article was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "NewsPublished";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "news";
}
