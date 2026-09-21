using OoplesFinance.StockIndicators.Builder.Backtest;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Fluent builder for configuring stock indicators.
/// </summary>
public sealed class StockIndicatorBuilder
{
    private IndicatorDataSource? _configuredSource;
    private Indicators.IBarSource? _barSource;
    private readonly List<Indicators.IIndicator> _configuredIndicators = new();
    private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
    private readonly Dictionary<IndicatorNodeKey, SeriesHandle> _indicatorNodes;
    private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
    private readonly Dictionary<SeriesKey, SeriesHandle> _baseSeries;
    private readonly Dictionary<string, IndicatorDataSource> _namedSources;
    private readonly IndicatorCatalog _indicators;
    private readonly SignalCatalog _signals;
    private readonly NotificationCatalog _notifications;
    private readonly AutoTradingCatalog _autoTrading;
    private readonly BehaviorOptions _behavior;
    private IndicatorOptions? _indicatorOptions;
    private SignalOptions? _signalOptions;
    private SymbolOptions? _symbolOptions;
    private DataOptions? _dataOptions;
    private BacktestOptions? _backtestOptions;
    private BenchmarkOptions? _benchmarkOptions;
    private IReadOnlyList<SymbolId>? _resolvedSymbols;
    private BarTimeframe? _resolvedTimeframe;
    private SeriesKey? _defaultSeriesKey;
    private bool _defaultsApplied;
    private int _nextId;

    /// <summary>
    /// Creates a new stock indicator builder.
    /// </summary>
    /// <param name="source">The data source to use.</param>
    public StockIndicatorBuilder()
        : this(null, deferred: true)
    {
    }

    /// <summary>Creates a builder over a data source.</summary>
    /// <param name="source">The data source to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    public StockIndicatorBuilder(IndicatorDataSource source)
        : this(source ?? throw new ArgumentNullException(nameof(source)), deferred: false)
    {
    }

    private StockIndicatorBuilder(IndicatorDataSource? source, bool deferred = true)
    {
        _ = deferred;
        _configuredSource = source;
        _nodes = new Dictionary<SeriesHandle, SeriesNode>();
        _indicatorNodes = new Dictionary<IndicatorNodeKey, SeriesHandle>();
        _keys = new Dictionary<IndicatorKey, SeriesHandle>();
        _baseSeries = new Dictionary<SeriesKey, SeriesHandle>();
        _namedSources = new Dictionary<string, IndicatorDataSource>(StringComparer.OrdinalIgnoreCase);
        _behavior = new BehaviorOptions();
        _signals = new SignalCatalog();
        _notifications = new NotificationCatalog();
        _autoTrading = new AutoTradingCatalog();
        _indicators = new IndicatorCatalog(this);
        _nextId = 1;
    }

