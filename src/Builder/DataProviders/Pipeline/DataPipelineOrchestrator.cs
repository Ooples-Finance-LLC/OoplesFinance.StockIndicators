using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OoplesFinance.StockIndicators.Builder.DataProviders.Pipeline;

/// <summary>
/// Orchestrates data ingestion pipelines for news, social sentiment, and SEC filings.
/// Coordinates scheduling, event processing, and monitoring across all data providers.
/// </summary>
public sealed class DataPipelineOrchestrator : IDisposable
{
    private readonly DataPipelineOptions _options;
    private readonly INewsProvider? _newsProvider;
    private readonly ISocialSentimentProvider? _socialProvider;
    private readonly ISECFilingProvider? _secFilingProvider;
    private readonly Channel<IDataEvent> _eventChannel;
    private readonly ConcurrentDictionary<string, IDataPipeline> _pipelines = new();
    private readonly ConcurrentDictionary<Type, List<object>> _eventHandlers = new();
    private readonly Timer _schedulerTimer;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _eventProcessorTask;
    private bool _disposed;

    // Metrics
    private long _eventsProcessed;
    private long _eventsErrored;
    private DateTime _lastEventTime = DateTime.MinValue;

    /// <summary>
    /// Creates a new data pipeline orchestrator.
    /// </summary>
    /// <param name="options">Pipeline configuration options.</param>
    /// <param name="newsProvider">Optional news data provider.</param>
    /// <param name="socialProvider">Optional social sentiment provider.</param>
    /// <param name="secFilingProvider">Optional SEC filing provider.</param>
    public DataPipelineOrchestrator(
        DataPipelineOptions options,
        INewsProvider? newsProvider = null,
        ISocialSentimentProvider? socialProvider = null,
        ISECFilingProvider? secFilingProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _newsProvider = newsProvider;
        _socialProvider = socialProvider;
        _secFilingProvider = secFilingProvider;

        _eventChannel = Channel.CreateBounded<IDataEvent>(new BoundedChannelOptions(_options.EventChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        // Initialize pipelines
        InitializePipelines();

        // Start event processor
        _eventProcessorTask = ProcessEventsAsync(_cts.Token);

        // Start scheduler
        _schedulerTimer = new Timer(
            OnSchedulerTick,
            null,
            TimeSpan.FromSeconds(5),
            _options.SchedulerInterval);
    }

    /// <summary>Gets the number of events processed.</summary>
    public long EventsProcessed => Interlocked.Read(ref _eventsProcessed);

    /// <summary>Gets the number of events that errored.</summary>
    public long EventsErrored => Interlocked.Read(ref _eventsErrored);

    /// <summary>Gets the time of the last event.</summary>
    public DateTime LastEventTime => _lastEventTime;

    /// <summary>
    /// Registers an event handler for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The handler to register.</param>
    public void RegisterHandler<TEvent>(IDataEventHandler<TEvent> handler) where TEvent : IDataEvent
    {
        var handlers = _eventHandlers.GetOrAdd(typeof(TEvent), _ => new List<object>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
    }

    /// <summary>
    /// Publishes an event to be processed by registered handlers.
    /// </summary>
    /// <param name="dataEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PublishEventAsync(IDataEvent dataEvent, CancellationToken cancellationToken = default)
    {
        await _eventChannel.Writer.WriteAsync(dataEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a specific pipeline by name.
    /// </summary>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartPipelineAsync(string pipelineName, CancellationToken cancellationToken = default)
    {
        if (_pipelines.TryGetValue(pipelineName, out var pipeline))
        {
            await pipeline.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops a specific pipeline by name.
    /// </summary>
    /// <param name="pipelineName">The pipeline name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopPipelineAsync(string pipelineName, CancellationToken cancellationToken = default)
    {
        if (_pipelines.TryGetValue(pipelineName, out var pipeline))
        {
            await pipeline.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts all pipelines.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _pipelines.Values.Select(p => p.StartAsync(cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops all pipelines.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _pipelines.Values.Select(p => p.StopAsync(cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets pipeline status for all pipelines.
    /// </summary>
    public IReadOnlyDictionary<string, PipelineStatus> GetPipelineStatuses()
    {
        return _pipelines.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetStatus());
    }

    /// <summary>
    /// Gets orchestrator metrics.
    /// </summary>
    public OrchestratorMetrics GetMetrics()
    {
        var pipelineMetrics = _pipelines.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetMetrics());

        return new OrchestratorMetrics
        {
            EventsProcessed = EventsProcessed,
            EventsErrored = EventsErrored,
            LastEventTime = LastEventTime,
            EventChannelCount = _eventChannel.Reader.Count,
            RegisteredHandlerCount = _eventHandlers.Values.Sum(h => h.Count),
            PipelineMetrics = pipelineMetrics
        };
    }

    private void InitializePipelines()
    {
        if (_newsProvider is not null)
        {
            var newsPipeline = new NewsPipeline(_newsProvider, _options.NewsOptions, this);
            _pipelines["news"] = newsPipeline;
        }

        if (_socialProvider is not null)
        {
            var socialPipeline = new SocialPipeline(_socialProvider, _options.SocialOptions, this);
            _pipelines["social"] = socialPipeline;
        }

        if (_secFilingProvider is not null)
        {
            var secPipeline = new SECFilingPipeline(_secFilingProvider, _options.SECFilingOptions, this);
            _pipelines["sec"] = secPipeline;
        }
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var dataEvent in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                _lastEventTime = DateTime.UtcNow;
                await ProcessEventAsync(dataEvent, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _eventsProcessed);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _eventsErrored);
                // Log error but continue processing
            }
        }
    }

    private async Task ProcessEventAsync(IDataEvent dataEvent, CancellationToken cancellationToken)
    {
        var eventType = dataEvent.GetType();

        if (!_eventHandlers.TryGetValue(eventType, out var handlers))
            return;

        List<object> handlersCopy;
        lock (handlers)
        {
            handlersCopy = handlers.ToList();
        }

        // Invoke handlers in parallel
        var tasks = handlersCopy.Select(handler =>
            InvokeHandlerAsync(handler, dataEvent, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task InvokeHandlerAsync(object handler, IDataEvent dataEvent, CancellationToken cancellationToken)
    {
        // Use reflection to invoke the correct HandleAsync method
        var handlerType = handler.GetType();
        var interfaceType = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                  i.GetGenericTypeDefinition() == typeof(IDataEventHandler<>) &&
                                  i.GetGenericArguments()[0] == dataEvent.GetType());

        if (interfaceType is not null)
        {
            var method = interfaceType.GetMethod("HandleAsync");
            if (method is not null)
            {
                var task = method.Invoke(handler, new object[] { dataEvent, cancellationToken }) as Task;
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }
            }
        }
    }

    private void OnSchedulerTick(object? state)
    {
        // Check each pipeline for scheduled work
        foreach (var (_, pipeline) in _pipelines)
        {
            try
            {
                _ = pipeline.ExecuteScheduledWorkAsync(_cts.Token);
            }
            catch
            {
                // Log but continue
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _schedulerTimer.Dispose();
            _eventChannel.Writer.Complete();

            try
            {
                _eventProcessorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore cancellation exceptions
            }

            foreach (var (_, pipeline) in _pipelines)
            {
                (pipeline as IDisposable)?.Dispose();
            }

            _cts.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for data events.
/// </summary>
public interface IDataEvent
{
    /// <summary>Gets the event timestamp.</summary>
    DateTime Timestamp { get; }

    /// <summary>Gets the event type name.</summary>
    string EventType { get; }

    /// <summary>Gets the source pipeline.</summary>
    string SourcePipeline { get; }
}

/// <summary>
/// Interface for data event handlers.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IDataEventHandler<TEvent> where TEvent : IDataEvent
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for data pipelines.
/// </summary>
public interface IDataPipeline
{
    /// <summary>Gets the pipeline name.</summary>
    string Name { get; }

    /// <summary>Gets whether the pipeline is running.</summary>
    bool IsRunning { get; }

    /// <summary>Starts the pipeline.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the pipeline.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes any scheduled work.</summary>
    Task ExecuteScheduledWorkAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the pipeline status.</summary>
    PipelineStatus GetStatus();

    /// <summary>Gets pipeline metrics.</summary>
    PipelineMetrics GetMetrics();
}

/// <summary>
/// Pipeline status information.
/// </summary>
public sealed class PipelineStatus
{
    /// <summary>Gets or sets the pipeline name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the pipeline is running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the last run time.</summary>
    public DateTime? LastRunTime { get; set; }

    /// <summary>Gets or sets the next scheduled run time.</summary>
    public DateTime? NextScheduledRun { get; set; }

    /// <summary>Gets or sets the last error message if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Gets or sets whether the pipeline is healthy.</summary>
    public bool IsHealthy { get; set; } = true;
}

/// <summary>
/// Pipeline metrics.
/// </summary>
public sealed class PipelineMetrics
{
    /// <summary>Gets or sets the number of items processed.</summary>
    public long ItemsProcessed { get; set; }

    /// <summary>Gets or sets the number of items errored.</summary>
    public long ItemsErrored { get; set; }

    /// <summary>Gets or sets the average processing time in milliseconds.</summary>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>Gets or sets the items processed per second.</summary>
    public double ItemsPerSecond { get; set; }

    /// <summary>Gets or sets when the pipeline started.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Gets or sets the uptime duration.</summary>
    public TimeSpan? Uptime { get; set; }
}

/// <summary>
/// Orchestrator metrics.
/// </summary>
public sealed class OrchestratorMetrics
{
    /// <summary>Gets or sets the total events processed.</summary>
    public long EventsProcessed { get; set; }

    /// <summary>Gets or sets the total events errored.</summary>
    public long EventsErrored { get; set; }

    /// <summary>Gets or sets the last event time.</summary>
    public DateTime LastEventTime { get; set; }

    /// <summary>Gets or sets the current event channel count.</summary>
    public int EventChannelCount { get; set; }

    /// <summary>Gets or sets the number of registered handlers.</summary>
    public int RegisteredHandlerCount { get; set; }

    /// <summary>Gets or sets metrics for each pipeline.</summary>
    public IReadOnlyDictionary<string, PipelineMetrics> PipelineMetrics { get; set; } =
        new Dictionary<string, PipelineMetrics>();
}

/// <summary>
/// Options for data pipeline orchestrator.
/// </summary>
public sealed class DataPipelineOptions
{
    /// <summary>Gets or sets the scheduler interval.</summary>
    public TimeSpan SchedulerInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the event channel capacity.</summary>
    public int EventChannelCapacity { get; set; } = 10000;

    /// <summary>Gets or sets news pipeline options.</summary>
    public NewsPipelineOptions NewsOptions { get; set; } = new();

    /// <summary>Gets or sets social pipeline options.</summary>
    public SocialPipelineOptions SocialOptions { get; set; } = new();

    /// <summary>Gets or sets SEC filing pipeline options.</summary>
    public SECFilingPipelineOptions SECFilingOptions { get; set; } = new();

    /// <summary>Gets or sets the symbols to monitor.</summary>
    public IReadOnlyList<string> MonitoredSymbols { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Options for news pipeline.
/// </summary>
public sealed class NewsPipelineOptions
{
    /// <summary>Gets or sets the catch-up interval for news.</summary>
    public TimeSpan CatchUpInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets whether to enable streaming.</summary>
    public bool EnableStreaming { get; set; } = true;

    /// <summary>Gets or sets the maximum articles per fetch.</summary>
    public int MaxArticlesPerFetch { get; set; } = 50;
}

/// <summary>
/// Options for social pipeline.
/// </summary>
public sealed class SocialPipelineOptions
{
    /// <summary>Gets or sets the aggregation interval.</summary>
    public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets whether to enable streaming.</summary>
    public bool EnableStreaming { get; set; } = true;

    /// <summary>Gets or sets the metrics window for aggregation.</summary>
    public TimeSpan MetricsWindow { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Options for SEC filing pipeline.
/// </summary>
public sealed class SECFilingPipelineOptions
{
    /// <summary>Gets or sets the check interval for new filings.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets the form types to monitor.</summary>
    public IReadOnlyList<string> FormTypes { get; set; } = new[] { "10-K", "10-Q", "8-K", "4" };

    /// <summary>Gets or sets whether to enable streaming.</summary>
    public bool EnableStreaming { get; set; } = true;
}
