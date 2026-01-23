using System.Buffers;
using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Builder.Signals;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Runtime for executing indicators and processing signals.
/// Implements IDisposable to manage buffer lifecycles and return all buffers to pool.
/// </summary>
public sealed class IndicatorRuntime : IDisposable
{
    private readonly IndicatorDataSource _source;
    private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
    private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
    private readonly IReadOnlyList<SignalRule> _signals;
    private readonly IReadOnlyList<SignalGroupRule> _groupSignals;
    private readonly IReadOnlyList<SignalRangeRule> _rangeSignals;
    private readonly IReadOnlyList<INotificationChannel> _notifications;
    private readonly AutoTradingConfiguration _autoTrading;
    private readonly BehaviorOptions _behavior;
    private readonly HashSet<SeriesHandle> _activeSeries;
    private readonly IReadOnlyList<SymbolId> _symbols;
    private readonly StreamingOptions? _streamingOptions;
    private readonly SignalOptions? _signalOptions;
    private readonly BacktestOptions? _backtestOptions;
    private readonly BenchmarkOptions? _benchmarkOptions;
    private readonly bool[] _signalStates;
    private readonly double?[] _signalPrevious;
    private readonly SignalGroupState[] _groupStates;
    private readonly bool[] _rangeStates;
    private readonly object _statsLock = new();
    private bool _hasEmitted;
    private bool _started;
    private volatile bool _disposed;

    // Buffer management
    private readonly Dictionary<SeriesHandle, IndicatorBuffer<double>> _buffers = new();
    private readonly object _buffersLock = new();

    // Streaming session (stored as field to prevent premature disposal)
    private StreamingSession? _streamingSession;

    // Object pooling for streaming hot-path allocations
    private Dictionary<SeriesHandle, double[]>? _streamingValues;
    private Dictionary<SeriesHandle, double[]>? _snapshotPool;
    private readonly object _poolLock = new();

    // Statistics
    private long _buffersAllocated;
    private long _buffersReused;
    private long _totalBytesAllocated;

    internal IndicatorRuntime(
        IndicatorDataSource source,
        Dictionary<SeriesHandle, SeriesNode> nodes,
        Dictionary<IndicatorKey, SeriesHandle> keys,
        IReadOnlyList<SignalRule> signals,
        IReadOnlyList<SignalGroupRule> groupSignals,
        IReadOnlyList<SignalRangeRule> rangeSignals,
        IReadOnlyList<INotificationChannel> notifications,
        AutoTradingConfiguration autoTrading,
        BehaviorOptions behavior,
        IReadOnlyCollection<SeriesHandle> activeSeries,
        IReadOnlyList<SymbolId> symbols,
        StreamingOptions? streamingOptions,
        SignalOptions? signalOptions,
        BacktestOptions? backtestOptions,
        BenchmarkOptions? benchmarkOptions)
    {
        _source = source;
        _nodes = nodes;
        _keys = keys;
        _signals = signals;
        _groupSignals = groupSignals;
        _rangeSignals = rangeSignals;
        _notifications = notifications;
        _autoTrading = autoTrading;
        _behavior = behavior;
        _activeSeries = new HashSet<SeriesHandle>(activeSeries);
        _symbols = symbols;
        _streamingOptions = streamingOptions;
        _signalOptions = signalOptions;
        _backtestOptions = backtestOptions;
        _benchmarkOptions = benchmarkOptions;
        // Use Array.Empty for zero-length arrays to avoid allocations in the common case
        _signalStates = signals.Count == 0 ? Array.Empty<bool>() : new bool[signals.Count];
        _signalPrevious = signals.Count == 0 ? Array.Empty<double?>() : new double?[signals.Count];
        _groupStates = groupSignals.Count == 0 ? Array.Empty<SignalGroupState>() : new SignalGroupState[groupSignals.Count];
        _rangeStates = rangeSignals.Count == 0 ? Array.Empty<bool>() : new bool[rangeSignals.Count];
        for (var i = 0; i < groupSignals.Count; i++)
        {
            _groupStates[i] = new SignalGroupState(groupSignals[i].Conditions.Length);
        }
    }

