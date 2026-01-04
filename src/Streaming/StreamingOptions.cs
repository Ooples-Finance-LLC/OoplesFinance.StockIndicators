namespace OoplesFinance.StockIndicators.Streaming;

public enum StreamingUpdatePolicy
{
    IncludeUpdates,
    FinalOnly
}

public enum StreamingProcessingMode
{
    Inline,
    Buffered
}

public enum StreamingBackpressurePolicy
{
    Block,
    DropOldest,
    DropNewest
}

public static class StreamingDefaults
{
    public static readonly IReadOnlyList<BarTimeframe> Timeframes = new[]
    {
        BarTimeframe.Tick,
        BarTimeframe.Seconds(1),
        BarTimeframe.Seconds(5),
        BarTimeframe.Minutes(1),
        BarTimeframe.Minutes(5),
        BarTimeframe.Minutes(15),
        BarTimeframe.Hours(1),
        BarTimeframe.Days(1)
    };

    public const int DefaultMaxPendingMessages = 4096;
}

public sealed class StreamingOptions
{
    public IReadOnlyList<string>? Symbols { get; set; }
    public IReadOnlyList<BarTimeframe>? Timeframes { get; set; }
    public bool? SubscribeTrades { get; set; }
    public bool? SubscribeQuotes { get; set; }
    public bool? SubscribeBars { get; set; }
    public StreamingUpdatePolicy? UpdatePolicy { get; set; }
    public QuotePriceMode? QuotePriceMode { get; set; }
    public OutOfOrderPolicy? OutOfOrderPolicy { get; set; }
    public TimeSpan? ReorderWindow { get; set; }
    public int? MaxBufferSize { get; set; }
    public StreamingProcessingMode? ProcessingMode { get; set; }
    public StreamingBackpressurePolicy? BackpressurePolicy { get; set; }        
    public int? MaxPendingMessages { get; set; }
    public InputName? InputName { get; set; }
    public bool? IncludeOutputValues { get; set; }
    public IndicatorOptions? IndicatorOptions { get; set; }
    public IReadOnlyList<StreamingIndicatorRegistration>? Indicators { get; set; }

    public IReadOnlyList<BarTimeframe> GetTimeframes()
    {
        if (Timeframes != null && Timeframes.Count > 0)
        {
            return Timeframes;
        }

        return StreamingDefaults.Timeframes;
    }

    public StreamingIndicatorEngineOptions CreateEngineOptions()
    {
        var policy = UpdatePolicy ?? StreamingUpdatePolicy.IncludeUpdates;
        return new StreamingIndicatorEngineOptions
        {
            EmitUpdates = policy == StreamingUpdatePolicy.IncludeUpdates,
            QuotePriceMode = QuotePriceMode ?? global::OoplesFinance.StockIndicators.Streaming.QuotePriceMode.Mid,
            OutOfOrderPolicy = OutOfOrderPolicy ?? global::OoplesFinance.StockIndicators.Streaming.OutOfOrderPolicy.Drop,
            ReorderWindow = ReorderWindow ?? TimeSpan.Zero,
            MaxBufferSize = MaxBufferSize ?? 1024
        };
    }

    public IndicatorSubscriptionOptions CreateSubscriptionOptions()
    {
        var policy = UpdatePolicy ?? StreamingUpdatePolicy.IncludeUpdates;
        return new IndicatorSubscriptionOptions
        {
            IncludeUpdates = policy == StreamingUpdatePolicy.IncludeUpdates,
            IncludeOutputValues = IncludeOutputValues ?? true,
            InputName = InputName ?? global::OoplesFinance.StockIndicators.Enums.InputName.Close,
            Options = IndicatorOptions
        };
    }

    public StreamSubscriptionRequest CreateSubscriptionRequest()
    {
        if (Symbols == null || Symbols.Count == 0)
        {
            throw new InvalidOperationException("StreamingOptions.Symbols is required to build a subscription request.");
        }

        var trades = SubscribeTrades ?? true;
        var quotes = SubscribeQuotes ?? true;
        var bars = SubscribeBars ?? false;
        var timeframes = bars ? GetTimeframes() : Array.Empty<BarTimeframe>();
        return new StreamSubscriptionRequest(Symbols, trades, quotes, bars, timeframes);
    }

    public StreamingProcessingMode GetProcessingMode()
    {
        return ProcessingMode ?? StreamingProcessingMode.Inline;
    }

    public StreamingBackpressurePolicy GetBackpressurePolicy()
    {
        return BackpressurePolicy ?? StreamingBackpressurePolicy.DropOldest;
    }

    public int GetMaxPendingMessages()
    {
        return MaxPendingMessages ?? StreamingDefaults.DefaultMaxPendingMessages;
    }
}

public sealed class StreamingIndicatorRegistration
{
    public StreamingIndicatorRegistration(string symbol,
        Func<StockData, StockData> calculator,
        Action<IndicatorUpdate> onUpdate,
        IReadOnlyList<BarTimeframe>? timeframes = null,
        IndicatorSubscriptionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        Symbol = symbol;
        Calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        OnUpdate = onUpdate ?? throw new ArgumentNullException(nameof(onUpdate));
        Timeframes = timeframes;
        Options = options;
    }

    public string Symbol { get; }
    public Func<StockData, StockData> Calculator { get; }
    public Action<IndicatorUpdate> OnUpdate { get; }
    public IReadOnlyList<BarTimeframe>? Timeframes { get; }
    public IndicatorSubscriptionOptions? Options { get; }
}
