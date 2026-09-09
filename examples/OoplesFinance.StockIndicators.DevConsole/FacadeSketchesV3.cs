
using System;
using System.Collections.Generic;
using System.Globalization;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

internal static class FacadeSketchesV3
{
    private const string Symbol = "AAPL";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("Facade/builder sketches (v3):");
        RunOptionA();
        RunOptionB();
        RunOptionC();
    }

    private static void RunOptionA()
    {
        Console.WriteLine();
        Console.WriteLine("Option A: Beginner facade (builder + runtime)");

        var data = BuildSampleData(160, 101d, new DateTime(2024, 7, 2, 9, 30, 0, DateTimeKind.Utc));
        var source = IndicatorSource.FromBatch(new StockData(data));
        var runtime = new StockIndicatorBuilder(source)
            .UsePack(IndicatorPack.Core)
            .AddSignal(SignalPreset.RsiOverbought)
            .Notify(AlertSink.Console())
            .Build();

        runtime.Updated += snapshot =>
        {
            PrintLast(snapshot, IndicatorKey.From(IndicatorName.SimpleMovingAverage), "SMA(20)");
            PrintLast(snapshot, IndicatorKey.From(IndicatorName.RelativeStrengthIndex), "RSI(14)");
            PrintLast(snapshot, IndicatorKey.From(IndicatorName.MovingAverageConvergenceDivergence), "MACD");
        };

        runtime.Start();
    }

    private static void RunOptionB()
    {
        Console.WriteLine();
        Console.WriteLine("Option B: Typed handles + formulas");

        var data = BuildSampleData(180, 99d, new DateTime(2024, 8, 2, 9, 30, 0, DateTimeKind.Utc));
        var source = IndicatorSource.FromBatch(new StockData(data));
        var builder = new StockIndicatorBuilder(source);

        var price = builder.BaseSeries;
        var sma = builder.AddIndicator(IndicatorSpecs.Sma(20), price);
        var rsi = builder.AddIndicator(IndicatorSpecs.Rsi(14), price);
        var rsiSma = builder.Then(rsi, IndicatorSpecs.Sma(5));
        var spread = builder.Formula(sma, rsi, (x, y) => x - y);

        var runtime = builder.Build();
        runtime.Start();

        if (runtime.Latest != null)
        {
            PrintLast(runtime.Latest, sma, "SMA(20)");
            PrintLast(runtime.Latest, rsi, "RSI(14)");
            PrintLast(runtime.Latest, rsiSma, "RSI SMA(5)");
            PrintLast(runtime.Latest, spread, "SMA - RSI");
        }
    }

    private static void RunOptionC()
    {
        Console.WriteLine();
        Console.WriteLine("Option C: Streaming + signals + alerts");

        var trades = BuildTradeEvents(Symbol, 12);
        var stream = new ReplayStreamSource(trades);
        var options = new StreamingOptions
        {
            Symbols = new[] { Symbol },
            SubscribeTrades = true,
            SubscribeQuotes = false,
            SubscribeBars = false,
            UpdatePolicy = StreamingUpdatePolicy.FinalOnly,
            ProcessingMode = StreamingProcessingMode.Inline
        };

        var builder = new StockIndicatorBuilder(IndicatorSource.FromStreaming(stream, options, BarTimeframe.Tick));
        builder.AddIndicator(IndicatorSpecs.Sma(5), key: IndicatorKey.From(IndicatorName.SimpleMovingAverage));
        builder.AddIndicator(IndicatorSpecs.Rsi(14), key: IndicatorKey.From(IndicatorName.RelativeStrengthIndex));
        builder.AddSignal(new SignalRule("RSI overbought", IndicatorKey.From(IndicatorName.RelativeStrengthIndex),
            SignalComparison.Above, 70));
        builder.Notify(AlertSink.Console());
        var runtime = builder.Build();

        runtime.Updated += snapshot =>
        {
            PrintLast(snapshot, IndicatorKey.From(IndicatorName.SimpleMovingAverage), "SMA(5)");
        };

        runtime.Start();
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

    private static void PrintLast(IndicatorSnapshot snapshot, SeriesHandle handle, string label)
    {
        if (!snapshot.TryGetSeries(handle, out var values))
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
        var random = new Random(415);
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

            var volume = 950d + (random.NextDouble() * 110d);
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

    private static List<StreamTrade> BuildTradeEvents(string symbol, int count)
    {
        var trades = new List<StreamTrade>(count);
        var start = new DateTime(2024, 9, 1, 9, 30, 0, DateTimeKind.Utc);
        var price = 100d;
        for (var i = 0; i < count; i++)
        {
            price += i % 2 == 0 ? 0.6 : -0.2;
            trades.Add(new StreamTrade(symbol, start.AddSeconds(i), price, 1));
        }

        return trades;
    }
    private sealed class IndicatorSource
    {
        private IndicatorSource(IndicatorSourceKind kind, StockData? batchData, IStreamSource? streamSource,
            StreamingOptions? options, BarTimeframe timeframe)
        {
            Kind = kind;
            BatchData = batchData;
            StreamSource = streamSource;
            StreamingOptions = options;
            Timeframe = timeframe;
        }

        public IndicatorSourceKind Kind { get; }
        public StockData? BatchData { get; }
        public IStreamSource? StreamSource { get; }
        public StreamingOptions? StreamingOptions { get; }
        public BarTimeframe Timeframe { get; }

        public static IndicatorSource FromBatch(StockData data)
        {
            return new IndicatorSource(IndicatorSourceKind.Batch, data, null, null, BarTimeframe.Tick);
        }

        public static IndicatorSource FromStreaming(IStreamSource source, StreamingOptions options, BarTimeframe timeframe)
        {
            return new IndicatorSource(IndicatorSourceKind.Streaming, null, source, options, timeframe);
        }
    }

    private enum IndicatorSourceKind
    {
        Batch,
        Streaming
    }

    private sealed class StockIndicatorBuilder
    {
        private readonly IndicatorSource _source;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
        private readonly List<SignalRule> _signals;
        private readonly List<AlertSink> _sinks;
        private readonly SeriesHandle _baseSeries;
        private int _nextId;

        public StockIndicatorBuilder(IndicatorSource source)
        {
            _source = source;
            _nodes = new Dictionary<SeriesHandle, SeriesNode>();
            _keys = new Dictionary<IndicatorKey, SeriesHandle>();
            _signals = new List<SignalRule>();
            _sinks = new List<AlertSink>();
            _nextId = 1;
            _baseSeries = NewHandle();
            _nodes[_baseSeries] = SeriesNode.Base();
        }

        public SeriesHandle BaseSeries => _baseSeries;

        public StockIndicatorBuilder UsePack(IndicatorPack pack)
        {
            if (pack == IndicatorPack.Core)
            {
                AddIndicator(IndicatorSpecs.Sma(20), _baseSeries, IndicatorKey.From(IndicatorName.SimpleMovingAverage));
                AddIndicator(IndicatorSpecs.Rsi(14), _baseSeries, IndicatorKey.From(IndicatorName.RelativeStrengthIndex));
                AddIndicator(IndicatorSpecs.Macd(IndicatorOutput.Primary), _baseSeries,
                    IndicatorKey.From(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Primary));
                AddIndicator(IndicatorSpecs.Macd(IndicatorOutput.Signal), _baseSeries,
                    IndicatorKey.From(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Signal));
                AddIndicator(IndicatorSpecs.Macd(IndicatorOutput.Histogram), _baseSeries,
                    IndicatorKey.From(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Histogram));
            }

            return this;
        }

        public SeriesHandle AddIndicator(IndicatorSpec spec, SeriesHandle? input = null, IndicatorKey? key = null)
        {
            var handle = NewHandle();
            _nodes[handle] = SeriesNode.Indicator(input ?? _baseSeries, spec);
            if (key.HasValue)
            {
                _keys[key.Value] = handle;
            }

            return handle;
        }

        public SeriesHandle Then(SeriesHandle input, IndicatorSpec spec, IndicatorKey? key = null)
        {
            return AddIndicator(spec, input, key);
        }

        public SeriesHandle Formula(SeriesHandle left, SeriesHandle right, FormulaOp op)
        {
            return Formula(left, right, op switch
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
            var handle = NewHandle();
            _nodes[handle] = SeriesNode.CreateFormula(left, right, formula);
            return handle;
        }

        public StockIndicatorBuilder AddSignal(SignalPreset preset)
        {
            _signals.Add(SignalRule.FromPreset(preset));
            return this;
        }

        public StockIndicatorBuilder AddSignal(SignalRule rule)
        {
            _signals.Add(rule);
            return this;
        }

        public StockIndicatorBuilder Notify(AlertSink sink)
        {
            _sinks.Add(sink);
            return this;
        }

        public IndicatorRuntime Build()
        {
            return new IndicatorRuntime(_source, new Dictionary<SeriesHandle, SeriesNode>(_nodes),
                new Dictionary<IndicatorKey, SeriesHandle>(_keys), new List<SignalRule>(_signals),
                new List<AlertSink>(_sinks), _baseSeries);
        }

        private SeriesHandle NewHandle()
        {
            var handle = new SeriesHandle(_nextId);
            _nextId++;
            return handle;
        }
    }
    private sealed class IndicatorRuntime
    {
        private readonly IndicatorSource _source;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
        private readonly List<SignalRule> _signals;
        private readonly Dictionary<SignalRule, bool> _signalStates;
        private readonly SeriesHandle _baseSeries;

        public IndicatorRuntime(IndicatorSource source, Dictionary<SeriesHandle, SeriesNode> nodes,
            Dictionary<IndicatorKey, SeriesHandle> keys, List<SignalRule> signals, List<AlertSink> sinks,
            SeriesHandle baseSeries)
        {
            _source = source;
            _nodes = nodes;
            _keys = keys;
            _signals = signals;
            _signalStates = new Dictionary<SignalRule, bool>();
            _baseSeries = baseSeries;

            for (var i = 0; i < sinks.Count; i++)
            {
                SignalFired += sinks[i].Notify;
            }
        }

        public IndicatorSnapshot? Latest { get; private set; }
        public event Action<IndicatorSnapshot>? Updated;
        public event Action<SignalEvent>? SignalFired;

        public void Start()
        {
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
            var evaluator = new SeriesEvaluator(data, _nodes, _baseSeries);
            var series = evaluator.EvaluateAll();
            Publish(new IndicatorSnapshot(series, _keys));
        }

        private void StartStreaming()
        {
            var stream = _source.StreamSource ?? throw new InvalidOperationException("Streaming source missing stream.");
            var options = _source.StreamingOptions ?? throw new InvalidOperationException("Streaming source missing options.");
            if (options.Symbols == null || options.Symbols.Count == 0)
            {
                throw new InvalidOperationException("Streaming options must include at least one symbol.");
            }

            var primarySymbol = options.Symbols[0];
            var values = new Dictionary<SeriesHandle, double[]>(_nodes.Count);
            using var session = StreamingSession.Create(stream, options.Symbols, options: options);

            var subscriptionOptions = new IndicatorSubscriptionOptions
            {
                IncludeUpdates = false,
                IncludeOutputValues = true
            };

            foreach (var pair in _nodes)
            {
                if (pair.Value.Kind != SeriesNodeKind.Indicator)
                {
                    continue;
                }

                var handle = pair.Key;
                var spec = pair.Value.Spec!;
                var state = CreateStreamingState(spec);
                session.RegisterStatefulIndicator(primarySymbol, _source.Timeframe, state, update =>
                {
                    var value = ExtractStreamingValue(update, spec.Output);
                    values[handle] = new[] { value };
                    UpdateFormulaNodes(values);
                    Publish(new IndicatorSnapshot(new Dictionary<SeriesHandle, double[]>(values), _keys));
                }, subscriptionOptions);
            }

            session.Start();
        }

        private void UpdateFormulaNodes(Dictionary<SeriesHandle, double[]> values)
        {
            foreach (var pair in _nodes)
            {
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

        private void Publish(IndicatorSnapshot snapshot)
        {
            Latest = snapshot;
            Updated?.Invoke(snapshot);
            EvaluateSignals(snapshot);
        }

        private void EvaluateSignals(IndicatorSnapshot snapshot)
        {
            for (var i = 0; i < _signals.Count; i++)
            {
                var rule = _signals[i];
                if (!snapshot.TryGetSeries(rule.Key, out var values))
                {
                    continue;
                }

                var last = LastValue(values);
                if (double.IsNaN(last))
                {
                    continue;
                }

                var triggered = rule.Evaluate(last);
                if (_signalStates.TryGetValue(rule, out var prior) && prior == triggered)
                {
                    continue;
                }

                _signalStates[rule] = triggered;
                if (triggered)
                {
                    SignalFired?.Invoke(new SignalEvent(rule.Name, last, rule.Key));
                }
            }
        }

        private static double LastValue(ReadOnlyMemory<double> values)
        {
            return values.Length == 0 ? double.NaN : values.Span[values.Length - 1];
        }
    }

    private sealed class SeriesEvaluator
    {
        private readonly StockData _data;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<SeriesHandle, double[]> _cache;

        public SeriesEvaluator(StockData data, Dictionary<SeriesHandle, SeriesNode> nodes, SeriesHandle baseSeries)
        {
            _data = data;
            _nodes = nodes;
            _cache = new Dictionary<SeriesHandle, double[]>
            {
                [baseSeries] = GetBaseInput()
            };
        }

        public Dictionary<SeriesHandle, double[]> EvaluateAll()
        {
            var result = new Dictionary<SeriesHandle, double[]>();
            foreach (var handle in _nodes.Keys)
            {
                result[handle] = Resolve(handle, new HashSet<SeriesHandle>());
            }

            return result;
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
                _ => throw new NotSupportedException($"Indicator '{spec.Name}' not wired in this sketch.")
            };
        }

        private static double[] ExtractOutput(StockData result, IndicatorSpec spec)
        {
            if (spec.Name == IndicatorName.MovingAverageConvergenceDivergence && spec.Output != IndicatorOutput.Primary)
            {
                var key = GetMacdKey(spec.Output);
                if (result.OutputValues.TryGetValue(key, out var list))
                {
                    return list.ToArray();
                }
            }

            return result.CustomValuesList.ToArray();
        }
    }
    private sealed class IndicatorSnapshot
    {
        private readonly Dictionary<SeriesHandle, double[]> _series;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;

        public IndicatorSnapshot(Dictionary<SeriesHandle, double[]> series, Dictionary<IndicatorKey, SeriesHandle> keys)
        {
            _series = series;
            _keys = keys;
        }

        public bool TryGetSeries(SeriesHandle handle, out ReadOnlyMemory<double> values)
        {
            if (_series.TryGetValue(handle, out var list))
            {
                values = list;
                return true;
            }

            values = ReadOnlyMemory<double>.Empty;
            return false;
        }

        public bool TryGetSeries(IndicatorKey key, out ReadOnlyMemory<double> values)
        {
            if (_keys.TryGetValue(key, out var handle) && _series.TryGetValue(handle, out var list))
            {
                values = list;
                return true;
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

        public static IndicatorSpec Macd(IndicatorOutput output)
        {
            return new IndicatorSpec(IndicatorName.MovingAverageConvergenceDivergence, new SketchMacdOptions(12, 26, 9), output);
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

    private enum IndicatorPack
    {
        Core
    }

    private enum IndicatorOutput
    {
        Primary,
        Signal,
        Histogram
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
    }

    private sealed class SignalRule
    {
        public SignalRule(string name, IndicatorKey key, SignalComparison comparison, double threshold)
        {
            Name = name;
            Key = key;
            Comparison = comparison;
            Threshold = threshold;
        }

        public string Name { get; }
        public IndicatorKey Key { get; }
        public SignalComparison Comparison { get; }
        public double Threshold { get; }

        public static SignalRule FromPreset(SignalPreset preset)
        {
            return preset switch
            {
                SignalPreset.RsiOverbought => new SignalRule("RSI overbought",
                    IndicatorKey.From(IndicatorName.RelativeStrengthIndex), SignalComparison.Above, 70),
                SignalPreset.RsiOversold => new SignalRule("RSI oversold",
                    IndicatorKey.From(IndicatorName.RelativeStrengthIndex), SignalComparison.Below, 30),
                _ => new SignalRule("RSI overbought",
                    IndicatorKey.From(IndicatorName.RelativeStrengthIndex), SignalComparison.Above, 70)
            };
        }

        public bool Evaluate(double value)
        {
            return Comparison switch
            {
                SignalComparison.Above => value >= Threshold,
                SignalComparison.Below => value <= Threshold,
                _ => false
            };
        }
    }

    private enum SignalPreset
    {
        RsiOverbought,
        RsiOversold
    }

    private enum SignalComparison
    {
        Above,
        Below
    }

    private sealed class SignalEvent
    {
        public SignalEvent(string name, double value, IndicatorKey key)
        {
            Name = name;
            Value = value;
            Key = key;
        }

        public string Name { get; }
        public double Value { get; }
        public IndicatorKey Key { get; }
    }

    private sealed class AlertSink
    {
        private readonly Action<SignalEvent> _handler;

        private AlertSink(Action<SignalEvent> handler)
        {
            _handler = handler;
        }

        public static AlertSink Console()
        {
            return new AlertSink(signal =>
            {
                System.Console.WriteLine($"Signal: {signal.Name} ({signal.Key.Name}/{signal.Key.Output}) value={signal.Value:F4}");
            });
        }

        public void Notify(SignalEvent signal)
        {
            _handler(signal);
        }
    }

    private sealed class SeriesNode
    {
        private SeriesNode(SeriesNodeKind kind, SeriesHandle? input, IndicatorSpec? spec, SeriesHandle? left,
            SeriesHandle? right, Func<double, double, double>? formula)
        {
            Kind = kind;
            Input = input;
            Spec = spec;
            Left = left;
            Right = right;
            Formula = formula;
        }

        public SeriesNodeKind Kind { get; }
        public SeriesHandle? Input { get; }
        public IndicatorSpec? Spec { get; }
        public SeriesHandle? Left { get; }
        public SeriesHandle? Right { get; }
        public Func<double, double, double>? Formula { get; }

        public static SeriesNode Base()
        {
            return new SeriesNode(SeriesNodeKind.Base, null, null, null, null, null);
        }

        public static SeriesNode Indicator(SeriesHandle input, IndicatorSpec spec)
        {
            return new SeriesNode(SeriesNodeKind.Indicator, input, spec, null, null, null);
        }

        public static SeriesNode CreateFormula(SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
        {
            return new SeriesNode(SeriesNodeKind.Formula, null, null, left, right, formula);
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

    private static string GetMacdKey(IndicatorOutput output)
    {
        return output switch
        {
            IndicatorOutput.Signal => "Signal",
            IndicatorOutput.Histogram => "Histogram",
            _ => "Macd"
        };
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
            _ => throw new NotSupportedException($"Indicator '{spec.Name}' not wired for streaming in this sketch.")
        };
    }

    private static double ExtractStreamingValue(StreamingIndicatorStateUpdate update, IndicatorOutput output)
    {
        if (output == IndicatorOutput.Primary)
        {
            return update.Value;
        }

        if (update.Outputs == null)
        {
            return double.NaN;
        }

        var key = GetMacdKey(output);
        return update.Outputs.TryGetValue(key, out var value) ? value : double.NaN;
    }
}