    /// <summary>
    /// Gets the latest indicator snapshot.
    /// </summary>
    public IndicatorSnapshot? Latest { get; private set; }

    /// <summary>
    /// Gets the number of buffers allocated during this session.
    /// </summary>
    public long BuffersAllocated
    {
        get
        {
            lock (_statsLock)
            {
                return _buffersAllocated;
            }
        }
    }

    /// <summary>
    /// Gets the number of buffer reuse operations during this session.
    /// </summary>
    public long BuffersReused
    {
        get
        {
            lock (_statsLock)
            {
                return _buffersReused;
            }
        }
    }

    /// <summary>
    /// Gets the total bytes allocated for buffers during this session.
    /// </summary>
    public long TotalBytesAllocated
    {
        get
        {
            lock (_statsLock)
            {
                return _totalBytesAllocated;
            }
        }
    }

    /// <summary>
    /// Gets the buffer reuse rate as a percentage (0-100).
    /// </summary>
    public double BufferReuseRate
    {
        get
        {
            lock (_statsLock)
            {
                var total = _buffersAllocated + _buffersReused;
                return total == 0 ? 0 : (double)_buffersReused / total * 100;
            }
        }
    }

    /// <summary>
    /// Gets whether this runtime has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Event raised when indicators are updated.
    /// </summary>
    public event Action<IndicatorSnapshot>? Updated;

    /// <summary>
    /// Subscribes to series handles for computation.
    /// </summary>
    public void Subscribe(params SeriesHandle[] handles)
    {
        ThrowIfDisposed();
        if (handles == null || handles.Length == 0)
        {
            return;
        }

        for (var i = 0; i < handles.Length; i++)
        {
            ActivateSeries(handles[i]);
        }
    }

    /// <summary>
    /// Gets the buffer for a computed indicator series.
    /// The buffer is owned by the runtime and will be returned to the pool when the runtime is disposed.
    /// Copy data with ToList() or ToArray() if you need it after disposal.
    /// </summary>
    /// <param name="handle">The series handle to retrieve.</param>
    /// <returns>The indicator buffer for the series.</returns>
    /// <exception cref="ObjectDisposedException">The runtime has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The series has not been computed.</exception>
    public IndicatorBuffer<double> GetSeries(SeriesHandle handle)
    {
        ThrowIfDisposed();
        lock (_buffersLock)
        {
            if (_buffers.TryGetValue(handle, out var buffer))
            {
                return buffer;
            }
        }

        // Try to resolve from latest snapshot if available
        if (Latest is not null && Latest.TryGetSeries(handle, out var values))
        {
            return GetOrCreateBuffer(handle, values.Span);
        }

        throw new InvalidOperationException($"Series {handle} has not been computed. Call Start() first.");
    }

    /// <summary>
    /// Tries to get the buffer for a computed indicator series.
    /// </summary>
    /// <param name="handle">The series handle to retrieve.</param>
    /// <param name="buffer">The indicator buffer if found.</param>
    /// <returns>True if the buffer was found; otherwise false.</returns>
    public bool TryGetSeries(SeriesHandle handle, out IndicatorBuffer<double>? buffer)
    {
        if (_disposed)
        {
            buffer = null;
            return false;
        }

        lock (_buffersLock)
        {
            if (_buffers.TryGetValue(handle, out var existing))
            {
                buffer = existing;
                return true;
            }
        }

        if (Latest is not null && Latest.TryGetSeries(handle, out var values))
        {
            buffer = GetOrCreateBuffer(handle, values.Span);
            return true;
        }

        buffer = null;
        return false;
    }

    /// <summary>
    /// Gets or creates a buffer for the specified series handle.
    /// </summary>
    private IndicatorBuffer<double> GetOrCreateBuffer(SeriesHandle handle, ReadOnlySpan<double> values)
    {
        lock (_buffersLock)
        {
            if (_buffers.TryGetValue(handle, out var existing))
            {
                lock (_statsLock)
                {
                    _buffersReused++;
                }
                return existing;
            }

            var buffer = new IndicatorBuffer<double>(values);
            _buffers[handle] = buffer;
            lock (_statsLock)
            {
                _buffersAllocated++;
                _totalBytesAllocated += values.Length * sizeof(double);
            }
            return buffer;
        }
    }

