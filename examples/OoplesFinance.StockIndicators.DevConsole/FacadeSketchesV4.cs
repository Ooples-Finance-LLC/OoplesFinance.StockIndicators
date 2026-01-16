
using System;
using System.Collections.Generic;
using System.Globalization;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

internal static class FacadeSketchesV4
{
    private const string Symbol = "AAPL";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("Facade/builder sketches (v4):");
        RunOptionA();
        RunOptionB();
        RunOptionC();
    }

    private static void RunOptionA()
    {
        Console.WriteLine();
        Console.WriteLine("Option A: Facade + presets + notifications");

        var data = BuildSampleData(160, 101d, new DateTime(2024, 10, 2, 9, 30, 0, DateTimeKind.Utc));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind => ind.UsePreset(IndicatorPreset.Core))
            .ConfigureSignals(sig => sig.UsePreset(SignalPreset.RsiOverbought))
            .ConfigureNotifications(notify =>
            {
                notify.Console();
                notify.Email(new EmailOptions { To = "alerts@example.com" });
                notify.Sms(new SmsOptions { Number = "+1-555-0100" });
            })
            .ConfigureAutoTrading(trade =>
            {
                trade.ConsoleAdapter()
                    .OnSignal(SignalId.RsiOverbought)
                    .MarketBuy();
            })
            .Build();

        runtime.Updated += snapshot =>
        {
            PrintLast(snapshot, IndicatorKey.Sma, "SMA(20)");
            PrintLast(snapshot, IndicatorKey.Rsi, "RSI(14)");
            PrintLast(snapshot, IndicatorKey.Macd, "MACD");
        };

        runtime.Start();
    }

    private static void RunOptionB()
    {
        Console.WriteLine();
        Console.WriteLine("Option B: Presets + advanced chaining");

        var data = BuildSampleData(180, 99d, new DateTime(2024, 11, 2, 9, 30, 0, DateTimeKind.Utc));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle rsi = default;
        SeriesHandle rsiSma = default;
        SeriesHandle spread = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind =>
            {
                ind.UsePreset(IndicatorPreset.Core);
                var sma = ind.Get(IndicatorKey.Sma);
                rsi = ind.Get(IndicatorKey.Rsi);
                rsiSma = ind.Then(rsi).Sma(5);
                spread = ind.Formula(sma, rsi, FormulaOp.Subtract);
            })
            .ConfigureSignals(sig => sig.When(rsi).CrossesAbove(70).Emit(SignalId.RsiOverbought))
            .ConfigureNotifications(notify => notify.Console())
            .Build();

        runtime.Start();

        if (runtime.Latest != null)
        {
            PrintLast(runtime.Latest, rsi, "RSI(14)");
            PrintLast(runtime.Latest, rsiSma, "RSI SMA(5)");
            PrintLast(runtime.Latest, spread, "SMA - RSI");
        }
    }

    private static void RunOptionC()
    {
        Console.WriteLine();
        Console.WriteLine("Option C: Streaming facade + alerts");

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

        SeriesHandle smaFast = default;
        var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromStreaming(stream, options, BarTimeframe.Tick))
            .ConfigureIndicators(ind =>
            {
                var price = ind.Price();
                smaFast = ind.Sma(5, price);
                ind.Rsi(14, price, IndicatorKey.Rsi);
            })
            .ConfigureSignals(sig => sig.When(IndicatorKey.Rsi).CrossesAbove(70).Emit(SignalId.RsiOverbought))
            .ConfigureNotifications(notify =>
            {
                notify.Console();
                notify.Webhook(new WebhookOptions { Url = "https://example.test/alerts" });
            })
            .Build();

        runtime.Updated += snapshot =>
        {
            PrintLast(snapshot, smaFast, "SMA(5)");
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

    private static List<StreamTrade> BuildTradeEvents(string symbol, int count)
    {
        var trades = new List<StreamTrade>(count);
        var start = new DateTime(2024, 12, 1, 9, 30, 0, DateTimeKind.Utc);
        var price = 100d;
        for (var i = 0; i < count; i++)
        {
            price += i % 2 == 0 ? 0.6 : -0.2;
            trades.Add(new StreamTrade(symbol, start.AddSeconds(i), price, 1));
        }

        return trades;
    }
    private sealed class IndicatorDataSource
    {
        private IndicatorDataSource(IndicatorSourceKind kind, StockData? batchData, IStreamSource? streamSource,
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

        public static IndicatorDataSource FromBatch(StockData data)
        {
            return new IndicatorDataSource(IndicatorSourceKind.Batch, data, null, null, BarTimeframe.Tick);
        }

        public static IndicatorDataSource FromStreaming(IStreamSource source, StreamingOptions options, BarTimeframe timeframe)
        {
            return new IndicatorDataSource(IndicatorSourceKind.Streaming, null, source, options, timeframe);
        }
    }

    private enum IndicatorSourceKind
    {
        Batch,
        Streaming
    }

    private sealed class StockIndicatorBuilder
    {
        private readonly IndicatorDataSource _source;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
        private readonly IndicatorCatalog _indicators;
        private readonly SignalCatalog _signals;
        private readonly NotificationCatalog _notifications;
        private readonly AutoTradingCatalog _autoTrading;
        private readonly BehaviorOptions _behavior;
        private int _nextId;
        private readonly SeriesHandle _baseSeries;

        public StockIndicatorBuilder(IndicatorDataSource source)
        {
            _source = source;
            _nodes = new Dictionary<SeriesHandle, SeriesNode>();
            _keys = new Dictionary<IndicatorKey, SeriesHandle>();
            _signals = new SignalCatalog();
            _notifications = new NotificationCatalog();
            _autoTrading = new AutoTradingCatalog();
            _behavior = new BehaviorOptions();
            _nextId = 1;
            _baseSeries = NewHandle();
            _nodes[_baseSeries] = SeriesNode.Base();
            _indicators = new IndicatorCatalog(this, _baseSeries);
        }

        public StockIndicatorBuilder ConfigureIndicators(Action<IndicatorCatalog> configure)
        {
            configure?.Invoke(_indicators);
            return this;
        }

        public StockIndicatorBuilder ConfigureSignals(Action<SignalCatalog> configure)
        {
            configure?.Invoke(_signals);
            return this;
        }

        public StockIndicatorBuilder ConfigureNotifications(Action<NotificationCatalog> configure)
        {
            configure?.Invoke(_notifications);
            return this;
        }

        public StockIndicatorBuilder ConfigureAutoTrading(Action<AutoTradingCatalog> configure)
        {
            configure?.Invoke(_autoTrading);
            return this;
        }

        public StockIndicatorBuilder ConfigureBehavior(Action<BehaviorOptions> configure)
        {
            configure?.Invoke(_behavior);
            return this;
        }

        public IndicatorRuntime Build()
        {
            return new IndicatorRuntime(_source, new Dictionary<SeriesHandle, SeriesNode>(_nodes),
                new Dictionary<IndicatorKey, SeriesHandle>(_keys), _signals.Build(),
                _notifications.Build(), _autoTrading.Build(), _behavior, _baseSeries);
        }

        internal SeriesHandle AddIndicator(IndicatorSpec spec, SeriesHandle input, IndicatorKey? key)
        {
            var handle = NewHandle();
            _nodes[handle] = SeriesNode.Indicator(input, spec);
            if (key.HasValue)
            {
                _keys[key.Value] = handle;
            }

            return handle;
        }

        internal SeriesHandle AddFormula(SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
        {
            var handle = NewHandle();
            _nodes[handle] = SeriesNode.CreateFormula(left, right, formula);
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
        private readonly SeriesHandle _baseSeries;

        public IndicatorCatalog(StockIndicatorBuilder builder, SeriesHandle baseSeries)
        {
            _builder = builder;
            _baseSeries = baseSeries;
        }

        public SeriesHandle Price()
        {
            return _baseSeries;
        }

        public void UsePreset(IndicatorPreset preset)
        {
            if (preset == IndicatorPreset.Core)
            {
                Sma(20, _baseSeries, IndicatorKey.Sma);
                Rsi(14, _baseSeries, IndicatorKey.Rsi);
                Macd(IndicatorOutput.Primary, _baseSeries, IndicatorKey.Macd);
                Macd(IndicatorOutput.Signal, _baseSeries, IndicatorKey.MacdSignal);
                Macd(IndicatorOutput.Histogram, _baseSeries, IndicatorKey.MacdHistogram);
            }
        }

        public SeriesHandle Get(IndicatorKey key)
        {
            return _builder.GetHandle(key);
        }

        public SeriesHandle Sma(int length, SeriesHandle? input = null, IndicatorKey? key = null)
        {
            return _builder.AddIndicator(IndicatorSpecs.Sma(length), input ?? _baseSeries, key);
        }

        public SeriesHandle Rsi(int length, SeriesHandle? input = null, IndicatorKey? key = null)
        {
            return _builder.AddIndicator(IndicatorSpecs.Rsi(length), input ?? _baseSeries, key);
        }

        public SeriesHandle Macd(IndicatorOutput output, SeriesHandle? input = null, IndicatorKey? key = null)
        {
            return _builder.AddIndicator(IndicatorSpecs.Macd(output), input ?? _baseSeries, key);
        }

        public IndicatorChain Then(SeriesHandle input)
        {
            return new IndicatorChain(this, input);
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

        public SeriesHandle Sma(int length, IndicatorKey? key = null)
        {
            return _catalog.Sma(length, _input, key);
        }

        public SeriesHandle Rsi(int length, IndicatorKey? key = null)
        {
            return _catalog.Rsi(length, _input, key);
        }

        public SeriesHandle Macd(IndicatorOutput output, IndicatorKey? key = null)
        {
            return _catalog.Macd(output, _input, key);
        }
    }

    private sealed class BehaviorOptions
    {
        public bool EmitWarmup { get; set; } = true;
    }

    private enum IndicatorPreset
    {
        Core
    }
    private sealed class SignalCatalog
    {
        private readonly List<SignalRule> _rules = new();

        public void UsePreset(SignalPreset preset)
        {
            switch (preset)
            {
                case SignalPreset.RsiOverbought:
                    When(IndicatorKey.Rsi).CrossesAbove(70).Emit(SignalId.RsiOverbought);
                    break;
                case SignalPreset.RsiOversold:
                    When(IndicatorKey.Rsi).CrossesBelow(30).Emit(SignalId.RsiOversold);
                    break;
            }
        }

        public SignalRuleBuilder When(IndicatorKey key)
        {
            return new SignalRuleBuilder(this, SignalSeries.FromKey(key));
        }

        public SignalRuleBuilder When(SeriesHandle handle)
        {
            return new SignalRuleBuilder(this, SignalSeries.FromHandle(handle));
        }

        internal void AddRule(SignalRule rule)
        {
            _rules.Add(rule);
        }

        public IReadOnlyList<SignalRule> Build()
        {
            return new List<SignalRule>(_rules);
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

        public void Emit(SignalId id, string? name = null)
        {
            _catalog.AddRule(new SignalRule(id, name ?? id.ToString(), _series, _trigger, _threshold));
        }
    }

    private sealed class SignalRule
    {
        public SignalRule(SignalId id, string name, SignalSeries series, SignalTrigger trigger, double threshold)
        {
            Id = id;
            Name = name;
            Series = series;
            Trigger = trigger;
            Threshold = threshold;
        }

        public SignalId Id { get; }
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

    private enum SignalPreset
    {
        RsiOverbought,
        RsiOversold
    }

    private enum SignalTrigger
    {
        Above,
        Below,
        CrossesAbove,
        CrossesBelow
    }

    private enum SignalId
    {
        RsiOverbought,
        RsiOversold
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
        public NotificationEvent(SignalId signalId, string name, double value, DateTime timestamp)
        {
            SignalId = signalId;
            Name = name;
            Value = value;
            Timestamp = timestamp;
        }

        public SignalId SignalId { get; }
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

        public AutoTradeRuleBuilder OnSignal(SignalId signalId)
        {
            return new AutoTradeRuleBuilder(_catalog, _adapter, signalId);
        }
    }

    private readonly struct AutoTradeRuleBuilder
    {
        private readonly AutoTradingCatalog _catalog;
        private readonly IAutoTradeAdapter _adapter;
        private readonly SignalId _signalId;

        public AutoTradeRuleBuilder(AutoTradingCatalog catalog, IAutoTradeAdapter adapter, SignalId signalId)
        {
            _catalog = catalog;
            _adapter = adapter;
            _signalId = signalId;
        }

        public AutoTradeAdapterBuilder MarketBuy()
        {
            _catalog.AddRule(new AutoTradeRule(_adapter, _signalId, TradeAction.MarketBuy));
            return new AutoTradeAdapterBuilder(_catalog, _adapter);
        }

        public AutoTradeAdapterBuilder MarketSell()
        {
            _catalog.AddRule(new AutoTradeRule(_adapter, _signalId, TradeAction.MarketSell));
            return new AutoTradeAdapterBuilder(_catalog, _adapter);
        }

        public AutoTradeAdapterBuilder ClosePosition()
        {
            _catalog.AddRule(new AutoTradeRule(_adapter, _signalId, TradeAction.ClosePosition));
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
        public AutoTradeRule(IAutoTradeAdapter adapter, SignalId signalId, TradeAction action)
        {
            Adapter = adapter;
            SignalId = signalId;
            Action = action;
        }

        public IAutoTradeAdapter Adapter { get; }
        public SignalId SignalId { get; }
        public TradeAction Action { get; }
    }

    private interface IAutoTradeAdapter
    {
        void Execute(TradeRequest request);
    }

    private sealed class TradeRequest
    {
        public TradeRequest(SignalId signalId, TradeAction action, DateTime timestamp)
        {
            SignalId = signalId;
            Action = action;
            Timestamp = timestamp;
        }

        public SignalId SignalId { get; }
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
            Console.WriteLine($"Trade: {request.SignalId} {request.Action}");
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
            Console.WriteLine($"Alpaca: {_options.ApiKey} {request.SignalId} {request.Action}");
        }
    }
    private sealed class IndicatorRuntime
    {
        private readonly IndicatorDataSource _source;
        private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
        private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
        private readonly IReadOnlyList<SignalRule> _signals;
        private readonly IReadOnlyList<INotificationChannel> _notifications;
        private readonly AutoTradingConfiguration _autoTrading;
        private readonly BehaviorOptions _behavior;
        private readonly SeriesHandle _baseSeries;
        private readonly bool[] _signalStates;
        private readonly double?[] _signalPrevious;
        private bool _hasEmitted;

        public IndicatorRuntime(IndicatorDataSource source, Dictionary<SeriesHandle, SeriesNode> nodes,
            Dictionary<IndicatorKey, SeriesHandle> keys, IReadOnlyList<SignalRule> signals,
            IReadOnlyList<INotificationChannel> notifications, AutoTradingConfiguration autoTrading,
            BehaviorOptions behavior, SeriesHandle baseSeries)
        {
            _source = source;
            _nodes = nodes;
            _keys = keys;
            _signals = signals;
            _notifications = notifications;
            _autoTrading = autoTrading;
            _behavior = behavior;
            _baseSeries = baseSeries;
            _signalStates = new bool[signals.Count];
            _signalPrevious = new double?[signals.Count];
        }

        public IndicatorSnapshot? Latest { get; private set; }
        public event Action<IndicatorSnapshot>? Updated;

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
                        Dispatch(rule, last);
                    }

                    continue;
                }

                var active = rule.IsActive(last);
                if (active && !_signalStates[i])
                {
                    Dispatch(rule, last);
                }

                _signalStates[i] = active;
                _signalPrevious[i] = last;
            }
        }

        private void Dispatch(SignalRule rule, double value)
        {
            var notification = new NotificationEvent(rule.Id, rule.Name, value, DateTime.UtcNow);
            for (var i = 0; i < _notifications.Count; i++)
            {
                _notifications[i].Notify(notification);
            }

            for (var i = 0; i < _autoTrading.Rules.Count; i++)
            {
                var ruleConfig = _autoTrading.Rules[i];
                if (ruleConfig.SignalId != rule.Id)
                {
                    continue;
                }

                ruleConfig.Adapter.Execute(new TradeRequest(rule.Id, ruleConfig.Action, DateTime.UtcNow));
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

        public static IndicatorKey Sma => new IndicatorKey(IndicatorName.SimpleMovingAverage, IndicatorOutput.Primary);
        public static IndicatorKey Rsi => new IndicatorKey(IndicatorName.RelativeStrengthIndex, IndicatorOutput.Primary);
        public static IndicatorKey Macd => new IndicatorKey(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Primary);
        public static IndicatorKey MacdSignal => new IndicatorKey(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Signal);
        public static IndicatorKey MacdHistogram => new IndicatorKey(IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Histogram);

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
