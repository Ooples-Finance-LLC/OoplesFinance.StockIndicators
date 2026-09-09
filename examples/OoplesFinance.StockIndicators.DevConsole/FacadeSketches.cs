using System;
using System.Collections.Generic;
using System.Globalization;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

internal static class FacadeSketches
{
    private const string Symbol = "AAPL";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("Facade/builder sketches:");
        RunBatchChainSketch();
        RunNamedOutputsSketch();
        RunStreamingSketch();
    }

    private static void RunBatchChainSketch()
    {
        Console.WriteLine();
        Console.WriteLine("Sketch 1: chained batch pipeline");

        var data = BuildSampleData(160, 100d, new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc));
        var stockData = new StockData(data);
        var plan = new IndicatorFacadePlan(stockData);

        var price = plan.Base;
        var sma20 = plan.Indicator("Sma20", price, d => d.CalculateSimpleMovingAverage(20));
        var rsi14 = plan.Indicator("Rsi14", price, d => d.CalculateRelativeStrengthIndex(length: 14));
        var rsiSmooth = plan.Indicator("RsiSma5", rsi14, d => d.CalculateSimpleMovingAverage(5));
        var spread = plan.Combine("SmaMinusRsi", sma20, rsi14, (a, b) => a - b);

        var result = plan.Run(sma20, rsi14, rsiSmooth, spread);
        PrintLast(result, "Sma20", "SMA(20)");
        PrintLast(result, "Rsi14", "RSI(14)");
        PrintLast(result, "RsiSma5", "RSI SMA(5)");
        PrintLast(result, "SmaMinusRsi", "SMA - RSI");
    }

    private static void RunNamedOutputsSketch()
    {
        Console.WriteLine();
        Console.WriteLine("Sketch 2: named outputs + combine");

        var data = BuildSampleData(160, 95d, new DateTime(2024, 2, 2, 9, 30, 0, DateTimeKind.Utc));
        var stockData = new StockData(data);
        var plan = new IndicatorFacadePlan(stockData);

        var price = plan.Base;
        var macd = plan.Indicator("Macd", price, d => d.CalculateMovingAverageConvergenceDivergence());
        var macdSignal = plan.Indicator("MacdSignal", price,
            d => d.CalculateMovingAverageConvergenceDivergence(), outputKey: "Signal");
        var histogram = plan.Combine("MacdHistogram", macd, macdSignal, (m, s) => m - s);
        var macdSma = plan.Indicator("MacdSma5", macd, d => d.CalculateSimpleMovingAverage(5));

        var result = plan.Run(macd, macdSignal, histogram, macdSma);
        PrintLast(result, "Macd", "MACD");
        PrintLast(result, "MacdSignal", "MACD Signal");
        PrintLast(result, "MacdHistogram", "MACD Histogram");
        PrintLast(result, "MacdSma5", "MACD SMA(5)");
    }

    private static void RunStreamingSketch()
    {
        Console.WriteLine();
        Console.WriteLine("Sketch 3: streaming facade");

        var trades = BuildTradeEvents(Symbol, 12);
        var source = new ReplayStreamSource(trades);
        var streamingOptions = new StreamingOptions
        {
            Symbols = new[] { Symbol },
            SubscribeTrades = true,
            SubscribeBars = false,
            SubscribeQuotes = false,
            UpdatePolicy = StreamingUpdatePolicy.FinalOnly,
            ProcessingMode = StreamingProcessingMode.Inline
        };

        var plan = new StreamingFacadePlan(Symbol, BarTimeframe.Tick)
            .Add(new SimpleMovingAverageState(5),
                new IndicatorSubscriptionOptions { IncludeUpdates = false, IncludeOutputValues = true })
            .Add(new RelativeStrengthIndexState(),
                new IndicatorSubscriptionOptions { IncludeUpdates = false, IncludeOutputValues = false });

        var updates = plan.Run(source, streamingOptions);
        Console.WriteLine($"Streaming updates = {updates.Count}");
        if (updates.Count > 0)
        {
            var last = updates[updates.Count - 1];
            Console.WriteLine($"{last.Indicator} last = {FormatValue(last.Value)}");
        }
    }

    private static void PrintLast(IndicatorResult result, string seriesName, string label)
    {
        if (!result.TryGetSeries(seriesName, out var values))
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
        var random = new Random(123);
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
        var start = new DateTime(2024, 3, 1, 9, 30, 0, DateTimeKind.Utc);
        var price = 100d;
        for (var i = 0; i < count; i++)
        {
            price += i % 2 == 0 ? 0.6 : -0.2;
            trades.Add(new StreamTrade(symbol, start.AddSeconds(i), price, 1));
        }

        return trades;
    }

    private sealed class IndicatorFacadePlan
    {
        private readonly StockData _baseData;
        private readonly Dictionary<string, SeriesNode> _nodes;
        private readonly string _baseName;

        public IndicatorFacadePlan(StockData baseData)
        {
            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _nodes = new Dictionary<string, SeriesNode>(StringComparer.OrdinalIgnoreCase);
            _baseName = "Price";
            _nodes[_baseName] = SeriesNode.Base();
        }

        public SeriesRef Base => new SeriesRef(_baseName);

        public SeriesRef Indicator(string name, SeriesRef input, Func<StockData, StockData> indicator, string? outputKey = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (indicator == null)
            {
                throw new ArgumentNullException(nameof(indicator));
            }

            _nodes[name] = SeriesNode.CreateIndicator(input.Name, indicator, outputKey);
            return new SeriesRef(name);
        }

        public SeriesRef Combine(string name, SeriesRef left, SeriesRef right, Func<double, double, double> combiner)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (combiner == null)
            {
                throw new ArgumentNullException(nameof(combiner));
            }

            _nodes[name] = SeriesNode.Combine(left.Name, right.Name, combiner);
            return new SeriesRef(name);
        }

        public IndicatorResult Run(params SeriesRef[] outputs)
        {
            var context = new SeriesContext(_baseData, _nodes, _baseName);
            var results = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

            if (outputs == null || outputs.Length == 0)
            {
                foreach (var entry in _nodes)
                {
                    if (string.Equals(entry.Key, _baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results[entry.Key] = context.Resolve(entry.Key);
                }
            }
            else
            {
                for (var i = 0; i < outputs.Length; i++)
                {
                    var output = outputs[i];
                    if (output == null)
                    {
                        continue;
                    }

                    results[output.Name] = context.Resolve(output.Name);
                }
            }

            return new IndicatorResult(results);
        }
    }

    private sealed class SeriesContext
    {
        private readonly StockData _baseData;
        private readonly Dictionary<string, SeriesNode> _nodes;
        private readonly Dictionary<string, List<double>> _cache;
        private readonly string _baseName;

        public SeriesContext(StockData baseData, Dictionary<string, SeriesNode> nodes, string baseName)
        {
            _baseData = baseData;
            _nodes = nodes;
            _cache = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            _baseName = baseName;
        }

        public List<double> Resolve(string name)
        {
            return Resolve(name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private List<double> Resolve(string name, HashSet<string> visiting)
        {
            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(name))
            {
                throw new InvalidOperationException($"Cycle detected in series graph at '{name}'.");
            }

            if (!_nodes.TryGetValue(name, out var node))
            {
                throw new InvalidOperationException($"Unknown series '{name}'.");
            }

            List<double> resolved;
            switch (node.Kind)
            {
                case SeriesNodeKind.Base:
                    resolved = GetBaseInput();
                    break;
                case SeriesNodeKind.Indicator:
                    resolved = ResolveIndicator(node, visiting);
                    break;
                case SeriesNodeKind.Combine:
                    resolved = ResolveCombine(node, visiting);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node kind for '{name}'.");
            }

            visiting.Remove(name);
            _cache[name] = resolved;
            return resolved;
        }

        private List<double> GetBaseInput()
        {
            var input = _baseData.CustomValuesList.Count > 0 ? _baseData.CustomValuesList : _baseData.InputValues;
            return new List<double>(input);
        }

        private List<double> ResolveIndicator(SeriesNode node, HashSet<string> visiting)
        {
            var input = Resolve(node.InputName!, visiting);
            var working = CloneWithCustomValues(_baseData, input);
            var result = node.Indicator!(working);

            if (node.OutputKey == null)
            {
                return new List<double>(result.CustomValuesList);
            }

            if (!result.OutputValues.TryGetValue(node.OutputKey, out var list))
            {
                throw new InvalidOperationException($"Output key '{node.OutputKey}' not found.");
            }

            return new List<double>(list);
        }

        private List<double> ResolveCombine(SeriesNode node, HashSet<string> visiting)
        {
            var left = Resolve(node.LeftName!, visiting);
            var right = Resolve(node.RightName!, visiting);
            var combined = new List<double>(_baseData.Count);
            var count = _baseData.Count;
            for (var i = 0; i < count; i++)
            {
                var l = i < left.Count ? left[i] : double.NaN;
                var r = i < right.Count ? right[i] : double.NaN;
                combined.Add(node.Combiner!(l, r));
            }

            return combined;
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

    private sealed class IndicatorResult
    {
        private readonly Dictionary<string, List<double>> _outputs;

        public IndicatorResult(Dictionary<string, List<double>> outputs)
        {
            _outputs = outputs;
        }

        public IReadOnlyDictionary<string, List<double>> Outputs => _outputs;

        public bool TryGetSeries(string name, out List<double> values)
        {
            return _outputs.TryGetValue(name, out values!);
        }
    }

    private sealed class SeriesRef
    {
        public SeriesRef(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private sealed class SeriesNode
    {
        private SeriesNode(SeriesNodeKind kind, string? inputName, Func<StockData, StockData>? indicator, string? outputKey,
            string? leftName, string? rightName, Func<double, double, double>? combiner)
        {
            Kind = kind;
            InputName = inputName;
            Indicator = indicator;
            OutputKey = outputKey;
            LeftName = leftName;
            RightName = rightName;
            Combiner = combiner;
        }

        public SeriesNodeKind Kind { get; }
        public string? InputName { get; }
        public Func<StockData, StockData>? Indicator { get; }
        public string? OutputKey { get; }
        public string? LeftName { get; }
        public string? RightName { get; }
        public Func<double, double, double>? Combiner { get; }

        public static SeriesNode Base()
        {
            return new SeriesNode(SeriesNodeKind.Base, null, null, null, null, null, null);
        }

        public static SeriesNode CreateIndicator(string inputName, Func<StockData, StockData> indicator, string? outputKey)
        {
            return new SeriesNode(SeriesNodeKind.Indicator, inputName, indicator, outputKey, null, null, null);
        }

        public static SeriesNode Combine(string leftName, string rightName, Func<double, double, double> combiner)
        {
            return new SeriesNode(SeriesNodeKind.Combine, null, null, null, leftName, rightName, combiner);
        }
    }

    private enum SeriesNodeKind
    {
        Base,
        Indicator,
        Combine
    }

    private sealed class StreamingFacadePlan
    {
        private readonly string _symbol;
        private readonly BarTimeframe _timeframe;
        private readonly List<StreamingStep> _steps;

        public StreamingFacadePlan(string symbol, BarTimeframe timeframe)
        {
            _symbol = symbol;
            _timeframe = timeframe;
            _steps = new List<StreamingStep>();
        }

        public StreamingFacadePlan Add(IStreamingIndicatorState state, IndicatorSubscriptionOptions? options = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _steps.Add(new StreamingStep(state, options));
            return this;
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
                session.RegisterStatefulIndicator(_symbol, _timeframe, step.State,
                    update => updates.Add(update), step.Options);
            }

            session.Start();
            return updates;
        }

        private readonly struct StreamingStep
        {
            public StreamingStep(IStreamingIndicatorState state, IndicatorSubscriptionOptions? options)
            {
                State = state;
                Options = options;
            }

            public IStreamingIndicatorState State { get; }
            public IndicatorSubscriptionOptions? Options { get; }
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
}
