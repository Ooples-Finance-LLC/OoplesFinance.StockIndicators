using System.Globalization;
using System.Threading;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

internal static class Program
{
    private const string PrimarySymbol = "AAPL";
    private const string SecondarySymbol = "MSFT";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static void Main(string[] args)
    {
        Console.WriteLine("OoplesFinance.StockIndicators Dev Console");
        Console.WriteLine("========================================");

        if (TryRunFromArgs(args))
        {
            return;
        }

        while (true)
        {
            PrintMenu();
            var input = Console.ReadLine();
            if (input == null)
            {
                return;
            }

            var choice = input.Trim().ToLowerInvariant();
            switch (choice)
            {
                case "1":
                    RunBatchExample();
                    break;
                case "2":
                    RunStreamingExample();
                    break;
                case "3":
                    RunMultiSeriesExample();
                    break;
                case "4":
                    RunComplexSetupExample();
                    break;
                case "5":
                    RunSessionExample();
                    break;
                case "6":
                    RunFacadeSketches();
                    break;
                case "7":
                    RunFacadeSketchesV2();
                    break;
                case "8":
                    RunFacadeSketchesV3();
                    break;
                case "9":
                    RunFacadeSketchesV4();
                    break;
                case "10":
                    RunFacadeSketchesV5();
                    break;
                case "11":
                    RunFacadeSketchesV6();
                    break;
                case "a":
                    RunBatchExample();
                    RunStreamingExample();
                    RunMultiSeriesExample();
                    RunComplexSetupExample();
                    RunSessionExample();
                    RunFacadeSketches();
                    RunFacadeSketchesV2();
                    RunFacadeSketchesV3();
                    RunFacadeSketchesV4();
                    RunFacadeSketchesV5();
                    RunFacadeSketchesV6();
                    break;
                case "q":
                    return;
                default:
                    Console.WriteLine("Unknown choice.");
                    break;
            }

            Pause();
        }
    }

    private static void PrintMenu()
    {
        Console.WriteLine();
        Console.WriteLine("Select an example:");
        Console.WriteLine("1) Batch calculations (SMA/RSI/MACD)");
        Console.WriteLine("2) Streaming engine (single-series)");
        Console.WriteLine("3) Streaming engine (multi-series)");
        Console.WriteLine("4) Complex streaming setup (multi indicator/timeframe)");
        Console.WriteLine("5) Streaming session (replay source)");
        Console.WriteLine("6) Facade/builder sketches");
        Console.WriteLine("7) Facade/builder sketches (v2)");
        Console.WriteLine("8) Facade/builder sketches (v3)");
        Console.WriteLine("9) Facade/builder sketches (v4)");
        Console.WriteLine("10) Facade/builder sketches (v5)");
        Console.WriteLine("11) Facade/builder sketches (v6)");
        Console.WriteLine("A) Run all");
        Console.WriteLine("Q) Quit");
        Console.Write("> ");
    }

    private static bool TryRunFromArgs(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return false;
        }

