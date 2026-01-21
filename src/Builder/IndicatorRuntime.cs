using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Builder.Signals;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Runtime for executing indicators and processing signals.
/// </summary>
public sealed class IndicatorRuntime
{
    private readonly IndicatorDataSource _source;
    private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
    private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
    private readonly IReadOnlyList<SignalRule> _signals;
    private readonly IReadOnlyList<SignalGroupRule> _groupSignals;
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
    private bool _hasEmitted;
    private bool _started;

    internal IndicatorRuntime(
        IndicatorDataSource source,
        Dictionary<SeriesHandle, SeriesNode> nodes,
        Dictionary<IndicatorKey, SeriesHandle> keys,
        IReadOnlyList<SignalRule> signals,
        IReadOnlyList<SignalGroupRule> groupSignals,
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
        _notifications = notifications;
        _autoTrading = autoTrading;
        _behavior = behavior;
        _activeSeries = new HashSet<SeriesHandle>(activeSeries);
        _symbols = symbols;
        _streamingOptions = streamingOptions;
        _signalOptions = signalOptions;
        _backtestOptions = backtestOptions;
        _benchmarkOptions = benchmarkOptions;
        _signalStates = new bool[signals.Count];
        _signalPrevious = new double?[signals.Count];
        _groupStates = new SignalGroupState[groupSignals.Count];
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
    /// Event raised when indicators are updated.
    /// </summary>
    public event Action<IndicatorSnapshot>? Updated;

    /// <summary>
    /// Subscribes to series handles for computation.
    /// </summary>
    public void Subscribe(params SeriesHandle[] handles)
    {
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

        var values = new Dictionary<SeriesHandle, double[]>(_nodes.Count);
        using var session = StreamingSession.Create(stream, symbols, options: options);
        var subscriptionOptions = options.CreateSubscriptionOptions();

        foreach (var pair in _nodes)
        {
            if (pair.Value.Kind != SeriesNodeKind.Indicator)
            {
                continue;
            }

            var handle = pair.Key;
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

            session.RegisterStatefulIndicator(key.Symbol.Value, key.Timeframe, state, update =>
            {
                var value = StreamingIndicatorFactory.ExtractValue(update, spec);
                values[handle] = new[] { value };
                UpdateFormulaNodes(values);
                var snapshotSeries = new Dictionary<SeriesHandle, double[]>(values);
                Publish(new IndicatorSnapshot(snapshotSeries, _keys, requested => ResolveStreamingSeries(snapshotSeries, requested)));
            }, subscriptionOptions);
        }

        session.Start();
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
                values[pair.Key] = new[] { result };
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

    private void Publish(IndicatorSnapshot snapshot)
    {
        if (!_behavior.EmitWarmup && !_hasEmitted)
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
            _notifications[i].Notify(notification);
        }

        for (var i = 0; i < _autoTrading.Rules.Count; i++)
        {
            var ruleConfig = _autoTrading.Rules[i];
            if (!ruleConfig.Signal.Equals(handle))
            {
                continue;
            }

            ruleConfig.Adapter.Execute(new TradeRequest(handle, ruleConfig.Action, DateTime.UtcNow));
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
}
