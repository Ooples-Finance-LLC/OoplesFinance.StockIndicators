namespace OoplesFinance.StockIndicators.Builder.MarketData;

/// <summary>
/// Real-time quote data for a security.
/// </summary>
public sealed class Quote
{
    /// <summary>
    /// Creates a new quote.
    /// </summary>
    public Quote(
        string symbol,
        decimal bid,
        decimal ask,
        decimal last,
        long bidSize,
        long askSize,
        DateTime timestamp)
    {
        Symbol = symbol;
        Bid = bid;
        Ask = ask;
        Last = last;
        BidSize = bidSize;
        AskSize = askSize;
        Timestamp = timestamp;
    }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the best bid price.</summary>
    public decimal Bid { get; }

    /// <summary>Gets the best ask price.</summary>
    public decimal Ask { get; }

    /// <summary>Gets the last traded price.</summary>
    public decimal Last { get; }

    /// <summary>Gets the size at the best bid.</summary>
    public long BidSize { get; }

    /// <summary>Gets the size at the best ask.</summary>
    public long AskSize { get; }

    /// <summary>Gets the quote timestamp.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Gets the mid price.</summary>
    public decimal Mid => (Bid + Ask) / 2m;

    /// <summary>Gets the bid-ask spread.</summary>
    public decimal Spread => Ask - Bid;

    /// <summary>Gets the spread as a percentage of the mid price.</summary>
    public decimal SpreadPercent => Mid > 0 ? Spread / Mid * 100m : 0m;
}

/// <summary>
/// A single trade execution.
/// </summary>
public sealed class Trade
{
    /// <summary>
    /// Creates a new trade record.
    /// </summary>
    public Trade(
        string symbol,
        decimal price,
        long size,
        DateTime timestamp,
        string? exchange = null,
        string? tradeId = null)
    {
        Symbol = symbol;
        Price = price;
        Size = size;
        Timestamp = timestamp;
        Exchange = exchange;
        TradeId = tradeId;
    }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the trade price.</summary>
    public decimal Price { get; }

    /// <summary>Gets the trade size.</summary>
    public long Size { get; }

    /// <summary>Gets the trade timestamp.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Gets the exchange where the trade occurred.</summary>
    public string? Exchange { get; }

    /// <summary>Gets the unique trade identifier.</summary>
    public string? TradeId { get; }

    /// <summary>Gets the total trade value.</summary>
    public decimal Value => Price * Size;
}

/// <summary>
/// OHLCV bar data.
/// </summary>
public sealed class Bar
{
    /// <summary>
    /// Creates a new bar.
    /// </summary>
    public Bar(
        string symbol,
        DateTime timestamp,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        long volume,
        decimal? vwap = null,
        int? tradeCount = null)
    {
        Symbol = symbol;
        Timestamp = timestamp;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        Vwap = vwap;
        TradeCount = tradeCount;
    }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the bar timestamp (start of period).</summary>
    public DateTime Timestamp { get; }

    /// <summary>Gets the open price.</summary>
    public decimal Open { get; }

    /// <summary>Gets the high price.</summary>
    public decimal High { get; }

    /// <summary>Gets the low price.</summary>
    public decimal Low { get; }

    /// <summary>Gets the close price.</summary>
    public decimal Close { get; }

    /// <summary>Gets the volume.</summary>
    public long Volume { get; }

    /// <summary>Gets the volume-weighted average price.</summary>
    public decimal? Vwap { get; }

    /// <summary>Gets the number of trades in this bar.</summary>
    public int? TradeCount { get; }

    /// <summary>Gets the bar range (high - low).</summary>
    public decimal Range => High - Low;

    /// <summary>Gets the typical price ((H+L+C)/3).</summary>
    public decimal TypicalPrice => (High + Low + Close) / 3m;

    /// <summary>Gets whether this is a bullish bar (close > open).</summary>
    public bool IsBullish => Close > Open;

    /// <summary>Gets whether this is a bearish bar (close < open).</summary>
    public bool IsBearish => Close < Open;
}

/// <summary>
/// Bar timeframe for historical data requests.
/// </summary>
public enum BarTimeframe
{
    /// <summary>1 minute bars.</summary>
    Minute1,

