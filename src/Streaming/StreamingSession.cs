using System.Threading;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class StreamingSession : IDisposable
{
    private readonly IStreamSource _source;
    private readonly StreamingOptions _options;
    private readonly IStreamSubscription _subscription;
    private readonly IStreamObserver _observer;
    private readonly List<StreamingIndicatorEngine.IndicatorRegistration> _registrations = new();
    private readonly List<StreamingIndicatorEngine.StatefulIndicatorRegistration> _stateRegistrations = new();
    private readonly List<StreamingIndicatorEngine.MultiSeriesIndicatorRegistration> _multiSeriesRegistrations = new();
    private bool _disposed;

    public StreamingSession(IStreamSource source, StreamingOptions? options = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? new StreamingOptions();
        Engine = new StreamingIndicatorEngine(_options.CreateEngineOptions());
        _observer = CreateObserver(Engine, _options);
        _subscription = _source.Subscribe(_options.CreateSubscriptionRequest(), _observer);

        if (_options.Indicators != null && _options.Indicators.Count > 0)
        {
            RegisterIndicators(_options.Indicators);
        }
    }

    public StreamingIndicatorEngine Engine { get; }

    public static StreamingSession Create(IStreamSource source, IReadOnlyList<string> symbols,
        IReadOnlyList<BarTimeframe>? timeframes = null, StreamingOptions? options = null)
    {
        if (symbols == null || symbols.Count == 0)
        {
            throw new ArgumentException("Symbols are required.", nameof(symbols));
        }

        var resolvedOptions = options ?? new StreamingOptions();
        resolvedOptions.Symbols = symbols;
        if (timeframes != null)
        {
            resolvedOptions.Timeframes = timeframes;
        }

        return new StreamingSession(source, resolvedOptions);
    }

    public StreamingIndicatorEngine.IndicatorRegistration RegisterIndicator(string symbol, BarTimeframe timeframe,
        Func<StockData, StockData> calculator, Action<IndicatorUpdate> onUpdate,
        IndicatorSubscriptionOptions? options = null)
    {
        var resolvedOptions = options ?? _options.CreateSubscriptionOptions();
        var registration = Engine.RegisterIndicator(symbol, timeframe, calculator, onUpdate, resolvedOptions);
        _registrations.Add(registration);
        return registration;
    }

    public IReadOnlyList<StreamingIndicatorEngine.IndicatorRegistration> RegisterIndicator(string symbol,
        IReadOnlyList<BarTimeframe>? timeframes, Func<StockData, StockData> calculator,
        Action<IndicatorUpdate> onUpdate, IndicatorSubscriptionOptions? options = null)
    {
        if (timeframes == null || timeframes.Count == 0)
        {
            timeframes = _options.GetTimeframes();
        }

        var registrations = new List<StreamingIndicatorEngine.IndicatorRegistration>(timeframes.Count);
        for (var i = 0; i < timeframes.Count; i++)
        {
            var registration = RegisterIndicator(symbol, timeframes[i], calculator, onUpdate, options);
            registrations.Add(registration);
        }

        return registrations;
    }

    public StreamingIndicatorEngine.StatefulIndicatorRegistration RegisterStatefulIndicator(string symbol,
        BarTimeframe timeframe, IStreamingIndicatorState indicator, Action<StreamingIndicatorStateUpdate> onUpdate,
        IndicatorSubscriptionOptions? options = null)
    {
        var resolvedOptions = options ?? _options.CreateSubscriptionOptions();
        var registration = Engine.RegisterStatefulIndicator(symbol, timeframe, indicator, onUpdate, resolvedOptions);
        _stateRegistrations.Add(registration);
        return registration;
    }

    public IReadOnlyList<StreamingIndicatorEngine.StatefulIndicatorRegistration> RegisterStatefulIndicator(
        string symbol, IReadOnlyList<BarTimeframe>? timeframes,
        Func<BarTimeframe, IStreamingIndicatorState> indicatorFactory,
        Action<StreamingIndicatorStateUpdate> onUpdate, IndicatorSubscriptionOptions? options = null)
    {
        if (indicatorFactory == null)
        {
            throw new ArgumentNullException(nameof(indicatorFactory));
        }

        if (timeframes == null || timeframes.Count == 0)
        {
            timeframes = _options.GetTimeframes();
        }

        var registrations = new List<StreamingIndicatorEngine.StatefulIndicatorRegistration>(timeframes.Count);
        for (var i = 0; i < timeframes.Count; i++)
        {
            var registration = RegisterStatefulIndicator(symbol, timeframes[i], indicatorFactory(timeframes[i]),
                onUpdate, options);
            registrations.Add(registration);
        }

        return registrations;
    }

    public StreamingIndicatorEngine.MultiSeriesIndicatorRegistration RegisterMultiSeriesIndicator(
        SeriesKey primarySeries, IReadOnlyList<SeriesKey> dependencies, IMultiSeriesIndicatorState indicator,
        Action<MultiSeriesIndicatorStateUpdate> onUpdate, IndicatorSubscriptionOptions? options = null)
    {
        var resolvedOptions = options ?? _options.CreateSubscriptionOptions();
        var registration = Engine.RegisterMultiSeriesIndicator(primarySeries, dependencies, indicator, onUpdate,
            resolvedOptions);
        _multiSeriesRegistrations.Add(registration);
        return registration;
    }

    public IReadOnlyList<StreamingIndicatorEngine.IndicatorRegistration> RegisterIndicators(
        IReadOnlyList<StreamingIndicatorRegistration> registrations)
    {
        if (registrations == null || registrations.Count == 0)
        {
            return Array.Empty<StreamingIndicatorEngine.IndicatorRegistration>();
        }

        var results = new List<StreamingIndicatorEngine.IndicatorRegistration>();
        for (var i = 0; i < registrations.Count; i++)
        {
            var registration = registrations[i];
            var timeframes = registration.Timeframes ?? _options.GetTimeframes();
            var options = registration.Options;
            var created = RegisterIndicator(registration.Symbol, timeframes, registration.Calculator,
                registration.OnUpdate, options);
            results.AddRange(created);
        }

        return results;
    }

    public IReadOnlyList<StreamingIndicatorEngine.IndicatorRegistration> RegisterAllIndicators(string symbol,
        Action<IndicatorUpdate> onUpdate, IReadOnlyList<BarTimeframe>? timeframes = null,
        IndicatorFilter? filter = null, IndicatorSubscriptionOptions? options = null)
    {
        if (timeframes == null || timeframes.Count == 0)
        {
            timeframes = _options.GetTimeframes();
        }

        var resolvedOptions = options ?? _options.CreateSubscriptionOptions();
        var registrations = Engine.RegisterAllIndicators(symbol, timeframes, onUpdate, resolvedOptions, filter);
        _registrations.AddRange(registrations);
        return registrations;
    }

    public void Start()
    {
        _subscription.Start();
    }

    public void Stop()
    {
        _subscription.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (var i = 0; i < _registrations.Count; i++)
        {
            _registrations[i].Dispose();
        }

        for (var i = 0; i < _stateRegistrations.Count; i++)
        {
            _stateRegistrations[i].Dispose();
        }

        for (var i = 0; i < _multiSeriesRegistrations.Count; i++)
        {
            _multiSeriesRegistrations[i].Dispose();
        }

        _subscription.Dispose();
        if (_observer is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }

    private static IStreamObserver CreateObserver(StreamingIndicatorEngine engine, StreamingOptions options)
    {
        if (options.GetProcessingMode() == StreamingProcessingMode.Buffered)
        {
            return new BufferedStreamObserver(engine, options.GetMaxPendingMessages(), options.GetBackpressurePolicy());
        }

        return engine;
    }

    private sealed class BufferedStreamObserver : IStreamObserver, IDisposable
    {
        private readonly StreamingIndicatorEngine _engine;
        private readonly int _capacity;
        private readonly StreamingBackpressurePolicy _policy;
        private readonly Queue<StreamEvent> _queue;
        private readonly object _sync = new();
        private readonly Thread _worker;
        private bool _stopped;
        private bool _disposed;

        public BufferedStreamObserver(StreamingIndicatorEngine engine, int capacity, StreamingBackpressurePolicy policy)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _capacity = Math.Max(1, capacity);
            _policy = policy;
            _queue = new Queue<StreamEvent>(_capacity);
            _worker = new Thread(ProcessLoop)
            {
                IsBackground = true,
                Name = "Ooples.StreamingWorker"
            };
            _worker.Start();
        }

        public void OnTrade(StreamTrade trade)
        {
            if (trade == null)
            {
                return;
            }

            Enqueue(StreamEvent.FromTrade(trade));
        }

        public void OnQuote(StreamQuote quote)
        {
            if (quote == null)
            {
                return;
            }

            Enqueue(StreamEvent.FromQuote(quote));
        }

        public void OnBar(OhlcvBar bar)
        {
            if (bar == null)
            {
                return;
            }

            Enqueue(StreamEvent.FromBar(bar));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_sync)
            {
                _stopped = true;
                Monitor.PulseAll(_sync);
            }

            _worker.Join();
            _disposed = true;
        }

        private void Enqueue(StreamEvent streamEvent)
        {
            lock (_sync)
            {
                if (_stopped)
                {
                    return;
                }

                if (_queue.Count >= _capacity)
                {
                    switch (_policy)
                    {
                        case StreamingBackpressurePolicy.DropNewest:
                            return;
                        case StreamingBackpressurePolicy.DropOldest:
                            _queue.Dequeue();
                            break;
                        case StreamingBackpressurePolicy.Block:
                            while (_queue.Count >= _capacity && !_stopped)
                            {
                                Monitor.Wait(_sync);
                            }

                            if (_stopped)
                            {
                                return;
                            }
                            break;
                    }
                }

                _queue.Enqueue(streamEvent);
                Monitor.Pulse(_sync);
            }
        }

        private void ProcessLoop()
        {
            while (true)
            {
                StreamEvent next;
                lock (_sync)
                {
                    while (_queue.Count == 0 && !_stopped)
                    {
                        Monitor.Wait(_sync);
                    }

                    if (_queue.Count == 0 && _stopped)
                    {
                        return;
                    }

                    next = _queue.Dequeue();
                    Monitor.PulseAll(_sync);
                }

                Dispatch(next);
            }
        }

        private void Dispatch(StreamEvent streamEvent)
        {
            switch (streamEvent.Kind)
            {
                case StreamEventKind.Trade:
                    _engine.OnTrade(streamEvent.Trade!);
                    break;
                case StreamEventKind.Quote:
                    _engine.OnQuote(streamEvent.Quote!);
                    break;
                case StreamEventKind.Bar:
                    _engine.OnBar(streamEvent.Bar!);
                    break;
            }
        }

        private readonly struct StreamEvent
        {
            private StreamEvent(StreamEventKind kind, StreamTrade? trade, StreamQuote? quote, OhlcvBar? bar)
            {
                Kind = kind;
                Trade = trade;
                Quote = quote;
                Bar = bar;
            }

            public StreamEventKind Kind { get; }
            public StreamTrade? Trade { get; }
            public StreamQuote? Quote { get; }
            public OhlcvBar? Bar { get; }

            public static StreamEvent FromTrade(StreamTrade trade) => new(StreamEventKind.Trade, trade, null, null);
            public static StreamEvent FromQuote(StreamQuote quote) => new(StreamEventKind.Quote, null, quote, null);
            public static StreamEvent FromBar(OhlcvBar bar) => new(StreamEventKind.Bar, null, null, bar);
        }

        private enum StreamEventKind
        {
            Trade,
            Quote,
            Bar
        }
    }
}
