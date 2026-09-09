namespace OoplesFinance.StockIndicators.Streaming.Providers.Alpaca;

public interface IAlpacaStreamClient
{
    void SubscribeTrades(IReadOnlyList<string> symbols, Action<StreamTrade> onTrade);
    void SubscribeQuotes(IReadOnlyList<string> symbols, Action<StreamQuote> onQuote);
    void SubscribeBars(IReadOnlyList<string> symbols, IReadOnlyList<BarTimeframe> timeframes, Action<OhlcvBar> onBar);
    void Start();
    void Stop();
}