    /// <summary>5 minute bars.</summary>
    Minute5,

    /// <summary>15 minute bars.</summary>
    Minute15,

    /// <summary>30 minute bars.</summary>
    Minute30,

    /// <summary>1 hour bars.</summary>
    Hour1,

    /// <summary>4 hour bars.</summary>
    Hour4,

    /// <summary>Daily bars.</summary>
    Day,

    /// <summary>Weekly bars.</summary>
    Week,

    /// <summary>Monthly bars.</summary>
    Month
}

/// <summary>
/// A price level in the order book.
/// </summary>
public sealed class PriceLevel
{
    /// <summary>
    /// Creates a new price level.
    /// </summary>
    public PriceLevel(decimal price, long size, int orderCount = 0)
    {
        Price = price;
        Size = size;
        OrderCount = orderCount;
    }

    /// <summary>Gets the price at this level.</summary>
    public decimal Price { get; }

    /// <summary>Gets the total size at this level.</summary>
    public long Size { get; }

    /// <summary>Gets the number of orders at this level.</summary>
    public int OrderCount { get; }

    /// <summary>Gets the total value at this level.</summary>
    public decimal Value => Price * Size;
}

/// <summary>
/// Level 2 order book with full depth of market.
/// </summary>
public sealed class Level2OrderBook
{
    /// <summary>
    /// Creates a new order book.
    /// </summary>
    public Level2OrderBook(
        string symbol,
        IReadOnlyList<PriceLevel> bids,
        IReadOnlyList<PriceLevel> asks,
        DateTime timestamp)
    {
        Symbol = symbol;
        Bids = bids;
        Asks = asks;
        Timestamp = timestamp;
    }

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the bid levels (sorted highest to lowest).</summary>
    public IReadOnlyList<PriceLevel> Bids { get; }

    /// <summary>Gets the ask levels (sorted lowest to highest).</summary>
    public IReadOnlyList<PriceLevel> Asks { get; }

    /// <summary>Gets the order book timestamp.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Gets the best bid price.</summary>
    public decimal? BestBid => Bids.Count > 0 ? Bids[0].Price : null;

    /// <summary>Gets the best ask price.</summary>
    public decimal? BestAsk => Asks.Count > 0 ? Asks[0].Price : null;

    /// <summary>Gets the mid price.</summary>
    public decimal? MidPrice => BestBid.HasValue && BestAsk.HasValue
        ? (BestBid.Value + BestAsk.Value) / 2m
        : null;

    /// <summary>Gets the spread.</summary>
    public decimal? Spread => BestBid.HasValue && BestAsk.HasValue
        ? BestAsk.Value - BestBid.Value
        : null;

    /// <summary>Gets the total bid volume.</summary>
    public long TotalBidVolume => Bids.Sum(b => b.Size);

    /// <summary>Gets the total ask volume.</summary>
    public long TotalAskVolume => Asks.Sum(a => a.Size);

    /// <summary>Gets the order book imbalance (-1 to 1, positive = more bids).</summary>
    public decimal Imbalance
    {
        get
        {
            var totalBid = TotalBidVolume;
            var totalAsk = TotalAskVolume;
            var total = totalBid + totalAsk;
            return total > 0 ? (decimal)(totalBid - totalAsk) / total : 0m;
        }
    }

    /// <summary>
    /// Calculates the VWAP if an order of given size were to be executed.
    /// </summary>
    /// <param name="size">The order size.</param>
    /// <param name="isBuy">True for buy (walks up asks), false for sell (walks down bids).</param>
    /// <returns>The expected VWAP and slippage.</returns>
    public (decimal Vwap, decimal Slippage) CalculateMarketImpact(long size, bool isBuy)
    {
        var levels = isBuy ? Asks : Bids;
        if (levels.Count == 0)
        {
            return (0m, 0m);
        }

        var remaining = size;
        var totalCost = 0m;
        var totalFilled = 0L;
        var bestPrice = levels[0].Price;

        foreach (var level in levels)
        {
            var fillAtLevel = Math.Min(remaining, level.Size);
            totalCost += level.Price * fillAtLevel;
            totalFilled += fillAtLevel;
            remaining -= fillAtLevel;

            if (remaining <= 0)
            {
                break;
            }
        }

        if (totalFilled == 0)
        {
            return (0m, 0m);
        }

        var vwap = totalCost / totalFilled;
        var slippage = Math.Abs(vwap - bestPrice);
        return (vwap, slippage);
    }
}

