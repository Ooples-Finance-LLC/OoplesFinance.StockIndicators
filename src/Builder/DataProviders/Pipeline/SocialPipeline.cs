using System.Diagnostics;
using OoplesFinance.StockIndicators.Builder.ML.Sentiment;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Pipeline;

/// <summary>
/// Pipeline for social sentiment data ingestion and processing.
/// Handles streaming, periodic aggregation, and trending ticker monitoring.
/// </summary>
public sealed class SocialPipeline : IDataPipeline, IDisposable
{
    private readonly ISocialSentimentProvider _provider;
    private readonly SocialPipelineOptions _options;
    private readonly DataPipelineOrchestrator _orchestrator;
    private readonly HashSet<string> _monitoredSymbols = new();
    private CancellationTokenSource? _streamingCts;
    private Task? _streamingTask;
    private DateTime _lastAggregationTime = DateTime.MinValue;
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
    /// Creates a new social sentiment pipeline.
    /// </summary>
    public SocialPipeline(
        ISocialSentimentProvider provider,
        SocialPipelineOptions options,
        DataPipelineOrchestrator orchestrator)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <inheritdoc />
    public string Name => "social";

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
            _streamingTask = StreamPostsAsync(_streamingCts.Token);
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
        // Check if it's time for aggregation
        if (DateTime.UtcNow - _lastAggregationTime < _options.AggregationInterval)
            return;

        _lastScheduledRun = DateTime.UtcNow;

        IReadOnlyList<string> symbolsToProcess;
        lock (_monitoredSymbols)
        {
            symbolsToProcess = _monitoredSymbols.ToList();
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get trending tickers first
            var trending = await _provider.GetTrendingTickersAsync(20, cancellationToken)
                .ConfigureAwait(false);

            if (trending.Count > 0)
            {
                var trendingEvent = new TrendingTickersChangedEvent
                {
                    TrendingTickers = trending,
                    DetectedAt = DateTime.UtcNow,
                    SourcePipeline = Name
                };

                await _orchestrator.PublishEventAsync(trendingEvent, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Aggregate metrics for each monitored symbol
            foreach (var symbol in symbolsToProcess)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var metrics = await _provider.GetAggregatedMetricsAsync(
                        symbol,
                        _options.MetricsWindow,
                        cancellationToken).ConfigureAwait(false);

                    var metricsEvent = new SocialMetricsUpdatedEvent
                    {
                        Symbol = symbol,
                        Metrics = metrics,
                        DetectedAt = DateTime.UtcNow,
                        SourcePipeline = Name
                    };

                    await _orchestrator.PublishEventAsync(metricsEvent, cancellationToken)
                        .ConfigureAwait(false);

                    // Check for significant sentiment changes
                    if (Math.Abs(metrics.AverageSentiment) > 0.5m)
                    {
                        var sentimentEvent = new SentimentChangedEvent
                        {
                            Symbol = symbol,
                            SentimentScore = metrics.AverageSentiment,
                            PreviousScore = 0, // Would need to track previous
                            BullishCount = metrics.BullishCount,
                            BearishCount = metrics.BearishCount,
                            DetectedAt = DateTime.UtcNow,
                            SourcePipeline = Name
                        };

                        await _orchestrator.PublishEventAsync(sentimentEvent, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    RecordProcessed(stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                }
                catch (Exception ex)
                {
                    RecordError();
                    _lastError = ex.Message;
                }
            }

            _lastAggregationTime = DateTime.UtcNow;
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
            NextScheduledRun = _lastAggregationTime == DateTime.MinValue
                ? DateTime.UtcNow
                : _lastAggregationTime + _options.AggregationInterval,
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

    private async Task StreamPostsAsync(CancellationToken cancellationToken)
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

                await foreach (var post in _provider.StreamPostsAsync(symbolsToStream, cancellationToken))
                {
                    var postEvent = new SocialPostReceivedEvent
                    {
                        Post = post,
                        DetectedAt = DateTime.UtcNow,
                        SourcePipeline = Name
                    };

                    await _orchestrator.PublishEventAsync(postEvent, cancellationToken)
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
/// Event published when a new social post is received.
/// </summary>
public sealed class SocialPostReceivedEvent : IDataEvent
{
    /// <summary>Gets or sets the post that was received.</summary>
    public SocialPost Post { get; set; } = new();

    /// <summary>Gets or sets when the post was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "SocialPostReceived";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "social";
}

/// <summary>
/// Event published when social metrics are updated for a symbol.
/// </summary>
public sealed class SocialMetricsUpdatedEvent : IDataEvent
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the updated metrics.</summary>
    public SocialMetrics Metrics { get; set; } = new();

    /// <summary>Gets or sets when the metrics were calculated.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "SocialMetricsUpdated";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "social";
}

/// <summary>
/// Event published when sentiment significantly changes for a symbol.
/// </summary>
public sealed class SentimentChangedEvent : IDataEvent
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the current sentiment score.</summary>
    public decimal SentimentScore { get; set; }

    /// <summary>Gets or sets the previous sentiment score.</summary>
    public decimal PreviousScore { get; set; }

    /// <summary>Gets or sets the bullish post count.</summary>
    public int BullishCount { get; set; }

    /// <summary>Gets or sets the bearish post count.</summary>
    public int BearishCount { get; set; }

    /// <summary>Gets or sets when the change was detected.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "SentimentChanged";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "social";
}

/// <summary>
/// Event published when trending tickers change.
/// </summary>
public sealed class TrendingTickersChangedEvent : IDataEvent
{
    /// <summary>Gets or sets the trending tickers.</summary>
    public IReadOnlyList<TrendingTicker> TrendingTickers { get; set; } = Array.Empty<TrendingTicker>();

    /// <summary>Gets or sets when the trending list was updated.</summary>
    public DateTime DetectedAt { get; set; }

    /// <inheritdoc />
    public DateTime Timestamp => DetectedAt;

    /// <inheritdoc />
    public string EventType => "TrendingTickersChanged";

    /// <inheritdoc />
    public string SourcePipeline { get; set; } = "social";
}
