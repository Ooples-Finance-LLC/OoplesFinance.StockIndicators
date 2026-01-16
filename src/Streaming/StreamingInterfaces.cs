namespace OoplesFinance.StockIndicators.Streaming;

public interface IStreamObserver
{
    void OnTrade(StreamTrade trade);
    void OnQuote(StreamQuote quote);
    void OnBar(OhlcvBar bar);
}

public interface IStreamSource
{
    IStreamSubscription Subscribe(StreamSubscriptionRequest request, IStreamObserver observer);
}

public interface IStreamSubscription : IDisposable
{
    void Start();
    void Stop();
}

public sealed class StreamSubscriptionRequest
{
    public StreamSubscriptionRequest(IReadOnlyList<string> symbols, bool trades = true, bool quotes = true,
        bool bars = false, IReadOnlyList<BarTimeframe>? barTimeframes = null)
    {
        Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        Trades = trades;
        Quotes = quotes;
        Bars = bars;
        BarTimeframes = barTimeframes ?? Array.Empty<BarTimeframe>();
    }

    public IReadOnlyList<string> Symbols { get; }
    public bool Trades { get; }
    public bool Quotes { get; }
    public bool Bars { get; }
    public IReadOnlyList<BarTimeframe> BarTimeframes { get; }
}
