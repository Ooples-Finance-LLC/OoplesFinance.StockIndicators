namespace OoplesFinance.StockIndicators.Streaming;

public enum QuotePriceMode
{
    Mid,
    Bid,
    Ask
}

public sealed class StreamTrade
{
    public StreamTrade(string symbol, DateTime timestamp, double price, double size)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Timestamp = timestamp;
        Price = price;
        Size = size;
    }

    public string Symbol { get; }
    public DateTime Timestamp { get; }
    public double Price { get; }
    public double Size { get; }
}

public sealed class StreamQuote
{
    public StreamQuote(string symbol, DateTime timestamp, double bidPrice, double askPrice, double bidSize, double askSize)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Timestamp = timestamp;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        BidSize = bidSize;
        AskSize = askSize;
    }

    public string Symbol { get; }
    public DateTime Timestamp { get; }
    public double BidPrice { get; }
    public double AskPrice { get; }
    public double BidSize { get; }
    public double AskSize { get; }
}

public sealed class OhlcvBar
{
    public OhlcvBar(string symbol, BarTimeframe timeframe, DateTime startTime, DateTime endTime,
        double open, double high, double low, double close, double volume, bool isFinal)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
        StartTime = startTime;
        EndTime = endTime;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        IsFinal = isFinal;
    }

    public string Symbol { get; }
    public BarTimeframe Timeframe { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public double Open { get; }
    public double High { get; }
    public double Low { get; }
    public double Close { get; }
    public double Volume { get; }
    public bool IsFinal { get; }
}
