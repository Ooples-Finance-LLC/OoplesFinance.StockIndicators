using System;
using System.Collections.Generic;
using System.Globalization;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

internal static class FacadeSketchesV6
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly SymbolId PrimarySymbol = SymbolId.Aapl;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("Facade/builder sketches (v6):");
        RunOptionA();
        RunOptionB();
        RunOptionC();
    }

    private static void RunOptionA()
    {
        Console.WriteLine();
        Console.WriteLine("Option A: Default facade (batch + typed handles)");

        var data = BuildSampleData(160, 101d, new DateTime(2024, 10, 2, 9, 30, 0, DateTimeKind.Utc));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle sma = default;
        BollingerBandsSeries bands = default;
        SignalHandle overBand = default;

        var builder = new StockIndicatorBuilder(source)
            .ConfigureIndicators(configure: indicators =>
            {
                var price = indicators.Price();
                sma = indicators.Sma(20, price);
                bands = indicators.BollingerBands(20, 2, price);
            })
            .ConfigureSignals(configure: signals =>
            {
                overBand = signals.When(bands.Upper).CrossesAbove(110d).Emit();
            })
            .ConfigureNotifications(notify => notify.Console())
            .ConfigureAutoTrading(trade =>
            {
                trade.ConsoleAdapter()
                    .OnSignal(overBand)
                    .MarketBuy();
            });

        var runtime = builder.Build();
        runtime.Subscribe(sma, bands.Upper, bands.Middle, bands.Lower);
        runtime.Start();

        if (runtime.Latest != null)
        {
            PrintLast(runtime.Latest, sma, "SMA(20)");
            PrintLast(runtime.Latest, bands.Upper, "BB Upper");
            PrintLast(runtime.Latest, bands.Middle, "BB Middle");
            PrintLast(runtime.Latest, bands.Lower, "BB Lower");
        }
    }

    private static void RunOptionB()
    {
        Console.WriteLine();
        Console.WriteLine("Option B: Configure-first (streaming defaults)");

        var trades = BuildTradeEvents(PrimarySymbol, 12);
        var stream = new ReplayStreamSource(trades);
        var source = IndicatorDataSource.FromStreaming(stream, new DemoProviderDefaults());

        SeriesHandle fast = default;
        SeriesHandle slow = default;
        SignalHandle momentum = default;

        var builder = new StockIndicatorBuilder(source)
            .ConfigureSymbols()
            .ConfigureIndicators(new IndicatorOptions
            {
                ComputePolicy = IndicatorComputePolicy.Lazy,
                Selection = IndicatorSelection.Core()
            }, indicators =>
            {
                var price = indicators.Price();
                fast = indicators.Sma(10, price);
                slow = indicators.Sma(30, price);
            })
            .ConfigureSignals(configure: signals =>
            {
                momentum = signals.Group(
                        SignalCondition.Above(fast, 101d),
                        SignalCondition.Above(slow, 100d))
                    .All()
                    .ForBars(3)
                    .Emit();
            })
            .ConfigureNotifications(notify => notify.Console())
            .ConfigureAutoTrading(trade =>
            {
                trade.ConsoleAdapter()
                    .OnSignal(momentum)
                    .MarketBuy();
            })
            .ConfigureBacktesting(new BacktestOptions
            {
                InitialCapital = 100_000d
            })
            .ConfigureBenchmarking(new BenchmarkOptions
            {
                Benchmark = BenchmarkKind.Spy
            });

        var runtime = builder.Build();
        runtime.Subscribe(fast, slow);
        runtime.Start();

        if (runtime.Latest != null)
        {
            PrintLast(runtime.Latest, fast, "SMA(10)");
            PrintLast(runtime.Latest, slow, "SMA(30)");
        }
    }

    private static void RunOptionC()
    {
        Console.WriteLine();
        Console.WriteLine("Option C: Cross-series signals (batch or streaming)");

        var data = BuildSampleData(200, 95d, new DateTime(2024, 11, 2, 9, 30, 0, DateTimeKind.Utc));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle aaplSma50 = default;
        SeriesHandle googSma20 = default;
        SeriesHandle spread = default;

        var builder = new StockIndicatorBuilder(source)
            .ConfigureSymbols(new SymbolOptions
            {
                Symbols = new[] { PrimarySymbol, SymbolId.Goog }
            })
            .ConfigureIndicators(configure: indicators =>
            {
                var aapl = indicators.For(PrimarySymbol, BarTimeframe.Minutes(1));
                var goog = indicators.For(SymbolId.Goog, BarTimeframe.Minutes(1));

                aaplSma50 = aapl.Sma(50);
                googSma20 = goog.Sma(20);
                spread = indicators.Formula(googSma20, aaplSma50, FormulaOp.Subtract);
            })
            .ConfigureSignals(configure: signals =>
            {
                signals.Group(
                        SignalCondition.Above(googSma20, 105d),
                        SignalCondition.Below(aaplSma50, 100d))
                    .All()
                    .For(SignalWindow.Minutes(5))
                    .Emit();
            })
            .ConfigureNotifications(notify => notify.Console());

        var runtime = builder.Build();
        runtime.Subscribe(aaplSma50, googSma20, spread);
        runtime.Start();

        if (runtime.Latest != null)
        {
            PrintLast(runtime.Latest, aaplSma50, "AAPL SMA(50)");
            PrintLast(runtime.Latest, googSma20, "GOOG SMA(20)");
            PrintLast(runtime.Latest, spread, "SMA spread");
        }
    }

    private static void PrintLast(IndicatorSnapshot snapshot, SeriesHandle handle, string label)
    {
        if (!snapshot.TryGetSeries(handle, out var values))
        {
            Console.WriteLine($"{label} last = n/a");
            return;
        }

        Console.WriteLine($"{label} last = {FormatValue(LastValue(values))}");
    }

    private static void PrintLast(IndicatorSnapshot snapshot, IndicatorKey key, string label)
    {
        if (!snapshot.TryGetSeries(key, out var values))
        {
            Console.WriteLine($"{label} last = n/a");
            return;
        }

        Console.WriteLine($"{label} last = {FormatValue(LastValue(values))}");
    }

    private static double LastValue(ReadOnlyMemory<double> values)
    {
        return values.Length == 0 ? double.NaN : values.Span[values.Length - 1];
    }

    private static string FormatValue(double value)
    {
        return double.IsNaN(value) ? "NaN" : value.ToString("F4", Invariant);
    }

    private static List<TickerData> BuildSampleData(int count, double startPrice, DateTime start)
    {
        var data = new List<TickerData>(count);
        var random = new Random(512);
        var price = startPrice;
        var timestamp = start;

        for (var i = 0; i < count; i++)
        {
            var change = (random.NextDouble() - 0.5d) * 2d;
            var open = price;
            var close = Math.Max(1d, price + change);
            var high = Math.Max(open, close) + random.NextDouble();
            var low = Math.Min(open, close) - random.NextDouble();
            if (low < 0.1d)
            {
                low = 0.1d;
            }

            var volume = 960d + (random.NextDouble() * 140d);
            data.Add(new TickerData
            {
                Date = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });

            price = close;
            timestamp = timestamp.AddMinutes(1);
        }

        return data;
    }

    private static List<StreamTrade> BuildTradeEvents(SymbolId symbol, int count)
    {
        var trades = new List<StreamTrade>(count);
        var start = new DateTime(2024, 12, 1, 9, 30, 0, DateTimeKind.Utc);
        var price = 100d;
        for (var i = 0; i < count; i++)
        {
            price += i % 2 == 0 ? 0.6 : -0.2;
            trades.Add(new StreamTrade(symbol.Value, start.AddSeconds(i), price, 1));
        }

        return trades;
    }
    private sealed class IndicatorDataSource
    {
        private IndicatorDataSource(IndicatorSourceKind kind, StockData? batchData, IStreamSource? streamSource,
            IDataProviderDefaults? providerDefaults)
        {
            Kind = kind;
            BatchData = batchData;
            StreamSource = streamSource;
            ProviderDefaults = providerDefaults;
        }

        public IndicatorSourceKind Kind { get; }
        public StockData? BatchData { get; }
        public IStreamSource? StreamSource { get; }
        public IDataProviderDefaults? ProviderDefaults { get; }

        public static IndicatorDataSource FromBatch(StockData data, IDataProviderDefaults? defaults = null)
        {
            return new IndicatorDataSource(IndicatorSourceKind.Batch, data, null, defaults);
        }

        public static IndicatorDataSource FromStreaming(IStreamSource source, IDataProviderDefaults? defaults = null)
        {
            return new IndicatorDataSource(IndicatorSourceKind.Streaming, null, source, defaults);
        }
    }

    private enum IndicatorSourceKind
    {
        Batch,
        Streaming
    }

    private interface IDataProviderDefaults
    {
        bool TryGetDefaultSymbols(out IReadOnlyList<SymbolId> symbols);
        bool TryGetDefaultTimeframe(out BarTimeframe timeframe);
        StreamingOptions? CreateStreamingOptions(IReadOnlyList<SymbolId> symbols, BarTimeframe timeframe);
    }

    private sealed class DemoProviderDefaults : IDataProviderDefaults
    {
        public bool TryGetDefaultSymbols(out IReadOnlyList<SymbolId> symbols)
        {
            symbols = new[] { SymbolId.Aapl };
            return true;
        }

        public bool TryGetDefaultTimeframe(out BarTimeframe timeframe)
        {
            timeframe = BarTimeframe.Tick;
            return true;
        }

        public StreamingOptions? CreateStreamingOptions(IReadOnlyList<SymbolId> symbols, BarTimeframe timeframe)
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
    }

    private sealed class DataOptions
    {
        public BarTimeframe? Timeframe { get; set; }
        public StreamingOptions? StreamingOptions { get; set; }
    }

    private sealed class SymbolOptions
    {
        public IReadOnlyList<SymbolId>? Symbols { get; set; }
        public SymbolUniverse? Universe { get; set; }

        public static SymbolOptions AllUs()
        {
            return new SymbolOptions { Universe = SymbolUniverse.AllUs };
        }
    }

    private enum SymbolUniverse
    {
        ProviderDefault,
        AllUs,
        All
    }

    private static class SymbolDefaults
    {
        private static readonly SymbolId[] AllUsSymbols = { SymbolId.Aapl, SymbolId.Msft, SymbolId.Goog };

        public static IReadOnlyList<SymbolId> AllUs => AllUsSymbols;
        public static IReadOnlyList<SymbolId> All => AllUsSymbols;
    }

    private readonly struct SymbolId : IEquatable<SymbolId>
    {
        public SymbolId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public static SymbolId From(string value)
        {
            return new SymbolId(value);
        }

        public static SymbolId Aapl => new SymbolId("AAPL");
        public static SymbolId Msft => new SymbolId("MSFT");
        public static SymbolId Goog => new SymbolId("GOOG");

        public bool Equals(SymbolId other)
        {
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is SymbolId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    private sealed class IndicatorOptions
    {
        public IndicatorComputePolicy ComputePolicy { get; set; } = IndicatorComputePolicy.Lazy;
        public IndicatorSelection? Selection { get; set; }
    }

    private enum IndicatorComputePolicy
    {
        Lazy,
        Eager
    }

    private sealed class IndicatorSelection
    {
        private IndicatorSelection(IndicatorPreset preset, IReadOnlyList<IndicatorName>? include)
        {
            Preset = preset;
            Include = include;
        }

        public IndicatorPreset Preset { get; }
        public IReadOnlyList<IndicatorName>? Include { get; }

        public static IndicatorSelection All()
        {
            return new IndicatorSelection(IndicatorPreset.All, null);
        }

        public static IndicatorSelection Core()
        {
            return new IndicatorSelection(IndicatorPreset.Core, null);
        }

        public static IndicatorSelection Only(params IndicatorName[] names)
        {
            return new IndicatorSelection(IndicatorPreset.Only, names);
        }
    }

    private enum IndicatorPreset
    {
        All,
        Core,
        Only
    }

    private sealed class SignalOptions
    {
        public SignalWindow? DefaultWindow { get; set; }
    }

    private readonly struct SignalWindow
    {
        public SignalWindow(int bars, TimeSpan? duration)
        {
            Bars = Math.Max(1, bars);
            Duration = duration;
        }

        public int Bars { get; }
        public TimeSpan? Duration { get; }

        public static SignalWindow FromBars(int bars)
        {
            return new SignalWindow(bars, null);
        }

        public static SignalWindow Minutes(int minutes)
        {
            return new SignalWindow(1, TimeSpan.FromMinutes(minutes));
        }
    }

    private sealed class BacktestOptions
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double? InitialCapital { get; set; }
        public FeeModel? FeeModel { get; set; }
        public SlippageModel? SlippageModel { get; set; }
    }

    private enum FeeModel
    {
        None,
        Fixed,
        Percent
    }

    private enum SlippageModel
    {
        None,
        FixedTicks,
        Percent
    }

    private sealed class BenchmarkOptions
    {
        public BenchmarkKind? Benchmark { get; set; }
        public SymbolId? CustomSymbol { get; set; }
    }

    private enum BenchmarkKind
    {
        Spy,
        Qqq,
        Custom
    }

    private sealed class BehaviorOptions
    {
        public bool EmitWarmup { get; set; } = true;
    }
    private sealed class StockIndicatorBuilder
    {
        private readonly IndicatorDataSource _source;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
        private readonly Dictionary<SeriesKey, SeriesHandle> _baseSeries;
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

        public StockIndicatorBuilder(IndicatorDataSource source)
        {
            _source = source;
            _nodes = new Dictionary<SeriesHandle, SeriesNode>();
            _keys = new Dictionary<IndicatorKey, SeriesHandle>();
            _baseSeries = new Dictionary<SeriesKey, SeriesHandle>();
            _behavior = new BehaviorOptions();
            _signals = new SignalCatalog();
            _notifications = new NotificationCatalog();
            _autoTrading = new AutoTradingCatalog();
            _indicators = new IndicatorCatalog(this);
            _nextId = 1;
        }

        public StockIndicatorBuilder ConfigureSymbols(SymbolOptions? options = null)
        {
            _symbolOptions = options;
            _resolvedSymbols = null;
            return this;
        }

        public StockIndicatorBuilder ConfigureData(DataOptions? options = null)
        {
            _dataOptions = options;
            _resolvedTimeframe = null;
            return this;
        }

        public StockIndicatorBuilder ConfigureIndicators(IndicatorOptions? options = null, Action<IndicatorCatalog>? configure = null)
        {
            _indicatorOptions = options ?? new IndicatorOptions();
            ApplyIndicatorDefaults();
            configure?.Invoke(_indicators);
            return this;
        }

        public StockIndicatorBuilder ConfigureSignals(SignalOptions? options = null, Action<SignalCatalog>? configure = null)
        {
            _signalOptions = options ?? new SignalOptions();
            configure?.Invoke(_signals);
            return this;
        }

        public StockIndicatorBuilder ConfigureNotifications(Action<NotificationCatalog>? configure = null)
        {
            configure?.Invoke(_notifications);
            return this;
        }

        public StockIndicatorBuilder ConfigureAutoTrading(Action<AutoTradingCatalog>? configure = null)
        {
            configure?.Invoke(_autoTrading);
            return this;
        }

        public StockIndicatorBuilder ConfigureBacktesting(BacktestOptions? options = null)
        {
            _backtestOptions = options ?? new BacktestOptions();
            return this;
        }

        public StockIndicatorBuilder ConfigureBenchmarking(BenchmarkOptions? options = null)
        {
            _benchmarkOptions = options ?? new BenchmarkOptions();
            return this;
        }

        public StockIndicatorBuilder ConfigureBehavior(Action<BehaviorOptions> configure)
        {
            configure?.Invoke(_behavior);
            return this;
        }

        public IndicatorRuntime Build()
        {
            EnsureDefaults();

            var symbols = ResolveSymbols();
            var timeframe = ResolveDefaultTimeframe();
            var activeSeries = ResolveActiveSeries();
            var streamingOptions = ResolveStreamingOptions(symbols, timeframe);

            return new IndicatorRuntime(_source, new Dictionary<SeriesHandle, SeriesNode>(_nodes),
                new Dictionary<IndicatorKey, SeriesHandle>(_keys),
                _signals.Build(),
                _signals.BuildGroups(),
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

            var selection = _indicatorOptions?.Selection ?? IndicatorSelection.All();
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
                var custom = _dataOptions.StreamingOptions;
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

        private SeriesHandle NewHandle()
        {
            var handle = new SeriesHandle(_nextId);
            _nextId++;
            return handle;
        }
    }

    private sealed class IndicatorCatalog
    {
        private readonly StockIndicatorBuilder _builder;

        public IndicatorCatalog(StockIndicatorBuilder builder)
        {
            _builder = builder;
        }

        public SeriesHandle Price()
        {
            return _builder.GetDefaultSeriesHandle();
        }

        public IndicatorChain For(SymbolId symbol, BarTimeframe timeframe)
        {
            var key = new SeriesKey(symbol, timeframe);
            var baseSeries = _builder.GetOrCreateBaseSeries(key);
            return new IndicatorChain(this, baseSeries);
        }

        public IndicatorChain For(SymbolId symbol)
        {
            var defaultKey = _builder.ResolveDefaultSeriesKey();
            return For(symbol, defaultKey.Timeframe);
        }

        public IndicatorChain Then(SeriesHandle input)
        {
            return new IndicatorChain(this, input);
        }

        public SeriesHandle Sma(int length, SeriesHandle? input = null, IndicatorKey? key = null)
        {
            var series = input ?? Price();
            return _builder.AddIndicator(IndicatorSpecs.Sma(length), series, _builder.ResolveSeriesKey(series), key);
        }

        public SeriesHandle Rsi(int length, SeriesHandle? input = null, IndicatorKey? key = null)
        {
            var series = input ?? Price();
            return _builder.AddIndicator(IndicatorSpecs.Rsi(length), series, _builder.ResolveSeriesKey(series), key);
        }

        public MacdSeries Macd(int fastLength = 12, int slowLength = 26, int signalLength = 9,
            SeriesHandle? input = null, IndicatorKey? primaryKey = null, IndicatorKey? signalKey = null, IndicatorKey? histogramKey = null)
        {
            var series = input ?? Price();
            var seriesKey = _builder.ResolveSeriesKey(series);
            var primary = _builder.AddIndicator(IndicatorSpecs.Macd(fastLength, slowLength, signalLength, IndicatorOutput.Primary),
                series, seriesKey, primaryKey);
            var signal = _builder.AddIndicator(IndicatorSpecs.Macd(fastLength, slowLength, signalLength, IndicatorOutput.Signal),
                series, seriesKey, signalKey);
            var histogram = _builder.AddIndicator(IndicatorSpecs.Macd(fastLength, slowLength, signalLength, IndicatorOutput.Histogram),
                series, seriesKey, histogramKey);

            return new MacdSeries(primary, signal, histogram);
        }

        public BollingerBandsSeries BollingerBands(int length = 20, double stdDevMult = 2, SeriesHandle? input = null,
            IndicatorKey? upperKey = null, IndicatorKey? middleKey = null, IndicatorKey? lowerKey = null)
        {
            var series = input ?? Price();
            var seriesKey = _builder.ResolveSeriesKey(series);
            var upper = _builder.AddIndicator(IndicatorSpecs.BollingerBands(length, stdDevMult, IndicatorOutput.UpperBand),
                series, seriesKey, upperKey);
            var middle = _builder.AddIndicator(IndicatorSpecs.BollingerBands(length, stdDevMult, IndicatorOutput.MiddleBand),
                series, seriesKey, middleKey);
            var lower = _builder.AddIndicator(IndicatorSpecs.BollingerBands(length, stdDevMult, IndicatorOutput.LowerBand),
                series, seriesKey, lowerKey);

            return new BollingerBandsSeries(upper, middle, lower);
        }

        public SeriesHandle Formula(SeriesHandle left, SeriesHandle right, FormulaOp op)
        {
            return _builder.AddFormula(left, right, op switch
            {
                FormulaOp.Add => (x, y) => x + y,
                FormulaOp.Subtract => (x, y) => x - y,
                FormulaOp.Multiply => (x, y) => x * y,
                FormulaOp.Divide => (x, y) => x / y,
                _ => (x, y) => double.NaN
            });
        }

        public SeriesHandle Formula(SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
        {
            return _builder.AddFormula(left, right, formula);
        }

        public void ApplyDefaults(IndicatorSelection selection)
        {
            if (selection == null)
            {
                return;
            }

            switch (selection.Preset)
            {
                case IndicatorPreset.All:
                case IndicatorPreset.Core:
                    var price = Price();
                    Sma(20, price, IndicatorKey.Sma);
                    Rsi(14, price, IndicatorKey.Rsi);
                    Macd(12, 26, 9, price, IndicatorKey.Macd, IndicatorKey.MacdSignal, IndicatorKey.MacdHistogram);
                    BollingerBands(20, 2, price, IndicatorKey.BollingerUpper, IndicatorKey.BollingerMiddle, IndicatorKey.BollingerLower);
                    break;
                case IndicatorPreset.Only:
                    if (selection.Include == null)
                    {
                        return;
                    }

                    var baseSeries = Price();
                    for (var i = 0; i < selection.Include.Count; i++)
                    {
                        switch (selection.Include[i])
                        {
                            case IndicatorName.SimpleMovingAverage:
                                Sma(20, baseSeries, IndicatorKey.Sma);
                                break;
                            case IndicatorName.RelativeStrengthIndex:
                                Rsi(14, baseSeries, IndicatorKey.Rsi);
                                break;
                            case IndicatorName.MovingAverageConvergenceDivergence:
                                Macd(12, 26, 9, baseSeries, IndicatorKey.Macd, IndicatorKey.MacdSignal, IndicatorKey.MacdHistogram);
                                break;
                            case IndicatorName.BollingerBands:
                                BollingerBands(20, 2, baseSeries, IndicatorKey.BollingerUpper, IndicatorKey.BollingerMiddle,
                                    IndicatorKey.BollingerLower);
                                break;
                        }
                    }

                    break;
            }
        }
    }

    private readonly struct IndicatorChain
    {
        private readonly IndicatorCatalog _catalog;
        private readonly SeriesHandle _input;

        public IndicatorChain(IndicatorCatalog catalog, SeriesHandle input)
        {
            _catalog = catalog;
            _input = input;
        }

        public SeriesHandle Input => _input;

        public SeriesHandle Sma(int length, IndicatorKey? key = null)
        {
            return _catalog.Sma(length, _input, key);
        }

        public SeriesHandle Rsi(int length, IndicatorKey? key = null)
        {
            return _catalog.Rsi(length, _input, key);
        }

        public MacdSeries Macd(int fastLength = 12, int slowLength = 26, int signalLength = 9,
            IndicatorKey? primaryKey = null, IndicatorKey? signalKey = null, IndicatorKey? histogramKey = null)
        {
            return _catalog.Macd(fastLength, slowLength, signalLength, _input, primaryKey, signalKey, histogramKey);
        }

        public BollingerBandsSeries BollingerBands(int length = 20, double stdDevMult = 2,
            IndicatorKey? upperKey = null, IndicatorKey? middleKey = null, IndicatorKey? lowerKey = null)
        {
            return _catalog.BollingerBands(length, stdDevMult, _input, upperKey, middleKey, lowerKey);
        }
    }

    private readonly struct MacdSeries
    {
        public MacdSeries(SeriesHandle primary, SeriesHandle signal, SeriesHandle histogram)
        {
            Primary = primary;
            Signal = signal;
            Histogram = histogram;
        }

        public SeriesHandle Primary { get; }
        public SeriesHandle Signal { get; }
        public SeriesHandle Histogram { get; }
    }

    private readonly struct BollingerBandsSeries
    {
        public BollingerBandsSeries(SeriesHandle upper, SeriesHandle middle, SeriesHandle lower)
        {
            Upper = upper;
            Middle = middle;
            Lower = lower;
        }

        public SeriesHandle Upper { get; }
        public SeriesHandle Middle { get; }
        public SeriesHandle Lower { get; }
    }
    private sealed class SignalCatalog
    {
        private readonly List<SignalRule> _rules = new();
        private readonly List<SignalGroupRule> _groupRules = new();
        private int _nextId;

        public SignalRuleBuilder When(SeriesHandle handle)
        {
            return new SignalRuleBuilder(this, SignalSeries.FromHandle(handle));
        }

        public SignalRuleBuilder When(IndicatorKey key)
        {
            return new SignalRuleBuilder(this, SignalSeries.FromKey(key));
        }

        public SignalGroupRuleBuilder Group(params SignalCondition[] conditions)
        {
            return new SignalGroupRuleBuilder(this, conditions ?? Array.Empty<SignalCondition>());
        }

        internal SignalHandle AddRule(SignalRule rule)
        {
            _rules.Add(rule);
            return rule.Handle;
        }

        internal SignalHandle AddGroupRule(SignalGroupRule rule)
        {
            _groupRules.Add(rule);
            return rule.Handle;
        }

        internal SignalHandle NextHandle()
        {
            _nextId++;
            return new SignalHandle(_nextId);
        }

        public IReadOnlyList<SignalRule> Build()
        {
            return new List<SignalRule>(_rules);
        }

        public IReadOnlyList<SignalGroupRule> BuildGroups()
        {
            return new List<SignalGroupRule>(_groupRules);
        }
    }

    private readonly struct SignalRuleBuilder
    {
        private readonly SignalCatalog _catalog;
        private readonly SignalSeries _series;

        public SignalRuleBuilder(SignalCatalog catalog, SignalSeries series)
        {
            _catalog = catalog;
            _series = series;
        }

        public SignalEmissionBuilder Above(double threshold)
        {
            return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.Above, threshold);
        }

        public SignalEmissionBuilder Below(double threshold)
        {
            return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.Below, threshold);
        }

        public SignalEmissionBuilder CrossesAbove(double threshold)
        {
            return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.CrossesAbove, threshold);
        }

        public SignalEmissionBuilder CrossesBelow(double threshold)
        {
            return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.CrossesBelow, threshold);
        }
    }

    private readonly struct SignalEmissionBuilder
    {
        private readonly SignalCatalog _catalog;
        private readonly SignalSeries _series;
        private readonly SignalTrigger _trigger;
        private readonly double _threshold;

        public SignalEmissionBuilder(SignalCatalog catalog, SignalSeries series, SignalTrigger trigger, double threshold)
        {
            _catalog = catalog;
            _series = series;
            _trigger = trigger;
            _threshold = threshold;
        }

        public SignalHandle Emit(string? name = null)
        {
            var handle = _catalog.NextHandle();
            _catalog.AddRule(new SignalRule(handle, name ?? handle.ToString(), _series, _trigger, _threshold));
            return handle;
        }
    }

    private sealed class SignalRule
    {
        public SignalRule(SignalHandle handle, string name, SignalSeries series, SignalTrigger trigger, double threshold)
        {
            Handle = handle;
            Name = name;
            Series = series;
            Trigger = trigger;
            Threshold = threshold;
        }

        public SignalHandle Handle { get; }
        public string Name { get; }
        public SignalSeries Series { get; }
        public SignalTrigger Trigger { get; }
        public double Threshold { get; }

        public bool IsCross => Trigger == SignalTrigger.CrossesAbove || Trigger == SignalTrigger.CrossesBelow;

        public bool IsActive(double value)
        {
            return Trigger switch
            {
                SignalTrigger.Above => value >= Threshold,
                SignalTrigger.Below => value <= Threshold,
                _ => false
            };
        }

        public bool IsCrossTriggered(double value, double? previous)
        {
            if (!previous.HasValue || double.IsNaN(previous.Value))
            {
                return false;
            }

            return Trigger switch
            {
                SignalTrigger.CrossesAbove => previous.Value < Threshold && value >= Threshold,
                SignalTrigger.CrossesBelow => previous.Value > Threshold && value <= Threshold,
                _ => false
            };
        }
    }

    private readonly struct SignalSeries
    {
        private SignalSeries(IndicatorKey? key, SeriesHandle? handle)
        {
            Key = key;
            Handle = handle;
        }

        public IndicatorKey? Key { get; }
        public SeriesHandle? Handle { get; }

        public static SignalSeries FromKey(IndicatorKey key)
        {
            return new SignalSeries(key, null);
        }

        public static SignalSeries FromHandle(SeriesHandle handle)
        {
            return new SignalSeries(null, handle);
        }

        public bool TryResolve(IndicatorSnapshot snapshot, out ReadOnlyMemory<double> values)
        {
            if (Key.HasValue)
            {
                return snapshot.TryGetSeries(Key.Value, out values);
            }

            if (Handle.HasValue)
            {
                return snapshot.TryGetSeries(Handle.Value, out values);
            }

            values = ReadOnlyMemory<double>.Empty;
            return false;
        }
    }

    private enum SignalTrigger
    {
        Above,
        Below,
        CrossesAbove,
        CrossesBelow
    }

    private readonly struct SignalCondition
    {
        public SignalCondition(SeriesHandle series, SignalTrigger trigger, double threshold)
        {
            Series = series;
            Trigger = trigger;
            Threshold = threshold;
        }

        public SeriesHandle Series { get; }
        public SignalTrigger Trigger { get; }
        public double Threshold { get; }

        public bool IsCross => Trigger == SignalTrigger.CrossesAbove || Trigger == SignalTrigger.CrossesBelow;

        public static SignalCondition Above(SeriesHandle series, double threshold)
        {
            return new SignalCondition(series, SignalTrigger.Above, threshold);
        }

        public static SignalCondition Below(SeriesHandle series, double threshold)
        {
            return new SignalCondition(series, SignalTrigger.Below, threshold);
        }

        public static SignalCondition CrossesAbove(SeriesHandle series, double threshold)
        {
            return new SignalCondition(series, SignalTrigger.CrossesAbove, threshold);
        }

        public static SignalCondition CrossesBelow(SeriesHandle series, double threshold)
        {
            return new SignalCondition(series, SignalTrigger.CrossesBelow, threshold);
        }

        public bool IsActive(double value)
        {
            return Trigger switch
            {
                SignalTrigger.Above => value >= Threshold,
                SignalTrigger.Below => value <= Threshold,
                _ => false
            };
        }

        public bool IsCrossTriggered(double value, double? previous)
        {
            if (!previous.HasValue || double.IsNaN(previous.Value))
            {
                return false;
            }

            return Trigger switch
            {
                SignalTrigger.CrossesAbove => previous.Value < Threshold && value >= Threshold,
                SignalTrigger.CrossesBelow => previous.Value > Threshold && value <= Threshold,
                _ => false
            };
        }
    }

    private readonly struct SignalGroupRuleBuilder
    {
        private readonly SignalCatalog _catalog;
        private readonly SignalCondition[] _conditions;

        public SignalGroupRuleBuilder(SignalCatalog catalog, SignalCondition[] conditions)
        {
            _catalog = catalog;
            _conditions = conditions;
        }

        public SignalGroupAggregationBuilder All()
        {
            return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.All, null, null);
        }

        public SignalGroupAggregationBuilder Any()
        {
            return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.Any, null, null);
        }

        public SignalGroupAggregationBuilder AtLeast(int count)
        {
            return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.AtLeast, count, null);
        }

        public SignalGroupAggregationBuilder Percent(double percent)
        {
            return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.Percent, null, percent);
        }
    }

    private readonly struct SignalGroupAggregationBuilder
    {
        private readonly SignalCatalog _catalog;
        private readonly SignalCondition[] _conditions;
        private readonly SignalGroupMode _mode;
        private readonly int? _requiredCount;
        private readonly double? _requiredPercent;
        private readonly SignalWindow _window;

        public SignalGroupAggregationBuilder(SignalCatalog catalog, SignalCondition[] conditions, SignalGroupMode mode,
            int? requiredCount, double? requiredPercent, SignalWindow? window = null)
        {
            _catalog = catalog;
            _conditions = conditions;
            _mode = mode;
            _requiredCount = requiredCount;
            _requiredPercent = requiredPercent;
            _window = window ?? SignalWindow.FromBars(1);
        }

        public SignalGroupAggregationBuilder ForBars(int bars)
        {
            return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent,
                SignalWindow.FromBars(bars));
        }

        public SignalGroupAggregationBuilder For(SignalWindow window)
        {
            return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent, window);
        }

        public SignalHandle Emit(string? name = null)
        {
            var handle = _catalog.NextHandle();
            _catalog.AddGroupRule(new SignalGroupRule(handle, name ?? handle.ToString(), _conditions, _mode, _requiredCount,
                _requiredPercent, _window));
            return handle;
        }
    }

    private sealed class SignalGroupRule
    {
        public SignalGroupRule(SignalHandle handle, string name, SignalCondition[] conditions, SignalGroupMode mode,
            int? requiredCount, double? requiredPercent, SignalWindow window)
        {
            Handle = handle;
            Name = name;
            Conditions = conditions;
            Mode = mode;
            RequiredCount = requiredCount;
            RequiredPercent = requiredPercent;
            Window = window;
        }

        public SignalHandle Handle { get; }
        public string Name { get; }
        public SignalCondition[] Conditions { get; }
        public SignalGroupMode Mode { get; }
        public int? RequiredCount { get; }
        public double? RequiredPercent { get; }
        public SignalWindow Window { get; }

        public bool IsGroupActive(int activeCount)
        {
            var total = Conditions.Length;
            if (total == 0)
            {
                return false;
            }

            return Mode switch
            {
                SignalGroupMode.All => activeCount == total,
                SignalGroupMode.Any => activeCount > 0,
                SignalGroupMode.AtLeast => activeCount >= Math.Max(1, RequiredCount ?? total),
                SignalGroupMode.Percent => activeCount >= Math.Ceiling(total * Math.Max(0, RequiredPercent ?? 0) / 100d),
                _ => false
            };
        }
    }

    private enum SignalGroupMode
    {
        All,
        Any,
        AtLeast,
        Percent
    }

    private readonly struct SignalHandle : IEquatable<SignalHandle>
    {
        public SignalHandle(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public bool Equals(SignalHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is SignalHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override string ToString()
        {
            return $"Signal:{Id}";
        }
    }

    private sealed class NotificationCatalog
    {
        private readonly List<INotificationChannel> _channels = new();

        public void Console()
        {
            _channels.Add(new ConsoleNotificationChannel());
        }

        public void Email(EmailOptions options)
        {
            _channels.Add(new EmailNotificationChannel(options));
        }

        public void Sms(SmsOptions options)
        {
            _channels.Add(new SmsNotificationChannel(options));
        }

        public void Webhook(WebhookOptions options)
        {
            _channels.Add(new WebhookNotificationChannel(options));
        }

        public void AddChannel(INotificationChannel channel)
        {
            _channels.Add(channel);
        }

        public IReadOnlyList<INotificationChannel> Build()
        {
            return new List<INotificationChannel>(_channels);
        }
    }

    private interface INotificationChannel
    {
        void Notify(NotificationEvent notification);
    }

    private sealed class NotificationEvent
    {
        public NotificationEvent(SignalHandle signal, string name, double value, DateTime timestamp)
        {
            Signal = signal;
            Name = name;
            Value = value;
            Timestamp = timestamp;
        }

        public SignalHandle Signal { get; }
        public string Name { get; }
        public double Value { get; }
        public DateTime Timestamp { get; }
    }

    private sealed class EmailOptions
    {
        public string To { get; set; } = string.Empty;
        public string? From { get; set; }
        public string? Subject { get; set; }
    }

    private sealed class SmsOptions
    {
        public string Number { get; set; } = string.Empty;
    }

    private sealed class WebhookOptions
    {
        public string Url { get; set; } = string.Empty;
    }

    private sealed class ConsoleNotificationChannel : INotificationChannel
    {
        public void Notify(NotificationEvent notification)
        {
            Console.WriteLine($"Notify: {notification.Name} value={notification.Value:F4}");
        }
    }

    private sealed class EmailNotificationChannel : INotificationChannel
    {
        private readonly EmailOptions _options;

        public EmailNotificationChannel(EmailOptions options)
        {
            _options = options;
        }

        public void Notify(NotificationEvent notification)
        {
            Console.WriteLine($"Email: {_options.To} {notification.Name} value={notification.Value:F4}");
        }
    }

    private sealed class SmsNotificationChannel : INotificationChannel
    {
        private readonly SmsOptions _options;

        public SmsNotificationChannel(SmsOptions options)
        {
            _options = options;
        }

        public void Notify(NotificationEvent notification)
        {
            Console.WriteLine($"Sms: {_options.Number} {notification.Name} value={notification.Value:F4}");
        }
    }

    private sealed class WebhookNotificationChannel : INotificationChannel
    {
        private readonly WebhookOptions _options;

        public WebhookNotificationChannel(WebhookOptions options)
        {
            _options = options;
        }

        public void Notify(NotificationEvent notification)
        {
            Console.WriteLine($"Webhook: {_options.Url} {notification.Name} value={notification.Value:F4}");
        }
    }

    private sealed class AutoTradingCatalog
    {
        private readonly List<AutoTradeRule> _rules = new();
        private readonly List<IAutoTradeAdapter> _adapters = new();

        public AutoTradeAdapterBuilder ConsoleAdapter()
        {
            var adapter = new ConsoleTradeAdapter();
            _adapters.Add(adapter);
            return new AutoTradeAdapterBuilder(this, adapter);
        }

        public AutoTradeAdapterBuilder Alpaca(AlpacaOptions options)
        {
            var adapter = new AlpacaTradeAdapter(options);
            _adapters.Add(adapter);
            return new AutoTradeAdapterBuilder(this, adapter);
        }

        public void AddAdapter(IAutoTradeAdapter adapter)
        {
            _adapters.Add(adapter);
        }

        internal void AddRule(AutoTradeRule rule)
        {
            _rules.Add(rule);
        }

        public AutoTradingConfiguration Build()
        {
            return new AutoTradingConfiguration(new List<AutoTradeRule>(_rules), new List<IAutoTradeAdapter>(_adapters));
        }
    }

    private readonly struct AutoTradeAdapterBuilder
    {
        private readonly AutoTradingCatalog _catalog;
        private readonly IAutoTradeAdapter _adapter;

        public AutoTradeAdapterBuilder(AutoTradingCatalog catalog, IAutoTradeAdapter adapter)
        {
            _catalog = catalog;
            _adapter = adapter;
        }

        public AutoTradeRuleBuilder OnSignal(SignalHandle signal)
        {
            return new AutoTradeRuleBuilder(_catalog, _adapter, signal);
        }
    }

    private readonly struct AutoTradeRuleBuilder
    {
        private readonly AutoTradingCatalog _catalog;
        private readonly IAutoTradeAdapter _adapter;
        private readonly SignalHandle _signal;

        public AutoTradeRuleBuilder(AutoTradingCatalog catalog, IAutoTradeAdapter adapter, SignalHandle signal)
        {
            _catalog = catalog;
            _adapter = adapter;
            _signal = signal;
        }

        public AutoTradeAdapterBuilder MarketBuy()
        {
            _catalog.AddRule(new AutoTradeRule(_adapter, _signal, TradeAction.MarketBuy));
            return new AutoTradeAdapterBuilder(_catalog, _adapter);
        }

        public AutoTradeAdapterBuilder MarketSell()
        {
            _catalog.AddRule(new AutoTradeRule(_adapter, _signal, TradeAction.MarketSell));
            return new AutoTradeAdapterBuilder(_catalog, _adapter);
        }

        public AutoTradeAdapterBuilder ClosePosition()
        {
            _catalog.AddRule(new AutoTradeRule(_adapter, _signal, TradeAction.ClosePosition));
            return new AutoTradeAdapterBuilder(_catalog, _adapter);
        }
    }

    private sealed class AutoTradingConfiguration
    {
        public AutoTradingConfiguration(IReadOnlyList<AutoTradeRule> rules, IReadOnlyList<IAutoTradeAdapter> adapters)
        {
            Rules = rules;
            Adapters = adapters;
        }

        public IReadOnlyList<AutoTradeRule> Rules { get; }
        public IReadOnlyList<IAutoTradeAdapter> Adapters { get; }
    }

    private sealed class AutoTradeRule
    {
        public AutoTradeRule(IAutoTradeAdapter adapter, SignalHandle signal, TradeAction action)
        {
            Adapter = adapter;
            Signal = signal;
            Action = action;
        }

        public IAutoTradeAdapter Adapter { get; }
        public SignalHandle Signal { get; }
        public TradeAction Action { get; }
    }

    private interface IAutoTradeAdapter
    {
        void Execute(TradeRequest request);
    }

    private sealed class TradeRequest
    {
        public TradeRequest(SignalHandle signal, TradeAction action, DateTime timestamp)
        {
            Signal = signal;
            Action = action;
            Timestamp = timestamp;
        }

        public SignalHandle Signal { get; }
        public TradeAction Action { get; }
        public DateTime Timestamp { get; }
    }

    private enum TradeAction
    {
        MarketBuy,
        MarketSell,
        ClosePosition
    }

    private sealed class ConsoleTradeAdapter : IAutoTradeAdapter
    {
        public void Execute(TradeRequest request)
        {
            Console.WriteLine($"Trade: {request.Signal} {request.Action}");
        }
    }

    private sealed class AlpacaOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public bool Paper { get; set; } = true;
    }

    private sealed class AlpacaTradeAdapter : IAutoTradeAdapter
    {
        private readonly AlpacaOptions _options;

        public AlpacaTradeAdapter(AlpacaOptions options)
        {
            _options = options;
        }

        public void Execute(TradeRequest request)
        {
            Console.WriteLine($"Alpaca: {_options.ApiKey} {request.Signal} {request.Action}");
        }
    }
    private sealed class IndicatorRuntime
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

        public IndicatorRuntime(IndicatorDataSource source, Dictionary<SeriesHandle, SeriesNode> nodes,
            Dictionary<IndicatorKey, SeriesHandle> keys, IReadOnlyList<SignalRule> signals,
            IReadOnlyList<SignalGroupRule> groupSignals, IReadOnlyList<INotificationChannel> notifications,
            AutoTradingConfiguration autoTrading, BehaviorOptions behavior, IReadOnlyCollection<SeriesHandle> activeSeries,
            IReadOnlyList<SymbolId> symbols, StreamingOptions? streamingOptions, SignalOptions? signalOptions,
            BacktestOptions? backtestOptions, BenchmarkOptions? benchmarkOptions)
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

        public IndicatorSnapshot? Latest { get; private set; }
        public event Action<IndicatorSnapshot>? Updated;

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

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
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
                var spec = pair.Value.Spec!;
                var key = pair.Value.SeriesKey;
                var state = CreateStreamingState(spec);
                session.RegisterStatefulIndicator(key.Symbol.Value, key.Timeframe, state, update =>
                {
                    var value = ExtractStreamingValue(update, spec.Name, spec.Output);
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
                var left = values.TryGetValue(node.Left!.Value, out var leftValues)
                    ? LastValue(leftValues)
                    : double.NaN;
                var right = values.TryGetValue(node.Right!.Value, out var rightValues)
                    ? LastValue(rightValues)
                    : double.NaN;
                var result = node.Formula!(left, right);
                values[pair.Key] = new[] { result };
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

            var result = node.Formula!(left, right);
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
    }

    private sealed class SignalGroupState
    {
        public SignalGroupState(int conditionCount)
        {
            PreviousValues = new double?[conditionCount];
        }

        public double?[] PreviousValues { get; }
        public int ActiveBars { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class SeriesEvaluator
    {
        private readonly StockData _data;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<SeriesHandle, double[]> _cache;

        public SeriesEvaluator(StockData data, Dictionary<SeriesHandle, SeriesNode> nodes)
        {
            _data = data;
            _nodes = nodes;
            _cache = new Dictionary<SeriesHandle, double[]>();
        }

        public Dictionary<SeriesHandle, double[]> Evaluate(IReadOnlyCollection<SeriesHandle> handles)
        {
            var result = new Dictionary<SeriesHandle, double[]>();
            foreach (var handle in handles)
            {
                result[handle] = Resolve(handle, new HashSet<SeriesHandle>());
            }

            return result;
        }

        public double[] Evaluate(SeriesHandle handle)
        {
            return Resolve(handle, new HashSet<SeriesHandle>());
        }

        private double[] Resolve(SeriesHandle handle, HashSet<SeriesHandle> visiting)
        {
            if (_cache.TryGetValue(handle, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(handle))
            {
                throw new InvalidOperationException("Cycle detected in series graph.");
            }

            if (!_nodes.TryGetValue(handle, out var node))
            {
                throw new InvalidOperationException("Unknown series handle.");
            }

            double[] resolved;
            switch (node.Kind)
            {
                case SeriesNodeKind.Base:
                    resolved = GetBaseInput();
                    break;
                case SeriesNodeKind.Indicator:
                    resolved = ResolveIndicator(node, visiting);
                    break;
                case SeriesNodeKind.Formula:
                    resolved = ResolveFormula(node, visiting);
                    break;
                default:
                    throw new InvalidOperationException("Unknown series node kind.");
            }

            visiting.Remove(handle);
            _cache[handle] = resolved;
            return resolved;
        }

        private double[] ResolveIndicator(SeriesNode node, HashSet<SeriesHandle> visiting)
        {
            var input = Resolve(node.Input!.Value, visiting);
            var working = CloneWithCustomValues(_data, input);
            var result = ApplyIndicator(working, node.Spec!);
            return ExtractOutput(result, node.Spec!);
        }

        private double[] ResolveFormula(SeriesNode node, HashSet<SeriesHandle> visiting)
        {
            var left = Resolve(node.Left!.Value, visiting);
            var right = Resolve(node.Right!.Value, visiting);
            var count = Math.Max(left.Length, right.Length);
            var values = new double[count];
            for (var i = 0; i < count; i++)
            {
                var l = i < left.Length ? left[i] : double.NaN;
                var r = i < right.Length ? right[i] : double.NaN;
                values[i] = node.Formula!(l, r);
            }

            return values;
        }

        private double[] GetBaseInput()
        {
            var input = _data.CustomValuesList.Count > 0 ? _data.CustomValuesList : _data.InputValues;
            return input.ToArray();
        }

        private static StockData CloneWithCustomValues(StockData baseData, double[] customValues)
        {
            var clone = new StockData(baseData.TickerDataList, baseData.InputName)
            {
                Options = baseData.Options,
                CustomValuesList = new List<double>(customValues)
            };
            return clone;
        }

        private static StockData ApplyIndicator(StockData data, IndicatorSpec spec)
        {
            return spec.Name switch
            {
                IndicatorName.SimpleMovingAverage => data.CalculateSimpleMovingAverage(((SketchSmaOptions)spec.Options).Length),
                IndicatorName.RelativeStrengthIndex => data.CalculateRelativeStrengthIndex(length: ((SketchRsiOptions)spec.Options).Length),
                IndicatorName.MovingAverageConvergenceDivergence => data.CalculateMovingAverageConvergenceDivergence(
                    fastLength: ((SketchMacdOptions)spec.Options).FastLength,
                    slowLength: ((SketchMacdOptions)spec.Options).SlowLength,
                    signalLength: ((SketchMacdOptions)spec.Options).SignalLength),
                IndicatorName.BollingerBands => data.CalculateBollingerBands(
                    length: ((SketchBollingerBandsOptions)spec.Options).Length,
                    stdDevMult: ((SketchBollingerBandsOptions)spec.Options).StdDevMult),
                _ => throw new NotSupportedException($"Indicator '{spec.Name}' not wired in this sketch.")
            };
        }

        private static double[] ExtractOutput(StockData result, IndicatorSpec spec)
        {
            var key = GetOutputKey(spec.Name, spec.Output);
            if (!string.IsNullOrEmpty(key) && result.OutputValues.TryGetValue(key, out var list))
            {
                return list.ToArray();
            }

            return result.CustomValuesList.ToArray();
        }
    }

    private sealed class IndicatorSnapshot
    {
        private readonly Dictionary<SeriesHandle, double[]> _series;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
        private readonly Func<SeriesHandle, double[]?>? _resolver;

        public IndicatorSnapshot(Dictionary<SeriesHandle, double[]> series, Dictionary<IndicatorKey, SeriesHandle> keys,
            Func<SeriesHandle, double[]?>? resolver = null)
        {
            _series = series;
            _keys = keys;
            _resolver = resolver;
        }

        public bool TryGetSeries(SeriesHandle handle, out ReadOnlyMemory<double> values)
        {
            if (_series.TryGetValue(handle, out var list))
            {
                values = list;
                return true;
            }

            if (_resolver != null)
            {
                var resolved = _resolver(handle);
                if (resolved != null)
                {
                    _series[handle] = resolved;
                    values = resolved;
                    return true;
                }
            }

            values = ReadOnlyMemory<double>.Empty;
            return false;
        }

        public bool TryGetSeries(IndicatorKey key, out ReadOnlyMemory<double> values)
        {
            if (_keys.TryGetValue(key, out var handle))
            {
                return TryGetSeries(handle, out values);
            }

            values = ReadOnlyMemory<double>.Empty;
            return false;
        }
    }
    private sealed class IndicatorSpec
    {
        public IndicatorSpec(IndicatorName name, ISketchOptions options, IndicatorOutput output)
        {
            Name = name;
            Options = options;
            Output = output;
        }

        public IndicatorName Name { get; }
        public ISketchOptions Options { get; }
        public IndicatorOutput Output { get; }
    }

    private static class IndicatorSpecs
    {
        public static IndicatorSpec Sma(int length)
        {
            return new IndicatorSpec(IndicatorName.SimpleMovingAverage, new SketchSmaOptions(length), IndicatorOutput.Primary);
        }

        public static IndicatorSpec Rsi(int length)
        {
            return new IndicatorSpec(IndicatorName.RelativeStrengthIndex, new SketchRsiOptions(length), IndicatorOutput.Primary);
        }

        public static IndicatorSpec Macd(int fastLength, int slowLength, int signalLength, IndicatorOutput output)
        {
            return new IndicatorSpec(IndicatorName.MovingAverageConvergenceDivergence,
                new SketchMacdOptions(fastLength, slowLength, signalLength), output);
        }

        public static IndicatorSpec BollingerBands(int length, double stdDevMult, IndicatorOutput output)
        {
            return new IndicatorSpec(IndicatorName.BollingerBands, new SketchBollingerBandsOptions(length, stdDevMult), output);
        }
    }

    private interface ISketchOptions { }

    private sealed class SketchSmaOptions : ISketchOptions
    {
        public SketchSmaOptions(int length)
        {
            Length = Math.Max(1, length);
        }

        public int Length { get; }
    }

    private sealed class SketchRsiOptions : ISketchOptions
    {
        public SketchRsiOptions(int length)
        {
            Length = Math.Max(1, length);
        }

        public int Length { get; }
    }

    private sealed class SketchMacdOptions : ISketchOptions
    {
        public SketchMacdOptions(int fastLength, int slowLength, int signalLength)
        {
            FastLength = Math.Max(1, fastLength);
            SlowLength = Math.Max(1, slowLength);
            SignalLength = Math.Max(1, signalLength);
        }

        public int FastLength { get; }
        public int SlowLength { get; }
        public int SignalLength { get; }
    }

    private sealed class SketchBollingerBandsOptions : ISketchOptions
    {
        public SketchBollingerBandsOptions(int length, double stdDevMult)
        {
            Length = Math.Max(1, length);
            StdDevMult = stdDevMult;
        }

        public int Length { get; }
        public double StdDevMult { get; }
    }

    private enum IndicatorOutput
    {
        Primary,
        Signal,
        Histogram,
        UpperBand,
        MiddleBand,
        LowerBand
    }

    private enum FormulaOp
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    private readonly struct SeriesHandle : IEquatable<SeriesHandle>
    {
        public SeriesHandle(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public bool Equals(SeriesHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is SeriesHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override string ToString()
        {
            return $"Series:{Id}";
        }
    }

    private readonly struct IndicatorKey : IEquatable<IndicatorKey>
    {
        public IndicatorKey(IndicatorName name, IndicatorOutput output)
        {
            Name = name;
            Output = output;
        }

        public IndicatorName Name { get; }
        public IndicatorOutput Output { get; }

        public static IndicatorKey From(IndicatorName name, IndicatorOutput output = IndicatorOutput.Primary)
        {
            return new IndicatorKey(name, output);
        }

        public static IndicatorKey Sma => new IndicatorKey(IndicatorName.SimpleMovingAverage, IndicatorOutput.Primary);
        public static IndicatorKey Rsi => new IndicatorKey(IndicatorName.RelativeStrengthIndex, IndicatorOutput.Primary);
        public static IndicatorKey Macd => new IndicatorKey(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Primary);
        public static IndicatorKey MacdSignal => new IndicatorKey(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Signal);
        public static IndicatorKey MacdHistogram => new IndicatorKey(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Histogram);
        public static IndicatorKey BollingerUpper => new IndicatorKey(IndicatorName.BollingerBands, IndicatorOutput.UpperBand);
        public static IndicatorKey BollingerMiddle => new IndicatorKey(IndicatorName.BollingerBands, IndicatorOutput.MiddleBand);
        public static IndicatorKey BollingerLower => new IndicatorKey(IndicatorName.BollingerBands, IndicatorOutput.LowerBand);

        public bool Equals(IndicatorKey other)
        {
            return Name == other.Name && Output == other.Output;
        }

        public override bool Equals(object? obj)
        {
            return obj is IndicatorKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Name, (int)Output);
        }

        public override string ToString()
        {
            return $"{Name}/{Output}";
        }
    }

    private readonly struct SeriesKey : IEquatable<SeriesKey>
    {
        public SeriesKey(SymbolId symbol, BarTimeframe timeframe)
        {
            Symbol = symbol;
            Timeframe = timeframe;
        }

        public SymbolId Symbol { get; }
        public BarTimeframe Timeframe { get; }

        public bool Equals(SeriesKey other)
        {
            return Symbol.Equals(other.Symbol) && Timeframe.Equals(other.Timeframe);
        }

        public override bool Equals(object? obj)
        {
            return obj is SeriesKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Symbol, Timeframe);
        }

        public override string ToString()
        {
            return $"{Symbol}:{Timeframe}";
        }
    }

    private sealed class SeriesNode
    {
        private SeriesNode(SeriesNodeKind kind, SeriesKey seriesKey, SeriesHandle? input, IndicatorSpec? spec,
            SeriesHandle? left, SeriesHandle? right, Func<double, double, double>? formula)
        {
            Kind = kind;
            SeriesKey = seriesKey;
            Input = input;
            Spec = spec;
            Left = left;
            Right = right;
            Formula = formula;
        }

        public SeriesNodeKind Kind { get; }
        public SeriesKey SeriesKey { get; }
        public SeriesHandle? Input { get; }
        public IndicatorSpec? Spec { get; }
        public SeriesHandle? Left { get; }
        public SeriesHandle? Right { get; }
        public Func<double, double, double>? Formula { get; }

        public static SeriesNode Base(SeriesKey key)
        {
            return new SeriesNode(SeriesNodeKind.Base, key, null, null, null, null, null);
        }

        public static SeriesNode Indicator(SeriesKey key, SeriesHandle input, IndicatorSpec spec)
        {
            return new SeriesNode(SeriesNodeKind.Indicator, key, input, spec, null, null, null);
        }

        public static SeriesNode CreateFormula(SeriesKey key, SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
        {
            return new SeriesNode(SeriesNodeKind.Formula, key, null, null, left, right, formula);
        }
    }

    private sealed class ReplayStreamSource : IStreamSource
    {
        private readonly IReadOnlyList<StreamTrade> _trades;

        public ReplayStreamSource(IReadOnlyList<StreamTrade> trades)
        {
            _trades = trades ?? throw new ArgumentNullException(nameof(trades));
        }

        public IStreamSubscription Subscribe(StreamSubscriptionRequest request, IStreamObserver observer)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return new ReplayStreamSubscription(_trades, request, observer);
        }

        private sealed class ReplayStreamSubscription : IStreamSubscription
        {
            private readonly IReadOnlyList<StreamTrade> _trades;
            private readonly StreamSubscriptionRequest _request;
            private readonly IStreamObserver _observer;
            private bool _stopped;

            public ReplayStreamSubscription(IReadOnlyList<StreamTrade> trades, StreamSubscriptionRequest request, IStreamObserver observer)
            {
                _trades = trades;
                _request = request;
                _observer = observer;
            }

            public void Start()
            {
                if (!_request.Trades)
                {
                    return;
                }

                for (var i = 0; i < _trades.Count; i++)
                {
                    if (_stopped)
                    {
                        break;
                    }

                    var trade = _trades[i];
                    if (!SymbolMatches(trade.Symbol))
                    {
                        continue;
                    }

                    _observer.OnTrade(trade);
                }
            }

            public void Stop()
            {
                _stopped = true;
            }

            public void Dispose()
            {
                Stop();
            }

            private bool SymbolMatches(string symbol)
            {
                for (var i = 0; i < _request.Symbols.Count; i++)
                {
                    if (string.Equals(_request.Symbols[i], symbol, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    private enum SeriesNodeKind
    {
        Base,
        Indicator,
        Formula
    }

    private static string? GetOutputKey(IndicatorName name, IndicatorOutput output)
    {
        if (name == IndicatorName.MovingAverageConvergenceDivergence)
        {
            return output switch
            {
                IndicatorOutput.Signal => "Signal",
                IndicatorOutput.Histogram => "Histogram",
                _ => null
            };
        }

        if (name == IndicatorName.BollingerBands)
        {
            return output switch
            {
                IndicatorOutput.UpperBand => "UpperBand",
                IndicatorOutput.LowerBand => "LowerBand",
                _ => "MiddleBand"
            };
        }

        return null;
    }

    private static IStreamingIndicatorState CreateStreamingState(IndicatorSpec spec)
    {
        return spec.Name switch
        {
            IndicatorName.SimpleMovingAverage => new SimpleMovingAverageState(((SketchSmaOptions)spec.Options).Length),
            IndicatorName.RelativeStrengthIndex => new RelativeStrengthIndexState(((SketchRsiOptions)spec.Options).Length),
            IndicatorName.MovingAverageConvergenceDivergence => new MovingAverageConvergenceDivergenceState(
                ((SketchMacdOptions)spec.Options).FastLength,
                ((SketchMacdOptions)spec.Options).SlowLength,
                ((SketchMacdOptions)spec.Options).SignalLength),
            IndicatorName.BollingerBands => new BollingerBandsState(
                ((SketchBollingerBandsOptions)spec.Options).Length,
                ((SketchBollingerBandsOptions)spec.Options).StdDevMult),
            _ => throw new NotSupportedException($"Indicator '{spec.Name}' not wired for streaming in this sketch.")
        };
    }

    private static double ExtractStreamingValue(StreamingIndicatorStateUpdate update, IndicatorName name, IndicatorOutput output)
    {
        if (output == IndicatorOutput.Primary || output == IndicatorOutput.MiddleBand)
        {
            return update.Value;
        }

        if (update.Outputs == null)
        {
            return double.NaN;
        }

        var key = GetOutputKey(name, output);
        return key != null && update.Outputs.TryGetValue(key, out var value) ? value : double.NaN;
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