/// <summary>
/// Snapshot of current market state for a symbol.
/// </summary>
public sealed class MarketSnapshot
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the latest quote.</summary>
    public Quote? LatestQuote { get; set; }

    /// <summary>Gets or sets the latest trade.</summary>
    public Trade? LatestTrade { get; set; }

    /// <summary>Gets or sets the daily bar.</summary>
    public Bar? DailyBar { get; set; }

    /// <summary>Gets or sets the previous daily bar.</summary>
    public Bar? PreviousBar { get; set; }

    /// <summary>Gets or sets the minute bar.</summary>
    public Bar? MinuteBar { get; set; }

    /// <summary>Gets the current price (last trade or mid quote).</summary>
    public decimal CurrentPrice =>
        LatestTrade?.Price ?? LatestQuote?.Mid ?? DailyBar?.Close ?? 0m;

    /// <summary>Gets the day's change.</summary>
    public decimal DayChange =>
        PreviousBar is not null && CurrentPrice > 0
            ? CurrentPrice - PreviousBar.Close
            : 0m;

    /// <summary>Gets the day's change as a percentage.</summary>
    public decimal DayChangePercent =>
        PreviousBar is not null && PreviousBar.Close > 0
            ? DayChange / PreviousBar.Close * 100m
            : 0m;
}

/// <summary>
/// Market data subscription request.
/// </summary>
public sealed class MarketDataSubscription
{
    /// <summary>Gets or sets the symbols to subscribe to.</summary>
    public IReadOnlyList<string> Symbols { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets whether to subscribe to quotes.</summary>
    public bool SubscribeQuotes { get; set; } = true;

    /// <summary>Gets or sets whether to subscribe to trades.</summary>
    public bool SubscribeTrades { get; set; } = false;

    /// <summary>Gets or sets whether to subscribe to minute bars.</summary>
    public bool SubscribeBars { get; set; } = false;

    /// <summary>Gets or sets the bar timeframe for bar subscriptions.</summary>
    public BarTimeframe BarTimeframe { get; set; } = BarTimeframe.Minute1;
}

/// <summary>
/// Market data event raised when new data arrives.
/// </summary>
public abstract class MarketDataEvent
{
    /// <summary>Gets the symbol.</summary>
    public abstract string Symbol { get; }

    /// <summary>Gets the event timestamp.</summary>
    public abstract DateTime Timestamp { get; }
}

/// <summary>
/// Quote update event.
/// </summary>
public sealed class QuoteEvent : MarketDataEvent
{
    /// <summary>
    /// Creates a new quote event.
    /// </summary>
    public QuoteEvent(Quote quote)
    {
        Quote = quote;
    }

    /// <summary>Gets the quote.</summary>
    public Quote Quote { get; }

    /// <inheritdoc />
    public override string Symbol => Quote.Symbol;

    /// <inheritdoc />
    public override DateTime Timestamp => Quote.Timestamp;
}

/// <summary>
/// Trade update event.
/// </summary>
public sealed class TradeEvent : MarketDataEvent
{
    /// <summary>
    /// Creates a new trade event.
    /// </summary>
    public TradeEvent(Trade trade)
    {
        Trade = trade;
    }

    /// <summary>Gets the trade.</summary>
    public Trade Trade { get; }

    /// <inheritdoc />
    public override string Symbol => Trade.Symbol;

    /// <inheritdoc />
    public override DateTime Timestamp => Trade.Timestamp;
}

/// <summary>
/// Bar update event.
/// </summary>
public sealed class BarEvent : MarketDataEvent
{
    /// <summary>
    /// Creates a new bar event.
    /// </summary>
    public BarEvent(Bar bar)
    {
        Bar = bar;
    }

    /// <summary>Gets the bar.</summary>
    public Bar Bar { get; }

    /// <inheritdoc />
    public override string Symbol => Bar.Symbol;

    /// <inheritdoc />
    public override DateTime Timestamp => Bar.Timestamp;
}