    /// <summary>
    /// Adds a named data source for multi-stock indicators (e.g., market comparison).
    /// </summary>
    /// <param name="name">The name to identify this data source (e.g., "market", "spy").</param>
    /// <param name="source">The data source containing the comparison data.</param>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// Multi-stock indicators like RSMKIndicator compare a stock against a market index.
    /// Use this method to register comparison data, then reference it via catalog.Price("market").
    /// </remarks>
    public StockIndicatorBuilder AddDataSource(string name, IndicatorDataSource source)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Data source name cannot be null or empty.", nameof(name));
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        _namedSources[name] = source;
        return this;
    }

    /// <summary>
    /// Gets a named data source.
    /// </summary>
    internal IndicatorDataSource? GetNamedSource(string name)
    {
        return _namedSources.TryGetValue(name, out var source) ? source : null;
    }

    /// <summary>
    /// Gets the primary data source.
    /// </summary>
    /// <summary>
    /// Reads the bars from <paramref name="source"/> when the run is built.
    /// </summary>
    /// <remarks>
    /// Finite or live is the source''s business, not the builder''s. That is the whole point: the two
    /// programs differ by which source is handed over and by nothing else.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <summary>Publishes live bars from the first one, before the indicators have warmed up.</summary>
    /// <remarks>
    /// A live run suppresses bars computed over less than <see cref="Indicators.IIndicator.WarmupBars"/>
    /// inputs, because their values are arithmetic over too little history to act on. Call this to receive
    /// them anyway - to show a feed filling, say - and read
    /// <see cref="Indicators.IBarSnapshot.IsWarmedUp"/> to tell which are worth acting on. It has no effect
    /// on a finite source.
    /// </remarks>
    private bool _publishBeforeWarmup;

    public StockIndicatorBuilder PublishBeforeWarmup()
    {
        _publishBeforeWarmup = true;
        return this;
    }

    public StockIndicatorBuilder ConfigureSource(Indicators.IBarSource source)
    {
        _barSource = source ?? throw new ArgumentNullException(nameof(source));
        return this;
    }

    /// <summary>
    /// The indicators to compute, as objects rather than names.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when indicators, or any of them, is null.</exception>
    public StockIndicatorBuilder ConfigureIndicators(params Indicators.IIndicator[] indicators)
    {
        if (indicators is null) throw new ArgumentNullException(nameof(indicators));

        foreach (var indicator in indicators)
        {
            if (indicator is null)
            {
                throw new ArgumentNullException(nameof(indicators), "An indicator cannot be null.");
            }

            _configuredIndicators.Add(indicator);
        }

        return this;
    }

    /// <summary>
    /// The indicators to compute, from any sequence.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when indicators is null.</exception>
    public StockIndicatorBuilder ConfigureIndicators(IEnumerable<Indicators.IIndicator> indicators)
    {
        if (indicators is null) throw new ArgumentNullException(nameof(indicators));
        return ConfigureIndicators([.. indicators]);
    }

    /// <summary>
    /// Reads the source and computes every configured indicator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backed by the existing evaluator: each indicator becomes the specification its batch calculation
    /// already understands, so the values are the ones the v1 surface has always produced. A new API that
    /// returns different numbers is not a new API.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when no source was configured.</exception>
    public async Task<Indicators.IIndicatorRun> BuildAsync(CancellationToken cancellationToken = default)
    {
        var source = _barSource ?? throw new InvalidOperationException(
            "No bar source. Call ConfigureSource before BuildAsync.");

        // Before the drain, not after: a live source never runs out, so reading it into lists first blocks
        // forever. That is exactly what it did.
        if (!source.IsFinite)
        {
            return await BuildLiveAsync(source, cancellationToken).ConfigureAwait(false);
        }

        var opens = new List<double>();
        var highs = new List<double>();
        var lows = new List<double>();
        var closes = new List<double>();
        var volumes = new List<double>();
        var dates = new List<DateTime>();
        var bars = new List<Indicators.Bar>();

        // Warm-up first, and counted, so the indicators see it but the caller does not. A finite source that
        // carries warm-up would otherwise either publish it as real bars or not be warmed at all.
        var warmupCount = 0;
        await foreach (var bar in source.ReadWarmupAsync(cancellationToken).ConfigureAwait(false))
        {
            opens.Add(bar.Open);
            highs.Add(bar.High);
            lows.Add(bar.Low);
            closes.Add(bar.Close);
            volumes.Add(bar.Volume);
            dates.Add(bar.Time);
            bars.Add(bar);
            warmupCount++;
        }

        await foreach (var bar in source.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            opens.Add(bar.Open);
            highs.Add(bar.High);
            lows.Add(bar.Low);
            closes.Add(bar.Close);
            volumes.Add(bar.Volume);
            dates.Add(bar.Time);
            bars.Add(bar);
        }


        _configuredSource = IndicatorDataSource.FromBatch(
            new StockData(opens, highs, lows, closes, volumes, dates));

        // Everything reachable, not just what was configured: an indicator used as a component or chained
        // onto still has to be computed, and a built-in one still belongs in the evaluator rather than being
        // re-implemented by the custom engine.
        var reachable = new List<Indicators.IIndicator>();
        var seen = new HashSet<Indicators.IIndicator>(Indicators.IndicatorIdentity.Comparer);
        foreach (var indicator in _configuredIndicators)
        {
            CollectReachable(indicator, seen, reachable);
        }

        var handles = new Dictionary<Indicators.IIndicator, SeriesHandle[]>(Indicators.IndicatorIdentity.Comparer);
        foreach (var indicator in reachable)
        {
            Indicators.IndicatorContract.RequireComputable(indicator);

            if (indicator is not Indicators.IBuiltInIndicator builtIn)
            {
                continue;
            }

            var series = _indicators.Price();
            var seriesKey = ResolveSeriesKey(series);
            var slots = new SeriesHandle[indicator.Outputs.Count];

            for (var slot = 0; slot < indicator.Outputs.Count; slot++)
            {
                var outputKey = indicator.Outputs.Count > 1
                    ? OutputKeyFor(indicator, slot)
                    : builtIn.BatchOutputKey;

                var spec = outputKey is null
                    ? IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions())
                    : IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions(), outputKey);

                slots[slot] = AddIndicator(spec, series, seriesKey, null);
            }

            handles[indicator] = slots;
        }

        var runtime = Build();
        runtime.Start();

        var engine = new Indicators.CustomIndicatorEngine(bars, indicator =>
        {
            if (!handles.TryGetValue(indicator, out var slots))
            {
                return null;
            }

            var values = new double[slots.Length][];
            for (var i = 0; i < slots.Length; i++)
            {
                values[i] = runtime.GetSeries(slots[i]).AsSpan().ToArray();
            }

            return values;
        });

        var series2 = new Dictionary<Indicators.IIndicatorOutput, double[]>();
        foreach (var indicator in _configuredIndicators)
        {
            var values = engine.Compute(indicator);
            for (var slot = 0; slot < indicator.Outputs.Count; slot++)
            {
                // The warm-up primed the states; it is not part of the answer.
                series2[indicator.Outputs[slot]] = warmupCount == 0
                    ? values[slot]
                    : values[slot].Skip(warmupCount).ToArray();
            }
        }

        return new Indicators.IndicatorRun(
            runtime, series2, warmupCount == 0 ? bars : bars.Skip(warmupCount).ToList());
    }

    /// <summary>
    /// A run over a source whose bars have not run out.
    /// </summary>
    /// <remarks>
    /// Built-ins are driven by their streaming states, which is the only way to compute them a bar at a time.
    /// Not every indicator has one - the factory returns null - and saying which one cannot run live beats
    /// publishing a number from something else.
    /// </remarks>
    /// <exception cref="NotSupportedException">Thrown when an indicator cannot be computed a bar at a time.</exception>
    private async Task<Indicators.IIndicatorRun> BuildLiveAsync(
        Indicators.IBarSource source, CancellationToken cancellationToken)
    {
        var reachable = new List<Indicators.IIndicator>();
        var seen = new HashSet<Indicators.IIndicator>(Indicators.IndicatorIdentity.Comparer);
        foreach (var indicator in _configuredIndicators)
        {
            CollectReachable(indicator, seen, reachable);
        }

        var states = new Dictionary<Indicators.IIndicator, object>(Indicators.IndicatorIdentity.Comparer);
        var outputKeys = new Dictionary<Indicators.IIndicator, IReadOnlyList<string>>(
            Indicators.IndicatorIdentity.Comparer);

        foreach (var indicator in reachable)
        {
            Indicators.IndicatorContract.RequireComputable(indicator);

            if (indicator is Indicators.IBuiltInIndicator builtIn)
            {
                var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
                var streaming = StreamingIndicatorFactory.CreateState(spec)
                    ?? throw new NotSupportedException(
                        indicator.GetType().Name + " has no streaming state, so it cannot run against a live "
                        + "source. Use a finite source, or an indicator that can be computed a bar at a time.");

                states[indicator] = streaming;
                outputKeys[indicator] = GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
                continue;
            }

            var own = indicator switch
            {
                Indicators.IndicatorBase single => single.CreateState(),
                Indicators.MultiOutputIndicatorBase multi => multi.CreateState(),
                _ => null
            };

            states[indicator] = own ?? throw new NotSupportedException(
                indicator.GetType().Name + " supplies no arithmetic.");
        }

        var run = new Indicators.LiveIndicatorRun(source, reachable, states, outputKeys, _configuredIndicators)
        {
            PublishBeforeWarmup = _publishBeforeWarmup
        };
        await run.WarmAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }
    /// <summary>Walks an indicator's components and chained source, depth first, without repeating one.</summary>
    private static void CollectReachable(
        Indicators.IIndicator indicator,
        HashSet<Indicators.IIndicator> seen,
        List<Indicators.IIndicator> ordered)
    {
        if (!seen.Add(indicator))
        {
            return;
        }

        foreach (var component in indicator.Components)
        {
            CollectReachable(component, seen, ordered);
        }

        if (indicator.Source is not null)
        {
            CollectReachable(indicator.Source, seen, ordered);
        }

        ordered.Add(indicator);
    }
    /// <summary>The published key a multi-output indicator''s slot stands for.</summary>
    private static string? OutputKeyFor(Indicators.IIndicator indicator, int slot) =>
        GeneratedIndicatorOutputs.KeysFor(((Indicators.IBuiltInIndicator)indicator).BatchName) is { } keys
        && slot < keys.Count
            ? keys[slot]
            : null;

    internal IndicatorDataSource PrimarySource => Source;

    /// <summary>
    /// The data source, once one has been configured.
    /// </summary>
    /// <remarks>
    /// Deferred rather than a constructor argument, because ConfigureSource takes an IBarSource whose bars
    /// have to be read before a StockData exists to hand over. Asking for it too early is a mistake worth
    /// naming rather than a null reference somewhere downstream.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when no source has been configured.</exception>
    private IndicatorDataSource Source =>
        _configuredSource ?? throw new InvalidOperationException(
            "No data source. Pass one to the constructor, or call ConfigureSource before building.");

    /// <summary>
    /// Configures the symbols to use.
    /// </summary>
    public StockIndicatorBuilder ConfigureSymbols(SymbolOptions? options = null)
    {
        _symbolOptions = options;
        _resolvedSymbols = null;
        _defaultSeriesKey = null;
        return this;
    }

    /// <summary>
    /// Configures data options.
    /// </summary>
    public StockIndicatorBuilder ConfigureData(DataOptions? options = null)
    {
        _dataOptions = options;
        _resolvedTimeframe = null;
        _defaultSeriesKey = null;
        return this;
    }

    /// <summary>
    /// Configures indicators through the catalog lambda.
    /// </summary>
    /// <remarks>
    /// Superseded by the overloads taking indicator objects, where chaining is <c>new Sma(9).Of(rsi)</c>
    /// rather than a captured <see cref="SeriesHandle"/>, and a result is addressed by the object the caller
    /// configured rather than by a handle they had to remember to keep.
    /// </remarks>
    [Obsolete("Pass indicator objects instead: ConfigureIndicators(new Rsi(14), new Sma(9).Of(rsi)). "
        + "This overload will be removed after v2 ships.")]
    public StockIndicatorBuilder ConfigureIndicators(IndicatorOptions? options = null, Action<IndicatorCatalog>? configure = null)
    {
        _indicatorOptions = options ?? new IndicatorOptions();
        _defaultsApplied = false;
        ApplyIndicatorDefaults();
        configure?.Invoke(_indicators);
        return this;
    }

    /// <summary>
    /// Configures indicators with a callback.
    /// </summary>
    [Obsolete("Pass indicator objects instead: ConfigureIndicators(new Rsi(14), new Sma(9).Of(rsi)). "
        + "This overload will be removed after v2 ships.")]
    public StockIndicatorBuilder ConfigureIndicators(Action<IndicatorCatalog> configure)
    {
#pragma warning disable CS0618 // The pair is deprecated together; one calling the other is not a new use.
        return ConfigureIndicators(null, configure);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Configures signals.
    /// </summary>
    public StockIndicatorBuilder ConfigureSignals(SignalOptions? options = null, Action<SignalCatalog>? configure = null)
    {
        _signalOptions = options ?? new SignalOptions();
        configure?.Invoke(_signals);
        return this;
    }

    /// <summary>
    /// Configures signals with a callback.
    /// </summary>
    public StockIndicatorBuilder ConfigureSignals(Action<SignalCatalog> configure)
    {
        return ConfigureSignals(null, configure);
    }

    /// <summary>
    /// Configures notifications.
    /// </summary>
    public StockIndicatorBuilder ConfigureNotifications(Action<NotificationCatalog>? configure = null)
    {
        configure?.Invoke(_notifications);
        return this;
    }

    /// <summary>
    /// Configures auto-trading.
    /// </summary>
    public StockIndicatorBuilder ConfigureAutoTrading(Action<AutoTradingCatalog>? configure = null)
    {
        configure?.Invoke(_autoTrading);
        return this;
    }

    /// <summary>
    /// Configures backtesting options.
    /// </summary>
    public StockIndicatorBuilder ConfigureBacktesting(BacktestOptions? options = null)
    {
        _backtestOptions = options ?? new BacktestOptions();
        return this;
    }

    /// <summary>
    /// Configures benchmarking options.
    /// </summary>
    public StockIndicatorBuilder ConfigureBenchmarking(BenchmarkOptions? options = null)
    {
        _benchmarkOptions = options ?? new BenchmarkOptions();
        return this;
    }

    /// <summary>
    /// Configures behavior options.
    /// </summary>
    public StockIndicatorBuilder ConfigureBehavior(Action<BehaviorOptions> configure)
    {
        configure?.Invoke(_behavior);
        return this;
    }

    /// <summary>
    /// Builds the indicator runtime.
    /// </summary>
    public IndicatorRuntime Build()
    {
        EnsureDefaults();

        var symbols = ResolveSymbols();
        var timeframe = ResolveDefaultTimeframe();
        var activeSeries = ResolveActiveSeries();
        var streamingOptions = ResolveStreamingOptions(symbols, timeframe);

        return new IndicatorRuntime(
            Source,
            new Dictionary<SeriesHandle, SeriesNode>(_nodes),
            new Dictionary<IndicatorKey, SeriesHandle>(_keys),
            _signals.Build(),
            _signals.BuildGroups(),
            _signals.BuildRangeRules(),
            _notifications.Build(),
            _autoTrading.Build(),
            _behavior,
            activeSeries,
            symbols,
            streamingOptions,
            _signalOptions,
            _backtestOptions,
            _benchmarkOptions);
    }

    /// <summary>
    /// Runs a backtest with the configured indicators and signals.
    /// </summary>
    /// <returns>Backtest results with comprehensive metrics.</returns>
    /// <exception cref="InvalidOperationException">If the data source is not batch mode.</exception>
    public BacktestResults Backtest()
    {
        return Backtest(null);
    }

    /// <summary>
    /// Runs a backtest with the configured indicators and signals.
    /// </summary>
    /// <param name="configure">Optional callback to configure backtest options.</param>
    /// <returns>Backtest results with comprehensive metrics.</returns>
    /// <exception cref="InvalidOperationException">If the data source is not batch mode.</exception>
    public BacktestResults Backtest(Action<BacktestOptions>? configure)
    {
        if (Source.Kind != IndicatorSourceKind.Batch)
        {
            throw new InvalidOperationException("Backtesting requires batch data. Use IndicatorDataSource.FromBatch().");
        }

        var data = Source.BatchData ?? throw new InvalidOperationException("Batch data is missing.");

        // Apply additional configuration if provided
        _backtestOptions ??= new BacktestOptions();
        configure?.Invoke(_backtestOptions);

        // Build and start the runtime to compute indicators
        using var runtime = Build();
        runtime.Start();

        // Create and run the backtest engine
        var engine = new BacktestEngine(
            runtime,
            data,
            _backtestOptions,
            _backtestOptions.PositionSizing,
            _backtestOptions.RiskManagement);

        return engine.Run();
    }

    /// <summary>
    /// Whether identical indicator computations share one node. On by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With this on, asking twice for the same indicator with the same parameters on the same input
    /// returns the same handle and the work happens once. Results are unchanged either way - the graph
    /// is a description of what to compute, and computing it twice produces the same numbers.
    /// </para>
    /// <para>
    /// Turn it off to confirm that a difference in output is not coming from node sharing. It should
    /// never be necessary, and if it ever is, that is a defect worth reporting rather than a setting
    /// worth leaving off.
    /// </para>
    /// </remarks>
    public bool EnableCommonSubexpressionElimination { get; set; } = true;

    internal SeriesHandle AddIndicator(IndicatorSpec spec, SeriesHandle input, SeriesKey seriesKey, IndicatorKey? key)
    {
        // Two requests for the same computation on the same input share one node. The builder already
        // does this for price series in GetOrCreateBaseSeries; without it here, asking for an SMA(20)
        // on the close twice built two nodes and computed it twice. Sharing is safe because a node is
        // a description of a computation rather than a result: evaluation is deterministic, and
        // subscribing to the same handle twice is a no-op (IndicatorRuntime.ActivateSeries is a set).
        //
        // A specification that cannot be compared by value yields no key, and then this behaves as it
        // did before. See IndicatorNodeKey for why that direction is the safe one.
        var nodeKey = EnableCommonSubexpressionElimination
            ? IndicatorNodeKey.TryCreate(seriesKey, input, spec)
            : null;

        if (nodeKey is not null && _indicatorNodes.TryGetValue(nodeKey, out var existing))
        {
            if (key.HasValue)
            {
                _keys[key.Value] = existing;
            }

            return existing;
        }

        var handle = NewHandle();
        _nodes[handle] = SeriesNode.Indicator(seriesKey, input, spec);
        if (key.HasValue)
        {
            _keys[key.Value] = handle;
        }

        if (nodeKey is not null)
        {
            _indicatorNodes[nodeKey] = handle;
        }

        return handle;
    }

    internal SeriesHandle AddFormula(SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
    {
        var handle = NewHandle();
        _nodes[handle] = SeriesNode.CreateFormula(ResolveSeriesKey(left), left, right, formula);
        return handle;
    }

    internal SeriesHandle AddMultiStockIndicator(IndicatorSpec spec, SeriesHandle stockInput, SeriesHandle marketInput, SeriesKey seriesKey, IndicatorKey? key)
    {
        var handle = NewHandle();
        _nodes[handle] = SeriesNode.MultiStockIndicator(seriesKey, stockInput, marketInput, spec);
        if (key.HasValue)
        {
            _keys[key.Value] = handle;
        }

        return handle;
    }

    internal SeriesHandle GetHandle(IndicatorKey key)
    {
        if (_keys.TryGetValue(key, out var handle))
        {
            return handle;
        }

        throw new InvalidOperationException($"Indicator key '{key}' has not been configured.");
    }

    internal SeriesHandle GetDefaultSeriesHandle()
    {
        var key = ResolveDefaultSeriesKey();
        return GetOrCreateBaseSeries(key);
    }

    internal SeriesHandle GetOrCreateBaseSeries(SeriesKey key)
    {
        if (_baseSeries.TryGetValue(key, out var handle))
        {
            return handle;
        }

        handle = NewHandle();
        _baseSeries[key] = handle;
        _nodes[handle] = SeriesNode.Base(key);
        return handle;
    }

    internal SeriesKey ResolveDefaultSeriesKey()
    {
        if (_defaultSeriesKey.HasValue)
        {
            return _defaultSeriesKey.Value;
        }

        var symbols = ResolveSymbols();
        if (symbols.Count == 0)
        {
            symbols = SymbolDefaults.AllUs;
        }

        var timeframe = ResolveDefaultTimeframe();
        _defaultSeriesKey = new SeriesKey(symbols[0], timeframe);
        return _defaultSeriesKey.Value;
    }

    internal SeriesKey ResolveSeriesKey(SeriesHandle handle)
    {
        if (_nodes.TryGetValue(handle, out var node))
        {
            return node.SeriesKey;
        }

        return ResolveDefaultSeriesKey();
    }

    private void EnsureDefaults()
    {
        _indicatorOptions ??= new IndicatorOptions();
        _signalOptions ??= new SignalOptions();
        _dataOptions ??= new DataOptions();
        ResolveDefaultSeriesKey();

        ApplyIndicatorDefaults();
    }

    private void ApplyIndicatorDefaults()
    {
        if (_defaultsApplied)
        {
            return;
        }

        // Default to None for optimal performance - only compute explicitly configured indicators
        var selection = _indicatorOptions?.Selection ?? IndicatorSelection.None();
        _indicators.ApplyDefaults(selection);
        _defaultsApplied = true;
    }

    private IReadOnlyList<SymbolId> ResolveSymbols()
    {
        if (_resolvedSymbols != null)
        {
            return _resolvedSymbols;
        }

        var symbols = _symbolOptions?.Symbols;
        if (symbols != null && symbols.Count > 0)
        {
            _resolvedSymbols = symbols;
            return symbols;
        }

        if (Source.ProviderDefaults != null && Source.ProviderDefaults.TryGetDefaultSymbols(out var defaults)
            && defaults.Count > 0)
        {
            _resolvedSymbols = defaults;
            return defaults;
        }

        if (_symbolOptions?.Universe == SymbolUniverse.All)
        {
            _resolvedSymbols = SymbolDefaults.All;
            return _resolvedSymbols;
        }

        _resolvedSymbols = SymbolDefaults.AllUs;
        return _resolvedSymbols;
    }

    private BarTimeframe ResolveDefaultTimeframe()
    {
        if (_resolvedTimeframe != null)
        {
            return _resolvedTimeframe;
        }

        if (_dataOptions?.Timeframe != null)
        {
            _resolvedTimeframe = _dataOptions.Timeframe;
            return _resolvedTimeframe;
        }

        if (Source.ProviderDefaults != null && Source.ProviderDefaults.TryGetDefaultTimeframe(out var providerTimeframe))
        {
            _resolvedTimeframe = providerTimeframe;
            return providerTimeframe;
        }

        _resolvedTimeframe = BarTimeframe.Tick;
        return _resolvedTimeframe;
    }

    private IReadOnlyCollection<SeriesHandle> ResolveActiveSeries()
    {
        if (_indicatorOptions?.ComputePolicy == IndicatorComputePolicy.Eager)
        {
            return _nodes.Keys;
        }

        var active = new HashSet<SeriesHandle>();
        var signalRules = _signals.Build();
        for (var i = 0; i < signalRules.Count; i++)
        {
            var series = signalRules[i].Series;
            if (series.Handle.HasValue)
            {
                active.Add(series.Handle.Value);
                AddDependencies(series.Handle.Value, active);
            }
            else if (series.Key.HasValue && _keys.TryGetValue(series.Key.Value, out var handle))
            {
                active.Add(handle);
                AddDependencies(handle, active);
            }
        }

        var groupRules = _signals.BuildGroups();
        for (var i = 0; i < groupRules.Count; i++)
        {
            var conditions = groupRules[i].Conditions;
            for (var j = 0; j < conditions.Length; j++)
            {
                active.Add(conditions[j].Series);
                AddDependencies(conditions[j].Series, active);
            }
        }

        if (active.Count == 0)
        {
            return _nodes.Keys;
        }

        return active;
    }

    private void AddDependencies(SeriesHandle handle, HashSet<SeriesHandle> active)
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
                    active.Add(node.Input.Value);
                    AddDependencies(node.Input.Value, active);
                }
                break;
            case SeriesNodeKind.Formula:
                if (node.Left.HasValue)
                {
                    active.Add(node.Left.Value);
                    AddDependencies(node.Left.Value, active);
                }

                if (node.Right.HasValue)
                {
                    active.Add(node.Right.Value);
                    AddDependencies(node.Right.Value, active);
                }
                break;
        }
    }

    private StreamingOptions? ResolveStreamingOptions(IReadOnlyList<SymbolId> symbols, BarTimeframe timeframe)
    {
        if (Source.Kind != IndicatorSourceKind.Streaming)
        {
            return null;
        }

        if (_dataOptions?.StreamingOptions != null)
        {
            // Clone to avoid mutating caller's options object
            var custom = CloneStreamingOptions(_dataOptions.StreamingOptions);
            custom.Symbols ??= ToSymbolStrings(symbols);
            return custom;
        }

        if (Source.ProviderDefaults != null)
        {
            var providerOptions = Source.ProviderDefaults.CreateStreamingOptions(symbols, timeframe);
            if (providerOptions != null)
            {
                providerOptions.Symbols ??= ToSymbolStrings(symbols);
                return providerOptions;
            }
        }

        return CreateDefaultStreamingOptions(symbols);
    }

    private static StreamingOptions CloneStreamingOptions(StreamingOptions source)
    {
        return new StreamingOptions
        {
            Symbols = source.Symbols,
            Timeframes = source.Timeframes,
            SubscribeTrades = source.SubscribeTrades,
            SubscribeQuotes = source.SubscribeQuotes,
            SubscribeBars = source.SubscribeBars,
            UpdatePolicy = source.UpdatePolicy,
            QuotePriceMode = source.QuotePriceMode,
            OutOfOrderPolicy = source.OutOfOrderPolicy,
            ReorderWindow = source.ReorderWindow,
            MaxBufferSize = source.MaxBufferSize,
            ProcessingMode = source.ProcessingMode,
            BackpressurePolicy = source.BackpressurePolicy,
            MaxPendingMessages = source.MaxPendingMessages,
            IncludeOutputValues = source.IncludeOutputValues,
            IndicatorOptions = source.IndicatorOptions,
            Indicators = source.Indicators
        };
    }

    private static StreamingOptions CreateDefaultStreamingOptions(IReadOnlyList<SymbolId> symbols)
    {
        return new StreamingOptions
        {
            Symbols = ToSymbolStrings(symbols),
            SubscribeTrades = true,
            SubscribeQuotes = false,
            SubscribeBars = false,
            UpdatePolicy = StreamingUpdatePolicy.FinalOnly,
            ProcessingMode = StreamingProcessingMode.Inline,
            IncludeOutputValues = true
        };
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

    private SeriesHandle NewHandle()
    {
        var handle = new SeriesHandle(_nextId);
        _nextId++;
        return handle;
    }
}
