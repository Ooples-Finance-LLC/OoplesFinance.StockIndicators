namespace OoplesFinance.StockIndicators.Streaming.Providers.Alpaca;

public static class AlpacaStreamingSession
{
    public static StreamingSession Create(IAlpacaStreamClient client, IReadOnlyList<string> symbols,
        IReadOnlyList<BarTimeframe>? timeframes = null, StreamingOptions? options = null)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        var source = new AlpacaStreamSource(client);
        return StreamingSession.Create(source, symbols, timeframes, options);
    }
}
