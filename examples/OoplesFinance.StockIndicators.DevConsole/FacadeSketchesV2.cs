using System;
using System.Collections.Generic;
using System.Globalization;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

internal static class FacadeSketchesV2
{
    private const string Symbol = "AAPL";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("Facade/builder sketches (v2):");
        RunOptionA();
        RunOptionB();
        RunOptionC();
    }

    private static void RunOptionA()
    {
        Console.WriteLine();
        Console.WriteLine("Option A: Builder -> Result (AiDotNet-style flow)");

        var data = BuildSampleData(160, 100d, new DateTime(2024, 4, 2, 9, 30, 0, DateTimeKind.Utc));
        var stockData = new StockData(data);
        var builder = IndicatorModelBuilder
            .Create()
            .ConfigureIndicator(SeriesId.Sma20, IndicatorSpecs.Sma(20))
            .ConfigureIndicator(SeriesId.Rsi14, IndicatorSpecs.Rsi(14))
            .ConfigureIndicator(SeriesId.RsiSma5, IndicatorSpecs.Sma(5), input: SeriesId.Rsi14)
            .Combine(SeriesId.SmaMinusRsi, SeriesId.Sma20, SeriesId.Rsi14, CombineOp.Subtract);

        var model = builder.Build();
        var result = model.Run(stockData);

        PrintLast(result, SeriesId.Sma20, "SMA(20)");
        PrintLast(result, SeriesId.Rsi14, "RSI(14)");
        PrintLast(result, SeriesId.RsiSma5, "RSI SMA(5)");
        PrintLast(result, SeriesId.SmaMinusRsi, "SMA - RSI");
    }

    private static void RunOptionB()
    {
        Console.WriteLine();
        Console.WriteLine("Option B: Recipe builder (spec + chain)");

        var data = BuildSampleData(160, 97d, new DateTime(2024, 5, 2, 9, 30, 0, DateTimeKind.Utc));
        var stockData = new StockData(data);
        var flow = IndicatorFlow
            .For(stockData)
            .Add(SeriesId.Sma20, IndicatorSpecs.Sma(20))
            .Add(SeriesId.Rsi14, IndicatorSpecs.Rsi(14))
            .Add(SeriesId.Macd, IndicatorSpecs.Macd(MacdOutputKey.Macd))
            .Add(SeriesId.MacdSignal, IndicatorSpecs.Macd(MacdOutputKey.Signal))
            .Combine(SeriesId.MacdHistogram, SeriesId.Macd, SeriesId.MacdSignal, CombineOp.Subtract);

        var result = flow.Run();
        PrintLast(result, SeriesId.Macd, "MACD");
        PrintLast(result, SeriesId.MacdSignal, "MACD Signal");
        PrintLast(result, SeriesId.MacdHistogram, "MACD Histogram");
    }

    private static void RunOptionC()
    {
        Console.WriteLine();
        Console.WriteLine("Option C: Streaming builder (auto uses StreamingSession)");

        var trades = BuildTradeEvents(Symbol, 12);
        var source = new ReplayStreamSource(trades);
        var options = new StreamingOptions
        {
            Symbols = new[] { Symbol },
            SubscribeTrades = true,
            SubscribeQuotes = false,
            SubscribeBars = false,
            UpdatePolicy = StreamingUpdatePolicy.FinalOnly,
            ProcessingMode = StreamingProcessingMode.Inline
        };

        var stream = StreamingIndicatorBuilder
            .For(Symbol, BarTimeframe.Tick)
            .Add(IndicatorSpecs.Sma(5), new IndicatorSubscriptionOptions { IncludeUpdates = false })
            .Add(IndicatorSpecs.Rsi(14), new IndicatorSubscriptionOptions { IncludeUpdates = false })
            .Build();

        var updates = stream.Run(source, options);
        Console.WriteLine($"Streaming updates = {updates.Count}");
        if (updates.Count > 0)
        {
            var last = updates[updates.Count - 1];
            Console.WriteLine($"{last.Indicator} last = {FormatValue(last.Value)}");
        }
    }

    private static void PrintLast(IndicatorRunResult result, SeriesId seriesId, string label)
    {
        if (!result.TryGetSeries(seriesId, out var values))
        {
            Console.WriteLine($"{label} last = n/a");
            return;
        }

        Console.WriteLine($"{label} last = {FormatValue(LastValue(values))}");
    }

    private static double LastValue(List<double> values)
    {
        return values.Count == 0 ? double.NaN : values[values.Count - 1];
    }

    private static string FormatValue(double value)
    {
        return double.IsNaN(value) ? "NaN" : value.ToString("F4", Invariant);
    }

    private static List<TickerData> BuildSampleData(int count, double startPrice, DateTime start)
    {
        var data = new List<TickerData>(count);
        var random = new Random(321);
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

            var volume = 900d + (random.NextDouble() * 120d);
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
        var start = new DateTime(2024, 6, 1, 9, 30, 0, DateTimeKind.Utc);
        var price = 100d;
        for (var i = 0; i < count; i++)
        {
            price += i % 2 == 0 ? 0.6 : -0.2;
            trades.Add(new StreamTrade(symbol, start.AddSeconds(i), price, 1));
        }

        return trades;
    }

    private sealed class IndicatorModelBuilder
    {
        private readonly Dictionary<SeriesId, SeriesNode> _series;

        private IndicatorModelBuilder()
        {
            _series = new Dictionary<SeriesId, SeriesNode>
            {
                [SeriesId.Price] = SeriesNode.Base()
            };
        }

        public static IndicatorModelBuilder Create()
        {
            return new IndicatorModelBuilder();
        }

        public IndicatorModelBuilder ConfigureIndicator(SeriesId id, IndicatorSpec spec, SeriesId input = SeriesId.Price)
        {
            _series[id] = SeriesNode.Indicator(input, spec);
            return this;
        }

        public IndicatorModelBuilder Combine(SeriesId id, SeriesId left, SeriesId right, CombineOp op)
        {
            _series[id] = SeriesNode.Combine(left, right, op);
            return this;
        }

        public IndicatorModelResult Build()
        {
            return new IndicatorModelResult(new Dictionary<SeriesId, SeriesNode>(_series));
        }
    }

    private sealed class IndicatorModelResult
    {
        private readonly Dictionary<SeriesId, SeriesNode> _series;

        public IndicatorModelResult(Dictionary<SeriesId, SeriesNode> series)
        {
            _series = series;
        }

        public IndicatorRunResult Run(StockData data)
        {
            var evaluator = new SeriesEvaluator(data, _series);
            return new IndicatorRunResult(evaluator.EvaluateAll());
        }
    }

    private sealed class IndicatorFlow
    {
        private readonly StockData _data;
        private readonly Dictionary<SeriesId, SeriesNode> _series;

        private IndicatorFlow(StockData data)
        {
            _data = data;
            _series = new Dictionary<SeriesId, SeriesNode>
            {
                [SeriesId.Price] = SeriesNode.Base()
            };
        }

        public static IndicatorFlow For(StockData data)
        {
            return new IndicatorFlow(data);
        }

        public IndicatorFlow Add(SeriesId id, IndicatorSpec spec, SeriesId input = SeriesId.Price)
        {
            _series[id] = SeriesNode.Indicator(input, spec);
            return this;
        }

        public IndicatorFlow Combine(SeriesId id, SeriesId left, SeriesId right, CombineOp op)
        {
            _series[id] = SeriesNode.Combine(left, right, op);
            return this;
        }

        public IndicatorRunResult Run()
        {
            var evaluator = new SeriesEvaluator(_data, _series);
            return new IndicatorRunResult(evaluator.EvaluateAll());
        }
    }

    private sealed class IndicatorRunResult
    {
        private readonly Dictionary<SeriesId, List<double>> _outputs;

        public IndicatorRunResult(Dictionary<SeriesId, List<double>> outputs)
        {
            _outputs = outputs;
        }

        public bool TryGetSeries(SeriesId id, out List<double> values)
        {
            return _outputs.TryGetValue(id, out values!);
        }
    }

    private sealed class SeriesEvaluator
    {
        private readonly StockData _data;
        private readonly Dictionary<SeriesId, SeriesNode> _series;
        private readonly Dictionary<SeriesId, List<double>> _cache;

        public SeriesEvaluator(StockData data, Dictionary<SeriesId, SeriesNode> series)
        {
            _data = data;
            _series = series;
            _cache = new Dictionary<SeriesId, List<double>>();
        }

        public Dictionary<SeriesId, List<double>> EvaluateAll()
        {
            var results = new Dictionary<SeriesId, List<double>>();
            foreach (var entry in _series)
            {
                if (entry.Key == SeriesId.Price)
                {
                    continue;
                }

                results[entry.Key] = Resolve(entry.Key);
            }

            return results;
        }

        private List<double> Resolve(SeriesId id)
        {
            if (_cache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            if (!_series.TryGetValue(id, out var node))
            {
                throw new InvalidOperationException($"Unknown series '{id}'.");
            }

            List<double> resolved;
            switch (node.Kind)
            {
                case SeriesNodeKind.Base:
                    resolved = GetBaseInput();
                    break;
                case SeriesNodeKind.Indicator:
                    resolved = ResolveIndicator(node);
                    break;
                case SeriesNodeKind.Combine:
                    resolved = ResolveCombine(node);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node kind for '{id}'.");
            }

            _cache[id] = resolved;
            return resolved;
        }

        private List<double> GetBaseInput()
        {
            var input = _data.CustomValuesList.Count > 0 ? _data.CustomValuesList : _data.InputValues;
            return new List<double>(input);
        }

        private List<double> ResolveIndicator(SeriesNode node)
        {
            var input = Resolve(node.InputId!.Value);
            var working = CloneWithCustomValues(_data, input);
            var result = ApplyIndicator(working, node.Spec!);
            return ExtractOutput(result, node.Spec!);
        }

        private List<double> ResolveCombine(SeriesNode node)
        {
            var left = Resolve(node.LeftId!.Value);
            var right = Resolve(node.RightId!.Value);
            var combined = new List<double>(_data.Count);
            var count = _data.Count;
            for (var i = 0; i < count; i++)
            {
                var l = i < left.Count ? left[i] : double.NaN;
                var r = i < right.Count ? right[i] : double.NaN;
                combined.Add(ApplyCombine(node.CombineOp, l, r));
            }

            return combined;
        }

        private static StockData ApplyIndicator(StockData data, IndicatorSpec spec)
        {
            return spec.Name switch
            {
                IndicatorName.SimpleMovingAverage => data.CalculateSimpleMovingAverage(((SmaOptions)spec.Options).Length),
                IndicatorName.RelativeStrengthIndex => data.CalculateRelativeStrengthIndex(length: ((RsiOptions)spec.Options).Length),
                IndicatorName.MovingAverageConvergenceDivergence => data.CalculateMovingAverageConvergenceDivergence(
                    fastLength: ((MacdOptions)spec.Options).FastLength,
                    slowLength: ((MacdOptions)spec.Options).SlowLength,
                    signalLength: ((MacdOptions)spec.Options).SignalLength),
                _ => throw new NotSupportedException($"Indicator '{spec.Name}' not wired in this sketch.")
            };
        }

        private static List<double> ExtractOutput(StockData result, IndicatorSpec spec)
        {
            if (spec.Name == IndicatorName.MovingAverageConvergenceDivergence && spec.OutputKey.HasValue)
            {
                var key = GetMacdKey(spec.OutputKey.Value);
                if (result.OutputValues.TryGetValue(key, out var list))
                {
                    return new List<double>(list);
                }
            }

            return new List<double>(result.CustomValuesList);
        }

        private static string GetMacdKey(MacdOutputKey key)
        {
            return key switch
            {
                MacdOutputKey.Macd => "Macd",
                MacdOutputKey.Signal => "Signal",
                MacdOutputKey.Histogram => "Histogram",
                _ => "Macd"
            };
        }

        private static double ApplyCombine(CombineOp op, double left, double right)
        {
            return op switch
            {
                CombineOp.Add => left + right,
                CombineOp.Subtract => left - right,
                CombineOp.Multiply => left * right,
                CombineOp.Divide => left / right,
                _ => left
            };
        }

        private static StockData CloneWithCustomValues(StockData baseData, List<double> customValues)
        {
            var clone = new StockData(baseData.TickerDataList, baseData.InputName)
            {
                Options = baseData.Options,
                CustomValuesList = customValues
            };
            return clone;
        }
    }

    private sealed class StreamingIndicatorBuilder
    {
        private readonly string _symbol;
        private readonly BarTimeframe _timeframe;
        private readonly List<StreamingStep> _steps;

        private StreamingIndicatorBuilder(string symbol, BarTimeframe timeframe)
        {
            _symbol = symbol;
            _timeframe = timeframe;
            _steps = new List<StreamingStep>();
        }

        public static StreamingIndicatorBuilder For(string symbol, BarTimeframe timeframe)
        {
            return new StreamingIndicatorBuilder(symbol, timeframe);
        }

        public StreamingIndicatorBuilder Add(IndicatorSpec spec, IndicatorSubscriptionOptions? options = null)
        {
            _steps.Add(new StreamingStep(spec, options));
            return this;
        }

        public StreamingIndicatorResult Build()
        {
            return new StreamingIndicatorResult(_symbol, _timeframe, _steps);
        }
    }

    private sealed class StreamingIndicatorResult
    {
        private readonly string _symbol;
        private readonly BarTimeframe _timeframe;
        private readonly IReadOnlyList<StreamingStep> _steps;

        public StreamingIndicatorResult(string symbol, BarTimeframe timeframe, IReadOnlyList<StreamingStep> steps)
        {
            _symbol = symbol;
            _timeframe = timeframe;
            _steps = steps;
        }

        public List<StreamingIndicatorStateUpdate> Run(IStreamSource source, StreamingOptions options)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var updates = new List<StreamingIndicatorStateUpdate>();
            var symbols = options.Symbols ?? new[] { _symbol };
            using var session = StreamingSession.Create(source, symbols, options: options);
            for (var i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                var state = StreamingIndicatorFactory.CreateState(step.Spec);
                session.RegisterStatefulIndicator(_symbol, _timeframe, state, update => updates.Add(update), step.Options);
            }

            session.Start();
            return updates;
        }
    }

    private sealed class StreamingIndicatorFactory
    {
        public static IStreamingIndicatorState CreateState(IndicatorSpec spec)
        {
            return spec.Name switch
            {
                IndicatorName.SimpleMovingAverage => new SimpleMovingAverageState(((SmaOptions)spec.Options).Length),
                IndicatorName.RelativeStrengthIndex => new RelativeStrengthIndexState(((RsiOptions)spec.Options).Length),
                IndicatorName.MovingAverageConvergenceDivergence => new MovingAverageConvergenceDivergenceState(
                    ((MacdOptions)spec.Options).FastLength,
                    ((MacdOptions)spec.Options).SlowLength,
                    ((MacdOptions)spec.Options).SignalLength),
                _ => throw new NotSupportedException($"Indicator '{spec.Name}' not wired in this sketch.")
            };
        }
    }

    private sealed record IndicatorSpec(IndicatorName Name, ISketchIndicatorOptions Options, MacdOutputKey? OutputKey = null);

    private static class IndicatorSpecs
    {
        public static IndicatorSpec Sma(int length)
        {
            return new IndicatorSpec(IndicatorName.SimpleMovingAverage, new SmaOptions(length));
        }

        public static IndicatorSpec Rsi(int length)
        {
            return new IndicatorSpec(IndicatorName.RelativeStrengthIndex, new RsiOptions(length));
        }

        public static IndicatorSpec Macd(MacdOutputKey outputKey, int fastLength = 12, int slowLength = 26, int signalLength = 9)
        {
            return new IndicatorSpec(IndicatorName.MovingAverageConvergenceDivergence,
                new MacdOptions(fastLength, slowLength, signalLength), outputKey);
        }
    }

    private interface ISketchIndicatorOptions
    {
    }

    private sealed class SmaOptions : ISketchIndicatorOptions
    {
        public SmaOptions(int length)
        {
            Length = Math.Max(1, length);
        }

        public int Length { get; }
    }

    private sealed class RsiOptions : ISketchIndicatorOptions
    {
        public RsiOptions(int length)
        {
            Length = Math.Max(1, length);
        }

        public int Length { get; }
    }

    private sealed class MacdOptions : ISketchIndicatorOptions
    {
        public MacdOptions(int fastLength, int slowLength, int signalLength)
        {
            FastLength = Math.Max(1, fastLength);
            SlowLength = Math.Max(1, slowLength);
            SignalLength = Math.Max(1, signalLength);
        }

        public int FastLength { get; }
        public int SlowLength { get; }
        public int SignalLength { get; }
    }

    private sealed class SeriesNode
    {
        private SeriesNode(SeriesNodeKind kind, SeriesId? inputId, IndicatorSpec? spec, SeriesId? leftId, SeriesId? rightId, CombineOp combineOp)
        {
            Kind = kind;
            InputId = inputId;
            Spec = spec;
            LeftId = leftId;
            RightId = rightId;
            CombineOp = combineOp;
        }

        public SeriesNodeKind Kind { get; }
        public SeriesId? InputId { get; }
        public IndicatorSpec? Spec { get; }
        public SeriesId? LeftId { get; }
        public SeriesId? RightId { get; }
        public CombineOp CombineOp { get; }

        public static SeriesNode Base()
        {
            return new SeriesNode(SeriesNodeKind.Base, null, null, null, null, CombineOp.Add);
        }

        public static SeriesNode Indicator(SeriesId inputId, IndicatorSpec spec)
        {
            return new SeriesNode(SeriesNodeKind.Indicator, inputId, spec, null, null, CombineOp.Add);
        }

        public static SeriesNode Combine(SeriesId leftId, SeriesId rightId, CombineOp op)
        {
            return new SeriesNode(SeriesNodeKind.Combine, null, null, leftId, rightId, op);
        }
    }

    private readonly struct StreamingStep
    {
        public StreamingStep(IndicatorSpec spec, IndicatorSubscriptionOptions? options)
        {
            Spec = spec;
            Options = options;
        }

        public IndicatorSpec Spec { get; }
        public IndicatorSubscriptionOptions? Options { get; }
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
        Combine
    }

    private enum SeriesId
    {
        Price,
        Sma20,
        Rsi14,
        RsiSma5,
        SmaMinusRsi,
        Macd,
        MacdSignal,
        MacdHistogram
    }

    private enum CombineOp
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    private enum MacdOutputKey
    {
        Macd,
        Signal,
        Histogram
    }
}