    /// <summary>
    /// Starts the runtime.
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        // Consume options to suppress unused warnings
        _ = _signalOptions;
        _ = _backtestOptions;
        _ = _benchmarkOptions;

        if (_source.Kind == IndicatorSourceKind.Batch)
        {
            StartBatch();
            return;
        }

        StartStreaming();
    }

    private void StartBatch()
    {
        var data = _source.BatchData ?? throw new InvalidOperationException("Batch source missing data.");
        var evaluator = new SeriesEvaluator(data, _nodes);
        var series = evaluator.Evaluate(_activeSeries);
        Publish(new IndicatorSnapshot(series, _keys, handle =>
        {
            if (series.TryGetValue(handle, out var existing))
            {
                return existing;
            }

            var computed = evaluator.Evaluate(handle);
            series[handle] = computed;
            ActivateSeries(handle);
            return computed;
        }));
    }

    private void StartStreaming()
    {
        var stream = _source.StreamSource ?? throw new InvalidOperationException("Streaming source missing stream.");
        var options = _streamingOptions ?? throw new InvalidOperationException("Streaming options missing.");
        var symbols = options.Symbols ?? ToSymbolStrings(_symbols);
        if (symbols.Count == 0)
        {
            throw new InvalidOperationException("Streaming options must include at least one symbol.");
        }

        // Initialize pooled objects for hot-path reuse
        _streamingValues = new Dictionary<SeriesHandle, double[]>(_nodes.Count);
        _snapshotPool = new Dictionary<SeriesHandle, double[]>(_nodes.Count);

        // Store session as field to prevent premature disposal (Start() is non-blocking)
        _streamingSession = StreamingSession.Create(stream, symbols, options: options);
        var subscriptionOptions = options.CreateSubscriptionOptions();

        foreach (var pair in _nodes)
        {
            if (pair.Value.Kind != SeriesNodeKind.Indicator)
            {
                continue;
            }

            var handle = pair.Key;

            // Respect lazy compute policy - skip inactive series
            if (!_activeSeries.Contains(handle))
            {
                continue;
            }

            var spec = pair.Value.Spec;
            if (spec == null)
            {
                continue;
            }

            var key = pair.Value.SeriesKey;
            var state = StreamingIndicatorFactory.CreateState(spec);
            if (state == null)
            {
                continue;
            }

            _streamingSession.RegisterStatefulIndicator(key.Symbol.Value, key.Timeframe, state, update =>
            {
                var value = StreamingIndicatorFactory.ExtractValue(update, spec);

                // Reuse buffer instead of allocating new array each time
                lock (_poolLock)
                {
                    if (!_streamingValues!.TryGetValue(handle, out var buffer))
                    {
                        buffer = new double[1];
                        _streamingValues[handle] = buffer;
                    }
                    buffer[0] = value;

                    UpdateFormulaNodes(_streamingValues);

                    // Reuse snapshot dictionary by clearing and refilling
                    _snapshotPool!.Clear();
                    foreach (var kvp in _streamingValues)
                    {
                        _snapshotPool[kvp.Key] = kvp.Value;
                    }

                    PublishStreaming(new IndicatorSnapshot(_snapshotPool, _keys, requested => ResolveStreamingSeries(_snapshotPool, requested)));
                }
            }, subscriptionOptions);
        }

        _streamingSession.Start();
    }

    private void UpdateFormulaNodes(Dictionary<SeriesHandle, double[]> values)
    {
        foreach (var pair in _nodes)
        {
            if (!_activeSeries.Contains(pair.Key))
            {
                continue;
            }

            if (pair.Value.Kind != SeriesNodeKind.Formula)
            {
                continue;
            }

            var node = pair.Value;
            var left = node.Left.HasValue && values.TryGetValue(node.Left.Value, out var leftValues)
                ? LastValue(leftValues)
                : double.NaN;
            var right = node.Right.HasValue && values.TryGetValue(node.Right.Value, out var rightValues)
                ? LastValue(rightValues)
                : double.NaN;

            if (node.Formula != null)
            {
                var result = node.Formula(left, right);
                // Reuse buffer instead of allocating new array each time
                if (!values.TryGetValue(pair.Key, out var buffer))
                {
                    buffer = new double[1];
                    values[pair.Key] = buffer;
                }
                buffer[0] = result;
            }
        }
    }

    private double[]? ResolveStreamingSeries(Dictionary<SeriesHandle, double[]> snapshotSeries, SeriesHandle handle)
    {
        if (snapshotSeries.TryGetValue(handle, out var existing))
        {
            return existing;
        }

        if (!_nodes.TryGetValue(handle, out var node))
        {
            return null;
        }

        ActivateSeries(handle);
        if (node.Kind != SeriesNodeKind.Formula)
        {
            return null;
        }

        if (!node.Left.HasValue || !node.Right.HasValue)
        {
            return null;
        }

        var left = snapshotSeries.TryGetValue(node.Left.Value, out var leftValues)
            ? LastValue(leftValues)
            : double.NaN;
        var right = snapshotSeries.TryGetValue(node.Right.Value, out var rightValues)
            ? LastValue(rightValues)
            : double.NaN;

        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return null;
        }

        if (node.Formula == null)
        {
            return null;
        }

        var result = node.Formula(left, right);
        var computed = new[] { result };
        snapshotSeries[handle] = computed;
        return computed;
    }

    private void ActivateSeries(SeriesHandle handle)
    {
        if (!_activeSeries.Add(handle))
        {
            return;
        }

        AddDependencies(handle);
    }

    private void AddDependencies(SeriesHandle handle)
    {
        if (!_nodes.TryGetValue(handle, out var node))
        {
            return;
        }

        switch (node.Kind)
        {
            case SeriesNodeKind.Indicator:
                if (node.Input.HasValue)
                {
                    ActivateSeries(node.Input.Value);
                }
                break;
            case SeriesNodeKind.Formula:
                if (node.Left.HasValue)
                {
                    ActivateSeries(node.Left.Value);
                }

                if (node.Right.HasValue)
                {
                    ActivateSeries(node.Right.Value);
                }
                break;
        }
    }

    /// <summary>
    /// Publishes a snapshot from streaming mode (respects warmup suppression).
    /// </summary>
    private void PublishStreaming(IndicatorSnapshot snapshot)
    {
        Publish(snapshot, allowWarmupSkip: _behavior.SuppressStreamingWarmup ?? _behavior.EmitWarmup == false);
    }

    /// <summary>
    /// Publishes a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to publish.</param>
    /// <param name="allowWarmupSkip">If true and this is the first emission, skip it (warmup suppression).</param>
    private void Publish(IndicatorSnapshot snapshot, bool allowWarmupSkip = false)
    {
        if (allowWarmupSkip && !_hasEmitted)
        {
            _hasEmitted = true;
            return;
        }

        _hasEmitted = true;
        Latest = snapshot;
        Updated?.Invoke(snapshot);
        EvaluateSignals(snapshot);
    }

    private void EvaluateSignals(IndicatorSnapshot snapshot)
    {
        for (var i = 0; i < _signals.Count; i++)
        {
            var rule = _signals[i];
            if (!rule.Series.TryResolve(snapshot, out var values))
            {
                continue;
            }

            var last = LastValue(values);
            if (double.IsNaN(last))
            {
                continue;
            }

            if (rule.IsCross)
            {
                var triggered = rule.IsCrossTriggered(last, _signalPrevious[i]);
                _signalPrevious[i] = last;
                if (triggered)
                {
                    Dispatch(rule.Handle, rule.Name, last);
                }

                continue;
            }

            var active = rule.IsActive(last);
            if (active && !_signalStates[i])
            {
                Dispatch(rule.Handle, rule.Name, last);
            }

            _signalStates[i] = active;
            _signalPrevious[i] = last;
        }

        EvaluateGroupSignals(snapshot);
        EvaluateRangeSignals(snapshot);
    }

    private void EvaluateRangeSignals(IndicatorSnapshot snapshot)
    {
        for (var i = 0; i < _rangeSignals.Count; i++)
        {
            var rule = _rangeSignals[i];
            if (!rule.Series.TryResolve(snapshot, out var values))
            {
                continue;
            }

            var last = LastValue(values);
            if (double.IsNaN(last))
            {
                continue;
            }

            var active = rule.IsActive(last);
            if (active && !_rangeStates[i])
            {
                Dispatch(rule.Handle, rule.Name, last);
            }

            _rangeStates[i] = active;
        }
    }

    private void EvaluateGroupSignals(IndicatorSnapshot snapshot)
    {
        for (var i = 0; i < _groupSignals.Count; i++)
        {
            var rule = _groupSignals[i];
            var state = _groupStates[i];
            var activeCount = 0;
            var firstValue = double.NaN;

            for (var j = 0; j < rule.Conditions.Length; j++)
            {
                var condition = rule.Conditions[j];
                if (!snapshot.TryGetSeries(condition.Series, out var values))
                {
                    state.PreviousValues[j] = double.NaN;
                    continue;
                }

                var last = LastValue(values);
                if (double.IsNaN(last))
                {
                    state.PreviousValues[j] = last;
                    continue;
                }

                if (double.IsNaN(firstValue))
                {
                    firstValue = last;
                }

                var triggered = condition.IsCross
                    ? condition.IsCrossTriggered(last, state.PreviousValues[j])
                    : condition.IsActive(last);

                if (triggered)
                {
                    activeCount++;
                }

                state.PreviousValues[j] = last;
            }

            var groupActive = rule.IsGroupActive(activeCount);
            if (groupActive)
            {
                state.ActiveBars++;
                if (state.ActiveBars >= rule.Window.Bars && !state.IsActive)
                {
                    Dispatch(rule.Handle, rule.Name, firstValue);
                    state.IsActive = true;
                }
            }
            else
            {
                state.ActiveBars = 0;
                state.IsActive = false;
            }
        }
    }

    private void Dispatch(SignalHandle handle, string name, double value)
    {
        var notification = new NotificationEvent(handle, name, value, DateTime.UtcNow);
        for (var i = 0; i < _notifications.Count; i++)
        {
            // Fire-and-forget async notification with error handling
            var channel = _notifications[i];
            _ = Task.Run(async () =>
            {
                try
                {
                    await channel.NotifyAsync(notification).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[Notification] Error dispatching to channel: {ex.Message}");
                }
            });
        }

        for (var i = 0; i < _autoTrading.Rules.Count; i++)
        {
            var ruleConfig = _autoTrading.Rules[i];
            if (!ruleConfig.Signal.Equals(handle))
            {
                continue;
            }

            try
            {
                ruleConfig.Adapter.Execute(new TradeRequest(handle, ruleConfig.Action, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[AutoTrading] Error executing trade: {ex.Message}");
            }
        }
    }

    private static double LastValue(ReadOnlyMemory<double> values)
    {
        return values.Length == 0 ? double.NaN : values.Span[values.Length - 1];
    }

    private static double LastValue(double[] values)
    {
        return values.Length == 0 ? double.NaN : values[values.Length - 1];
    }

    private static IReadOnlyList<string> ToSymbolStrings(IReadOnlyList<SymbolId> symbols)
    {
        var result = new string[symbols.Count];
        for (var i = 0; i < symbols.Count; i++)
        {
            result[i] = symbols[i].Value;
        }
        return result;
    }

    /// <summary>
    /// Throws if the runtime has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IndicatorRuntime),
                "Cannot access buffer after runtime has been disposed. Copy data with ToList() or ToArray() before disposing.");
        }
    }

    /// <summary>
    /// Disposes the runtime and returns all buffers to the pool atomically.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose streaming session if active
        _streamingSession?.Dispose();
        _streamingSession = null;

        // Clear pooled objects
        lock (_poolLock)
        {
            _streamingValues?.Clear();
            _snapshotPool?.Clear();
            _streamingValues = null;
            _snapshotPool = null;
        }

        // Return all buffers to pool atomically
        lock (_buffersLock)
        {
            foreach (var kvp in _buffers)
            {
                kvp.Value.Dispose();
            }
            _buffers.Clear();
        }

        // Clear event handlers to prevent leaks
        Updated = null;
    }
}