        var runAll = false;
        var noPause = false;
        string? example = null;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--run-all", StringComparison.OrdinalIgnoreCase))
            {
                runAll = true;
            }
            else if (string.Equals(arg, "--no-pause", StringComparison.OrdinalIgnoreCase))
            {
                noPause = true;
            }
            else if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
            }
            else if (string.Equals(arg, "--example", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                example = args[++i];
            }
            else if (arg.StartsWith("--example=", StringComparison.OrdinalIgnoreCase))
            {
                example = arg.Substring("--example=".Length);
            }
        }

        if (showHelp)
        {
            PrintUsage();
            return true;
        }

        if (runAll)
        {
            RunBatchExample();
            RunStreamingExample();
            RunMultiSeriesExample();
            RunComplexSetupExample();
            RunSessionExample();
            RunFacadeSketches();
            RunFacadeSketchesV2();
            RunFacadeSketchesV3();
            RunFacadeSketchesV4();
            RunFacadeSketchesV5();
            RunFacadeSketchesV6();
            if (!noPause)
            {
                Pause();
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(example))
        {
            if (!RunExampleByName(example))
            {
                Console.WriteLine("Unknown example.");
                PrintUsage();
            }
            else if (!noPause)
            {
                Pause();
            }

            return true;
        }

        PrintUsage();
        return true;
    }

    private static bool RunExampleByName(string example)
    {
        switch (example.Trim().ToLowerInvariant())
        {
            case "batch":
                RunBatchExample();
                return true;
            case "streaming":
                RunStreamingExample();
                return true;
            case "multi-series":
            case "multiseries":
                RunMultiSeriesExample();
                return true;
            case "complex":
                RunComplexSetupExample();
                return true;
            case "session":
                RunSessionExample();
                return true;
            case "facade":
            case "sketch":
            case "sketches":
                RunFacadeSketches();
                return true;
            case "facade-v2":
            case "sketch-v2":
            case "sketches-v2":
                RunFacadeSketchesV2();
                return true;
            case "facade-v3":
            case "sketch-v3":
            case "sketches-v3":
                RunFacadeSketchesV3();
                return true;
            case "facade-v4":
            case "sketch-v4":
            case "sketches-v4":
                RunFacadeSketchesV4();
                return true;
            case "facade-v5":
            case "sketch-v5":
            case "sketches-v5":
                RunFacadeSketchesV5();
                return true;
            case "facade-v6":
            case "sketch-v6":
            case "sketches-v6":
                RunFacadeSketchesV6();
                return true;
            default:
                return false;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  --run-all           Run all examples and exit");
        Console.WriteLine("  --example <name>    Run one example (batch, streaming, multi-series, complex, session, facade, facade-v2, facade-v3, facade-v4, facade-v5, facade-v6)");
        Console.WriteLine("  --no-pause          Skip pause after running");
    }

    private static void RunBatchExample()
    {
        Console.WriteLine();
        Console.WriteLine("Batch example:");

        var data = BuildSampleData(200, 100d, new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc));
        var stockData = new StockData(data);

        var sma = stockData.CalculateSimpleMovingAverage(20).CustomValuesList;
        var rsi = stockData.CalculateRelativeStrengthIndex(length: 14).CustomValuesList;
        var macd = stockData.CalculateMovingAverageConvergenceDivergence().CustomValuesList;

        Console.WriteLine($"SMA(20) last = {FormatValue(LastValue(sma))}");
        Console.WriteLine($"RSI(14) last = {FormatValue(LastValue(rsi))}");
        Console.WriteLine($"MACD last = {FormatValue(LastValue(macd))}");
    }

    private static void RunStreamingExample()
    {
        Console.WriteLine();
        Console.WriteLine("Streaming example (single-series):");

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<StreamingIndicatorStateUpdate>();
        engine.RegisterStatefulIndicator(
            PrimarySymbol,
            BarTimeframe.Tick,
            new SimpleMovingAverageState(5),
            update => updates.Add(update),
            new IndicatorSubscriptionOptions { IncludeUpdates = false, IncludeOutputValues = true });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        var price = 100d;
        for (var i = 0; i < 12; i++)
        {
            price += i % 2 == 0 ? 0.6 : -0.2;
            engine.OnTrade(new StreamTrade(PrimarySymbol, start.AddSeconds(i), price, 1));
        }

        Console.WriteLine($"Updates received = {updates.Count}");
        if (updates.Count > 0)
        {
            var last = updates[updates.Count - 1];
            Console.WriteLine($"SMA(5) last = {FormatValue(last.Value)}");
        }
    }

    private static void RunMultiSeriesExample()
    {
        Console.WriteLine();
        Console.WriteLine("Streaming example (multi-series):");

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var primary = new SeriesKey(PrimarySymbol, BarTimeframe.Minutes(1));
        var secondary = new SeriesKey(SecondarySymbol, BarTimeframe.Minutes(1));
        var updates = new List<MultiSeriesIndicatorStateUpdate>();
        var options = new IndicatorSubscriptionOptions
        {
            IncludeUpdates = false,
            SeriesAlignmentPolicy = SeriesAlignmentPolicy.Strict
        };

        engine.RegisterMultiSeriesIndicator(primary, new[] { secondary }, new SpreadState(primary, secondary),
            update => updates.Add(update), options);

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        EmitBar(engine, SecondarySymbol, start, 99d);
        EmitBar(engine, PrimarySymbol, start, 101d);
        EmitBar(engine, SecondarySymbol, start.AddMinutes(1), 100d);
        EmitBar(engine, PrimarySymbol, start.AddMinutes(1), 102d);

        Console.WriteLine($"Updates received = {updates.Count}");
        if (updates.Count > 0)
        {
            var last = updates[updates.Count - 1];
            Console.WriteLine($"Spread last = {FormatValue(last.Value)}");
        }
    }

    private static void RunComplexSetupExample()
    {
        Console.WriteLine();
        Console.WriteLine("Complex setup example:");

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<StreamingIndicatorStateUpdate>();
        var options = new IndicatorSubscriptionOptions { IncludeUpdates = false };

        engine.RegisterStatefulIndicator(PrimarySymbol, BarTimeframe.Minutes(1),
            new SimpleMovingAverageState(10), update => updates.Add(update), options);
        engine.RegisterStatefulIndicator(PrimarySymbol, BarTimeframe.Minutes(1),
            new RelativeStrengthIndexState(), update => updates.Add(update), options);
        engine.RegisterStatefulIndicator(PrimarySymbol, BarTimeframe.Minutes(5),
            new ExponentialMovingAverageState(5), update => updates.Add(update), options);

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        for (var i = 0; i < 15; i++)
        {
            EmitBar(engine, PrimarySymbol, start.AddMinutes(i), 100d + i);
        }

        for (var i = 0; i < 3; i++)
        {
            EmitBar(engine, PrimarySymbol, start.AddMinutes(i * 5), 100d + (i * 5), BarTimeframe.Minutes(5));
        }

        var counts = CountUpdates(updates);
        Console.WriteLine($"Total updates = {updates.Count}");
        foreach (var entry in counts)
        {
            Console.WriteLine($"{entry.Key} = {entry.Value}");
        }
    }

    private static void RunSessionExample()
    {
        Console.WriteLine();
        Console.WriteLine("Streaming session example (replay source):");        

        var events = BuildTradeEvents(PrimarySymbol, SecondarySymbol, 8);
        var source = new ReplayStreamSource(events, TimeSpan.Zero);
        var options = new StreamingOptions
        {
            Symbols = new[] { PrimarySymbol, SecondarySymbol },
            SubscribeTrades = true,
            SubscribeQuotes = false,
            SubscribeBars = false,
            UpdatePolicy = StreamingUpdatePolicy.FinalOnly,
            ProcessingMode = StreamingProcessingMode.Inline
        };

        using var session = StreamingSession.Create(source, options.Symbols, options: options);
        var updates = new List<StreamingIndicatorStateUpdate>();
        session.RegisterStatefulIndicator(PrimarySymbol, BarTimeframe.Tick, new SimpleMovingAverageState(3),
            update => updates.Add(update));

        session.Start();

        Console.WriteLine($"Updates received = {updates.Count}");
        if (updates.Count > 0)
        {
            var last = updates[updates.Count - 1];
            Console.WriteLine($"SMA(3) last = {FormatValue(last.Value)}");
        }
    }

    private static void RunFacadeSketches()
    {
        FacadeSketches.Run();
    }

    private static void RunFacadeSketchesV2()
    {
        FacadeSketchesV2.Run();
    }

    private static void RunFacadeSketchesV3()
    {
        FacadeSketchesV3.Run();
    }

    private static void RunFacadeSketchesV4()
    {
        FacadeSketchesV4.Run();
    }

    private static void RunFacadeSketchesV5()
    {
        FacadeSketchesV5.Run();
    }

    private static void RunFacadeSketchesV6()
    {
        FacadeSketchesV6.Run();
    }

    private static List<TickerData> BuildSampleData(int count, double startPrice, DateTime start)
    {
        var data = new List<TickerData>(count);
        var random = new Random(42);
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

            var volume = 1000d + (random.NextDouble() * 100d);

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

    private static List<ReplayStreamEvent> BuildTradeEvents(string symbolA, string symbolB, int count)
    {
        var events = new List<ReplayStreamEvent>(count * 2);
        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        var priceA = 100d;
        var priceB = 200d;

        for (var i = 0; i < count; i++)
        {
            priceA += 0.4d;
            priceB -= 0.2d;
            events.Add(ReplayStreamEvent.FromTrade(new StreamTrade(symbolA, start.AddSeconds(i), priceA, 1)));
            events.Add(ReplayStreamEvent.FromTrade(new StreamTrade(symbolB, start.AddSeconds(i), priceB, 1)));
        }

        return events;
    }

    private static void EmitBar(StreamingIndicatorEngine engine, string symbol, DateTime start, double price,
        BarTimeframe? timeframe = null)
    {
        var resolved = timeframe ?? BarTimeframe.Minutes(1);
        var end = resolved.IsTick ? start : start.Add(resolved.ToTimeSpan());
        var bar = new OhlcvBar(symbol, resolved, start, end, price, price, price, price, 100d, true);
        engine.OnBar(bar);
    }

    private static Dictionary<string, int> CountUpdates(List<StreamingIndicatorStateUpdate> updates)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < updates.Count; i++)
        {
            var update = updates[i];
            var key = $"{update.Indicator}:{update.Timeframe}";
            if (counts.TryGetValue(key, out var count))
            {
                counts[key] = count + 1;
            }
            else
            {
                counts[key] = 1;
            }
        }

        return counts;
    }

    private static double LastValue(List<double> values)
    {
        return values.Count == 0 ? double.NaN : values[values.Count - 1];
    }

    private static string FormatValue(double value)
    {
        return double.IsNaN(value) ? "NaN" : value.ToString("F4", Invariant);
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press ENTER to continue...");
        Console.ReadLine();
    }

    private sealed class SpreadState : IMultiSeriesIndicatorState
    {
        private readonly SeriesKey _left;
        private readonly SeriesKey _right;

        public SpreadState(SeriesKey left, SeriesKey right)
        {
            _left = left;
            _right = right;
        }

        public IndicatorName Name => IndicatorName.None;

        public void Reset()
        {
        }

        public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
            bool isFinal, bool includeOutputs)
        {
            if (!context.TryGetLatest(_left, out var leftBar) || !context.TryGetLatest(_right, out var rightBar))
            {
                return new MultiSeriesIndicatorStateResult(false, 0d, null);
            }

            var value = leftBar.Close - rightBar.Close;
            IReadOnlyDictionary<string, double>? outputs = null;
            if (includeOutputs)
            {
                outputs = new Dictionary<string, double>
                {
                    ["Spread"] = value
                };
            }

            return new MultiSeriesIndicatorStateResult(true, value, outputs);
        }
    }

    private sealed class ReplayStreamSource : IStreamSource
    {
        private readonly IReadOnlyList<ReplayStreamEvent> _events;
        private readonly TimeSpan _delay;

        public ReplayStreamSource(IReadOnlyList<ReplayStreamEvent> events, TimeSpan delay)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _delay = delay;
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

            return new ReplayStreamSubscription(_events, _delay, request, observer);
        }

        private sealed class ReplayStreamSubscription : IStreamSubscription
        {
            private readonly IReadOnlyList<ReplayStreamEvent> _events;
            private readonly TimeSpan _delay;
            private readonly StreamSubscriptionRequest _request;
            private readonly IStreamObserver _observer;
            private bool _stopped;
            private bool _started;

            public ReplayStreamSubscription(IReadOnlyList<ReplayStreamEvent> events, TimeSpan delay,
                StreamSubscriptionRequest request, IStreamObserver observer)
            {
                _events = events;
                _delay = delay;
                _request = request;
                _observer = observer;
            }

            public void Start()
            {
                if (_started)
                {
                    return;
                }

                _started = true;
                for (var i = 0; i < _events.Count; i++)
                {
                    if (_stopped)
                    {
                        break;
                    }

                    var streamEvent = _events[i];
                    if (!ShouldDispatch(streamEvent))
                    {
                        continue;
                    }

                    Dispatch(streamEvent);
                    if (_delay > TimeSpan.Zero)
                    {
                        Thread.Sleep(_delay);
                    }
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

            private bool ShouldDispatch(ReplayStreamEvent streamEvent)
            {
                return streamEvent.Kind switch
                {
                    ReplayStreamEventKind.Trade => _request.Trades,
                    ReplayStreamEventKind.Quote => _request.Quotes,
                    ReplayStreamEventKind.Bar => _request.Bars && MatchesBarTimeframe(streamEvent.Bar!.Timeframe),
                    _ => false
                };
            }

            private bool MatchesBarTimeframe(BarTimeframe timeframe)
            {
                var timeframes = _request.BarTimeframes;
                if (timeframes.Count == 0)
                {
                    return true;
                }

                for (var i = 0; i < timeframes.Count; i++)
                {
                    if (timeframes[i].Equals(timeframe))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void Dispatch(ReplayStreamEvent streamEvent)
            {
                switch (streamEvent.Kind)
                {
                    case ReplayStreamEventKind.Trade:
                        _observer.OnTrade(streamEvent.Trade!);
                        break;
                    case ReplayStreamEventKind.Quote:
                        _observer.OnQuote(streamEvent.Quote!);
                        break;
                    case ReplayStreamEventKind.Bar:
                        _observer.OnBar(streamEvent.Bar!);
                        break;
                }
            }
        }
    }

    private enum ReplayStreamEventKind
    {
        Trade,
        Quote,
        Bar
    }

    private readonly struct ReplayStreamEvent
    {
        private ReplayStreamEvent(ReplayStreamEventKind kind, StreamTrade? trade, StreamQuote? quote, OhlcvBar? bar)
        {
            Kind = kind;
            Trade = trade;
            Quote = quote;
            Bar = bar;
        }

        public ReplayStreamEventKind Kind { get; }
        public StreamTrade? Trade { get; }
        public StreamQuote? Quote { get; }
        public OhlcvBar? Bar { get; }

        public static ReplayStreamEvent FromTrade(StreamTrade trade)
        {
            return new ReplayStreamEvent(ReplayStreamEventKind.Trade, trade, null, null);
        }

        public static ReplayStreamEvent FromQuote(StreamQuote quote)
        {
            return new ReplayStreamEvent(ReplayStreamEventKind.Quote, null, quote, null);
        }

        public static ReplayStreamEvent FromBar(OhlcvBar bar)
        {
            return new ReplayStreamEvent(ReplayStreamEventKind.Bar, null, null, bar);
        }
    }
}
