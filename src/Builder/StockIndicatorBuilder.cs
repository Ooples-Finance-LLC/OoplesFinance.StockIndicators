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
    private readonly IndicatorDataSource _source;
    private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
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
    public StockIndicatorBuilder(IndicatorDataSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _nodes = new Dictionary<SeriesHandle, SeriesNode>();
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
    internal IndicatorDataSource PrimarySource => _source;

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
    /// Configures indicators.
    /// </summary>
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
    public StockIndicatorBuilder ConfigureIndicators(Action<IndicatorCatalog> configure)
    {
        return ConfigureIndicators(null, configure);
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
            _source,
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
        if (_source.Kind != IndicatorSourceKind.Batch)
        {
            throw new InvalidOperationException("Backtesting requires batch data. Use IndicatorDataSource.FromBatch().");
        }

        var data = _source.BatchData ?? throw new InvalidOperationException("Batch data is missing.");

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

    internal SeriesHandle AddIndicator(IndicatorSpec spec, SeriesHandle input, SeriesKey seriesKey, IndicatorKey? key)
    {
        var handle = NewHandle();
        _nodes[handle] = SeriesNode.Indicator(seriesKey, input, spec);
        if (key.HasValue)
        {
            _keys[key.Value] = handle;
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

        if (_source.ProviderDefaults != null && _source.ProviderDefaults.TryGetDefaultSymbols(out var defaults)
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

        if (_source.ProviderDefaults != null && _source.ProviderDefaults.TryGetDefaultTimeframe(out var providerTimeframe))
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
        if (_source.Kind != IndicatorSourceKind.Streaming)
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

        if (_source.ProviderDefaults != null)
        {
            var providerOptions = _source.ProviderDefaults.CreateStreamingOptions(symbols, timeframe);
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
            InputName = source.InputName,
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
