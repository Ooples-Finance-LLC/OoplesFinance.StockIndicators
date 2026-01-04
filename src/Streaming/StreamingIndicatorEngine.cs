using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class StreamingIndicatorEngine : IStreamObserver
{
    private readonly Dictionary<AggregatorKey, BarAggregator> _aggregators = new();
    private readonly Dictionary<AggregatorKey, List<IndicatorSubscription>> _subscriptions = new();
    private readonly Dictionary<AggregatorKey, List<IndicatorStateSubscription>> _stateSubscriptions = new();
    private readonly Dictionary<string, List<BarAggregator>> _aggregatorsBySymbol = new(StringComparer.OrdinalIgnoreCase);
    private readonly StreamingIndicatorEngineOptions _options;

    public StreamingIndicatorEngine(StreamingIndicatorEngineOptions? options = null)
    {
        _options = options ?? new StreamingIndicatorEngineOptions();
    }

    private void EnsureAggregator(AggregatorKey key)
    {
        if (_aggregators.TryGetValue(key, out _))
        {
            return;
        }

        var barOptions = _options.CreateBarOptions(key.Symbol, key.Timeframe);
        var aggregator = new BarAggregator(barOptions);
        aggregator.BarUpdated += bar => HandleBar(key, bar);
        aggregator.BarClosed += bar => HandleBar(key, bar);
        _aggregators[key] = aggregator;

        if (!_aggregatorsBySymbol.TryGetValue(key.Symbol, out var list))
        {
            list = new List<BarAggregator>();
            _aggregatorsBySymbol[key.Symbol] = list;
        }

        list.Add(aggregator);
    }

    public IndicatorRegistration RegisterIndicator(string symbol, BarTimeframe timeframe,
        Func<StockData, StockData> calculator, Action<IndicatorUpdate> onUpdate,
        IndicatorSubscriptionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (timeframe == null)
        {
            throw new ArgumentNullException(nameof(timeframe));
        }

        if (calculator == null)
        {
            throw new ArgumentNullException(nameof(calculator));
        }

        if (onUpdate == null)
        {
            throw new ArgumentNullException(nameof(onUpdate));
        }

        var key = new AggregatorKey(symbol, timeframe);
        EnsureAggregator(key);

        if (!_subscriptions.TryGetValue(key, out var subs))
        {
            subs = new List<IndicatorSubscription>();
            _subscriptions[key] = subs;
        }

        var subscription = new IndicatorSubscription(symbol, timeframe, calculator, onUpdate, options);
        subs.Add(subscription);

        return new IndicatorRegistration(this, key, subscription);
    }

    public IReadOnlyList<IndicatorRegistration> RegisterIndicator(string symbol,
        IReadOnlyList<BarTimeframe> timeframes, Func<StockData, StockData> calculator,
        Action<IndicatorUpdate> onUpdate, IndicatorSubscriptionOptions? options = null)
    {
        if (timeframes == null || timeframes.Count == 0)
        {
            throw new ArgumentException("At least one timeframe is required.", nameof(timeframes));
        }

        var registrations = new List<IndicatorRegistration>(timeframes.Count);
        for (var i = 0; i < timeframes.Count; i++)
        {
            registrations.Add(RegisterIndicator(symbol, timeframes[i], calculator, onUpdate, options));
        }

        return registrations;
    }

    public StatefulIndicatorRegistration RegisterStatefulIndicator(string symbol, BarTimeframe timeframe,
        IStreamingIndicatorState indicator, Action<StreamingIndicatorStateUpdate> onUpdate,
        IndicatorSubscriptionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (timeframe == null)
        {
            throw new ArgumentNullException(nameof(timeframe));
        }

        if (indicator == null)
        {
            throw new ArgumentNullException(nameof(indicator));
        }

        if (onUpdate == null)
        {
            throw new ArgumentNullException(nameof(onUpdate));
        }

        var key = new AggregatorKey(symbol, timeframe);
        EnsureAggregator(key);

        if (!_stateSubscriptions.TryGetValue(key, out var subs))
        {
            subs = new List<IndicatorStateSubscription>();
            _stateSubscriptions[key] = subs;
        }

        var subscription = new IndicatorStateSubscription(symbol, timeframe, indicator, onUpdate, options);
        subs.Add(subscription);

        return new StatefulIndicatorRegistration(this, key, subscription);
    }

    public IReadOnlyList<StatefulIndicatorRegistration> RegisterStatefulIndicator(string symbol,
        IReadOnlyList<BarTimeframe> timeframes, Func<BarTimeframe, IStreamingIndicatorState> indicatorFactory,
        Action<StreamingIndicatorStateUpdate> onUpdate, IndicatorSubscriptionOptions? options = null)
    {
        if (timeframes == null || timeframes.Count == 0)
        {
            throw new ArgumentException("At least one timeframe is required.", nameof(timeframes));
        }

        if (indicatorFactory == null)
        {
            throw new ArgumentNullException(nameof(indicatorFactory));
        }

        var registrations = new List<StatefulIndicatorRegistration>(timeframes.Count);
        for (var i = 0; i < timeframes.Count; i++)
        {
            var timeframe = timeframes[i];
            var indicator = indicatorFactory(timeframe);
            registrations.Add(RegisterStatefulIndicator(symbol, timeframe, indicator, onUpdate, options));
        }

        return registrations;
    }

    public IReadOnlyList<IndicatorRegistration> RegisterAllIndicators(string symbol, BarTimeframe timeframe,
        Action<IndicatorUpdate> onUpdate, IndicatorSubscriptionOptions? options = null, IndicatorFilter? filter = null)
    {
        if (timeframe == null)
        {
            throw new ArgumentNullException(nameof(timeframe));
        }

        return RegisterAllIndicators(symbol, new[] { timeframe }, onUpdate, options, filter);
    }

    public IReadOnlyList<IndicatorRegistration> RegisterAllIndicators(string symbol, IReadOnlyList<BarTimeframe> timeframes,
        Action<IndicatorUpdate> onUpdate, IndicatorSubscriptionOptions? options = null, IndicatorFilter? filter = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (timeframes == null || timeframes.Count == 0)
        {
            throw new ArgumentException("At least one timeframe is required.", nameof(timeframes));
        }

        if (onUpdate == null)
        {
            throw new ArgumentNullException(nameof(onUpdate));
        }

        var definitions = IndicatorRegistry.GetDefinitions();
        var registrations = new List<IndicatorRegistration>(definitions.Count * timeframes.Count);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (filter != null && !filter.Matches(definition))
            {
                continue;
            }

            var calculator = definition.Calculator;
            for (var j = 0; j < timeframes.Count; j++)
            {
                registrations.Add(RegisterIndicator(symbol, timeframes[j], calculator, onUpdate, options));
            }
        }

        return registrations;
    }

    public void OnTrade(StreamTrade trade)
    {
        if (trade == null)
        {
            return;
        }

        if (_aggregatorsBySymbol.TryGetValue(trade.Symbol, out var list))
        {
            for (var i = 0; i < list.Count; i++)
            {
                list[i].AddTrade(trade);
            }
        }
    }

    public void OnQuote(StreamQuote quote)
    {
        if (quote == null)
        {
            return;
        }

        if (_aggregatorsBySymbol.TryGetValue(quote.Symbol, out var list))
        {
            for (var i = 0; i < list.Count; i++)
            {
                list[i].AddQuote(quote);
            }
        }
    }

    public void OnBar(OhlcvBar bar)
    {
        if (bar == null)
        {
            return;
        }

        var key = new AggregatorKey(bar.Symbol, bar.Timeframe);
        HandleBar(key, bar);
    }

    private void HandleBar(AggregatorKey key, OhlcvBar bar)
    {
        if (_subscriptions.TryGetValue(key, out var subs))
        {
            for (var i = 0; i < subs.Count; i++)
            {
                subs[i].HandleBar(bar);
            }
        }

        if (_stateSubscriptions.TryGetValue(key, out var stateSubs))
        {
            for (var i = 0; i < stateSubs.Count; i++)
            {
                stateSubs[i].HandleBar(bar);
            }
        }
    }

    private void Unregister(AggregatorKey key, IndicatorSubscription subscription)
    {
        if (_subscriptions.TryGetValue(key, out var subs))
        {
            subs.Remove(subscription);
            if (subs.Count == 0)
            {
                _subscriptions.Remove(key);
            }
        }
    }

    private void UnregisterStateful(AggregatorKey key, IndicatorStateSubscription subscription)
    {
        if (_stateSubscriptions.TryGetValue(key, out var subs))
        {
            subs.Remove(subscription);
            if (subs.Count == 0)
            {
                _stateSubscriptions.Remove(key);
            }
        }

        subscription.Dispose();
    }

    public sealed class IndicatorRegistration : IDisposable
    {
        private readonly StreamingIndicatorEngine _engine;
        private readonly AggregatorKey _key;
        private readonly IndicatorSubscription _subscription;
        private bool _disposed;

        internal IndicatorRegistration(StreamingIndicatorEngine engine, AggregatorKey key, IndicatorSubscription subscription)
        {
            _engine = engine;
            _key = key;
            _subscription = subscription;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _engine.Unregister(_key, _subscription);
            _disposed = true;
        }
    }

    public sealed class StatefulIndicatorRegistration : IDisposable
    {
        private readonly StreamingIndicatorEngine _engine;
        private readonly AggregatorKey _key;
        private readonly IndicatorStateSubscription _subscription;
        private bool _disposed;

        internal StatefulIndicatorRegistration(StreamingIndicatorEngine engine, AggregatorKey key,
            IndicatorStateSubscription subscription)
        {
            _engine = engine;
            _key = key;
            _subscription = subscription;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _engine.UnregisterStateful(_key, _subscription);
            _disposed = true;
        }
    }

    internal readonly struct AggregatorKey : IEquatable<AggregatorKey>
    {
        public AggregatorKey(string symbol, BarTimeframe timeframe)
        {
            Symbol = symbol;
            Timeframe = timeframe;
        }

        public string Symbol { get; }
        public BarTimeframe Timeframe { get; }

        public bool Equals(AggregatorKey other)
        {
            return string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase)
                && Timeframe.Equals(other.Timeframe);
        }

        public override bool Equals(object? obj)
        {
            return obj is AggregatorKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Symbol != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Symbol) : 0) * 397)
                    ^ Timeframe.GetHashCode();
            }
        }
    }

    internal sealed class IndicatorSubscription
    {
        private readonly string _symbol;
        private readonly BarTimeframe _timeframe;
        private readonly Func<StockData, StockData> _calculator;
        private readonly Action<IndicatorUpdate> _onUpdate;
        private readonly IndicatorSubscriptionOptions _options;
        private readonly List<TickerData> _bars = new();

        public IndicatorSubscription(string symbol, BarTimeframe timeframe,
            Func<StockData, StockData> calculator, Action<IndicatorUpdate> onUpdate,
            IndicatorSubscriptionOptions? options)
        {
            _symbol = symbol;
            _timeframe = timeframe;
            _calculator = calculator;
            _onUpdate = onUpdate;
            _options = options ?? new IndicatorSubscriptionOptions();
        }

        public void HandleBar(OhlcvBar bar)
        {
            if (!_options.IncludeUpdates && !bar.IsFinal)
            {
                return;
            }

            if (_bars.Count == 0 || bar.StartTime > _bars[_bars.Count - 1].Date)
            {
                _bars.Add(ToTickerData(bar));
            }
            else
            {
                UpdateLast(bar);
            }

            var stockData = new StockData(_bars, _options.InputName)
            {
                Options = _options.Options
            };

            var result = _calculator(stockData);
            _onUpdate(new IndicatorUpdate(_symbol, _timeframe, bar.IsFinal, result));
        }

        private void UpdateLast(OhlcvBar bar)
        {
            var last = _bars[_bars.Count - 1];
            last.Date = bar.StartTime;
            last.Open = bar.Open;
            last.High = bar.High;
            last.Low = bar.Low;
            last.Close = bar.Close;
            last.Volume = bar.Volume;
        }

        private static TickerData ToTickerData(OhlcvBar bar)
        {
            return new TickerData
            {
                Date = bar.StartTime,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume
            };
        }
    }

    internal sealed class IndicatorStateSubscription : IDisposable
    {
        private readonly string _symbol;
        private readonly BarTimeframe _timeframe;
        private readonly IStreamingIndicatorState _indicator;
        private readonly Action<StreamingIndicatorStateUpdate> _onUpdate;
        private readonly IndicatorSubscriptionOptions _options;

        public IndicatorStateSubscription(string symbol, BarTimeframe timeframe,
            IStreamingIndicatorState indicator, Action<StreamingIndicatorStateUpdate> onUpdate,
            IndicatorSubscriptionOptions? options)
        {
            _symbol = symbol;
            _timeframe = timeframe;
            _indicator = indicator;
            _onUpdate = onUpdate;
            _options = options ?? new IndicatorSubscriptionOptions();
        }

        public void HandleBar(OhlcvBar bar)
        {
            if (!_options.IncludeUpdates && !bar.IsFinal)
            {
                return;
            }

            var result = _indicator.Update(bar, bar.IsFinal, _options.IncludeOutputValues);
            _onUpdate(new StreamingIndicatorStateUpdate(_symbol, _timeframe, bar.IsFinal, _indicator.Name,
                result.Value, result.Outputs));
        }

        public void Dispose()
        {
            if (_indicator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

public sealed class IndicatorUpdate
{
    public IndicatorUpdate(string symbol, BarTimeframe timeframe, bool isFinalBar, StockData indicatorData)
    {
        Symbol = symbol;
        Timeframe = timeframe;
        IsFinalBar = isFinalBar;
        IndicatorData = indicatorData;
    }

    public string Symbol { get; }
    public BarTimeframe Timeframe { get; }
    public bool IsFinalBar { get; }
    public StockData IndicatorData { get; }
}

public sealed class IndicatorSubscriptionOptions
{
    public bool IncludeUpdates { get; set; } = true;
    public bool IncludeOutputValues { get; set; } = true;
    public InputName InputName { get; set; } = InputName.Close;
    public IndicatorOptions? Options { get; set; }
}

public sealed class StreamingIndicatorEngineOptions
{
    public bool EmitUpdates { get; set; } = true;
    public QuotePriceMode QuotePriceMode { get; set; } = QuotePriceMode.Mid;
    public OutOfOrderPolicy OutOfOrderPolicy { get; set; } = OutOfOrderPolicy.Drop;
    public TimeSpan ReorderWindow { get; set; } = TimeSpan.Zero;
    public int MaxBufferSize { get; set; } = 1024;

    internal BarAggregatorOptions CreateBarOptions(string symbol, BarTimeframe timeframe)
    {
        return new BarAggregatorOptions(symbol, timeframe)
        {
            EmitUpdates = EmitUpdates,
            QuotePriceMode = QuotePriceMode,
            OutOfOrderPolicy = OutOfOrderPolicy,
            ReorderWindow = ReorderWindow,
            MaxBufferSize = MaxBufferSize
        };
    }
}
