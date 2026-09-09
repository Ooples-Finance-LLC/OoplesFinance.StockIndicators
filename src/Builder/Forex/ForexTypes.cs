namespace OoplesFinance.StockIndicators.Builder.Forex;

/// <summary>
/// Represents a currency pair for forex trading.
/// </summary>
public sealed class CurrencyPair
{
    /// <summary>Gets the base currency (first currency in pair).</summary>
    public string BaseCurrency { get; }

    /// <summary>Gets the quote currency (second currency in pair).</summary>
    public string QuoteCurrency { get; }

    /// <summary>Gets the standard symbol (e.g., "EUR/USD").</summary>
    public string Symbol { get; }

    /// <summary>Gets the pip decimal places (usually 4 for most pairs, 2 for JPY pairs).</summary>
    public int PipDecimalPlaces { get; }

    /// <summary>Gets the pip value (0.0001 for most pairs, 0.01 for JPY pairs).</summary>
    public decimal PipValue { get; }

    /// <summary>Gets whether this is a major pair.</summary>
    public bool IsMajorPair { get; }

    /// <summary>Gets whether this involves JPY.</summary>
    public bool IsJpyPair { get; }

    /// <summary>
    /// Creates a new currency pair.
    /// </summary>
    public CurrencyPair(string baseCurrency, string quoteCurrency)
    {
        BaseCurrency = baseCurrency.ToUpperInvariant();
        QuoteCurrency = quoteCurrency.ToUpperInvariant();
        Symbol = $"{BaseCurrency}/{QuoteCurrency}";
        IsJpyPair = BaseCurrency == "JPY" || QuoteCurrency == "JPY";
        PipDecimalPlaces = IsJpyPair ? 2 : 4;
        PipValue = IsJpyPair ? 0.01m : 0.0001m;
        IsMajorPair = IsMajor(BaseCurrency, QuoteCurrency);
    }

    /// <summary>
    /// Parses a currency pair from a symbol string.
    /// </summary>
    public static CurrencyPair Parse(string symbol)
    {
        // Handle formats: "EUR/USD", "EURUSD", "EUR-USD", "EUR_USD"
        var normalized = symbol.ToUpperInvariant()
            .Replace("/", "")
            .Replace("-", "")
            .Replace("_", "");

        if (normalized.Length != 6)
        {
            throw new ArgumentException($"Invalid currency pair format: {symbol}", nameof(symbol));
        }

        return new CurrencyPair(normalized.Substring(0, 3), normalized.Substring(3));
    }

    /// <summary>
    /// Gets the inverse pair (e.g., EUR/USD -> USD/EUR).
    /// </summary>
    public CurrencyPair GetInverse() => new(QuoteCurrency, BaseCurrency);

    private static bool IsMajor(string baseCcy, string quoteCcy)
    {
        var majors = new HashSet<string> { "USD", "EUR", "GBP", "JPY", "CHF", "AUD", "CAD", "NZD" };
        return majors.Contains(baseCcy) && majors.Contains(quoteCcy) &&
               (baseCcy == "USD" || quoteCcy == "USD");
    }

    public override string ToString() => Symbol;
    public override int GetHashCode() => Symbol.GetHashCode();
    public override bool Equals(object? obj) => obj is CurrencyPair other && Symbol == other.Symbol;
}

/// <summary>
/// Forex lot size specifications.
/// </summary>
public enum LotSize
{
    /// <summary>Standard lot = 100,000 units of base currency.</summary>
    Standard = 100000,

    /// <summary>Mini lot = 10,000 units of base currency.</summary>
    Mini = 10000,

    /// <summary>Micro lot = 1,000 units of base currency.</summary>
    Micro = 1000,

    /// <summary>Nano lot = 100 units of base currency.</summary>
    Nano = 100
}

/// <summary>
/// Forex position with pip-based calculations.
/// </summary>
public sealed class ForexPosition
{
    /// <summary>Gets the currency pair.</summary>
    public CurrencyPair Pair { get; }

    /// <summary>Gets the position size in units of base currency.</summary>
    public decimal Units { get; }

    /// <summary>Gets the position size in lots.</summary>
    public decimal Lots => Units / (int)LotSize.Standard;

    /// <summary>Gets the entry price.</summary>
    public decimal EntryPrice { get; }

    /// <summary>Gets or sets the current price.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>Gets whether this is a long position.</summary>
    public bool IsLong { get; }

    /// <summary>Gets the entry time.</summary>
    public DateTime EntryTime { get; }

    /// <summary>Gets or sets the stop loss price.</summary>
    public decimal? StopLoss { get; set; }

    /// <summary>Gets or sets the take profit price.</summary>
    public decimal? TakeProfit { get; set; }

    /// <summary>Gets or sets the accumulated swap/rollover.</summary>
    public decimal AccumulatedSwap { get; set; }

    /// <summary>
    /// Creates a new forex position.
    /// </summary>
    public ForexPosition(CurrencyPair pair, decimal units, decimal entryPrice, bool isLong, DateTime? entryTime = null)
    {
        Pair = pair;
        Units = units;
        EntryPrice = entryPrice;
        CurrentPrice = entryPrice;
        IsLong = isLong;
        EntryTime = entryTime ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates pip difference between two prices.
    /// </summary>
    public decimal GetPipDifference(decimal fromPrice, decimal toPrice)
    {
        return (toPrice - fromPrice) / Pair.PipValue;
    }

    /// <summary>
    /// Gets the current P&L in pips.
    /// </summary>
    public decimal GetPnLInPips()
    {
        var pips = GetPipDifference(EntryPrice, CurrentPrice);
        return IsLong ? pips : -pips;
    }

    /// <summary>
    /// Gets the current P&L in quote currency.
    /// </summary>
    public decimal GetPnLInQuoteCurrency()
    {
        var priceDiff = CurrentPrice - EntryPrice;
        var pnl = IsLong ? priceDiff * Units : -priceDiff * Units;
        return pnl + AccumulatedSwap;
    }

    /// <summary>
    /// Gets the pip value in quote currency for the position size.
    /// </summary>
    public decimal GetPipValueInQuoteCurrency()
    {
        return Units * Pair.PipValue;
    }

    /// <summary>
    /// Calculates margin required for this position.
    /// </summary>
    /// <param name="leverage">Account leverage (e.g., 50 for 50:1).</param>
    public decimal CalculateMarginRequired(decimal leverage)
    {
        if (leverage <= 0) return Units * CurrentPrice;
        return (Units * CurrentPrice) / leverage;
    }
}

/// <summary>
/// Swap/rollover rate information for a currency pair.
/// </summary>
public sealed class SwapRate
{
    /// <summary>Gets the currency pair.</summary>
    public CurrencyPair Pair { get; }

    /// <summary>Gets the swap rate for long positions (in pips per lot per day).</summary>
    public decimal LongSwapPips { get; }

    /// <summary>Gets the swap rate for short positions (in pips per lot per day).</summary>
    public decimal ShortSwapPips { get; }

    /// <summary>Gets the day of week when triple swap is applied (usually Wednesday = 3).</summary>
    public DayOfWeek TripleSwapDay { get; }

    /// <summary>Gets the rollover time in UTC.</summary>
    public TimeSpan RolloverTimeUtc { get; }

    /// <summary>Gets when this rate was last updated.</summary>
    public DateTime LastUpdated { get; }

    /// <summary>
    /// Creates a new swap rate.
    /// </summary>
    public SwapRate(
        CurrencyPair pair,
        decimal longSwapPips,
        decimal shortSwapPips,
        DayOfWeek tripleSwapDay = DayOfWeek.Wednesday,
        TimeSpan? rolloverTimeUtc = null)
    {
        Pair = pair;
        LongSwapPips = longSwapPips;
        ShortSwapPips = shortSwapPips;
        TripleSwapDay = tripleSwapDay;
        RolloverTimeUtc = rolloverTimeUtc ?? new TimeSpan(21, 0, 0); // 9 PM UTC default
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the swap cost/credit for a position.
    /// </summary>
    /// <param name="position">The forex position.</param>
    /// <param name="days">Number of days held over rollover.</param>
    /// <returns>Swap amount in quote currency.</returns>
    public decimal CalculateSwap(ForexPosition position, int days = 1)
    {
        var swapPips = position.IsLong ? LongSwapPips : ShortSwapPips;
        var pipValue = position.GetPipValueInQuoteCurrency();
        return swapPips * pipValue * days;
    }

    /// <summary>
    /// Gets the swap multiplier for a given day (3x on triple swap day).
    /// </summary>
    public int GetSwapMultiplier(DayOfWeek day)
    {
        return day == TripleSwapDay ? 3 : 1;
    }
}

/// <summary>
/// Forex pip calculator utility.
/// </summary>
public static class PipCalculator
{
    /// <summary>
    /// Calculates pip value in account currency.
    /// </summary>
    /// <param name="pair">The currency pair.</param>
    /// <param name="lotSize">Position size in lots.</param>
    /// <param name="accountCurrency">Account base currency.</param>
    /// <param name="conversionRate">Rate to convert quote currency to account currency (1.0 if same).</param>
    public static decimal CalculatePipValue(
        CurrencyPair pair,
        decimal lotSize,
        string accountCurrency,
        decimal conversionRate = 1.0m)
    {
        var units = lotSize * (int)LotSize.Standard;
        var pipValueInQuote = units * pair.PipValue;

        if (pair.QuoteCurrency == accountCurrency.ToUpperInvariant())
        {
            return pipValueInQuote;
        }

        return pipValueInQuote * conversionRate;
    }

    /// <summary>
    /// Calculates the number of pips between two prices.
    /// </summary>
    public static decimal GetPips(CurrencyPair pair, decimal fromPrice, decimal toPrice)
    {
        return (toPrice - fromPrice) / pair.PipValue;
    }

    /// <summary>
    /// Adds pips to a price.
    /// </summary>
    public static decimal AddPips(CurrencyPair pair, decimal price, decimal pips)
    {
        return price + (pips * pair.PipValue);
    }

    /// <summary>
    /// Calculates position size based on risk amount and stop loss.
    /// </summary>
    /// <param name="pair">The currency pair.</param>
    /// <param name="riskAmount">Amount willing to risk in account currency.</param>
    /// <param name="stopLossPips">Stop loss distance in pips.</param>
    /// <param name="accountCurrency">Account currency.</param>
    /// <param name="conversionRate">Rate to convert quote currency to account currency.</param>
    /// <returns>Position size in lots.</returns>
    public static decimal CalculatePositionSize(
        CurrencyPair pair,
        decimal riskAmount,
        decimal stopLossPips,
        string accountCurrency,
        decimal conversionRate = 1.0m)
    {
        if (stopLossPips <= 0) return 0;

        var pipValue = CalculatePipValue(pair, 1.0m, accountCurrency, conversionRate);
        var positionSizeLots = riskAmount / (stopLossPips * pipValue);

        return Math.Round(positionSizeLots, 2);
    }

    /// <summary>
    /// Calculates required margin for a position.
    /// </summary>
    /// <param name="pair">The currency pair.</param>
    /// <param name="lotSize">Position size in lots.</param>
    /// <param name="currentPrice">Current market price.</param>
    /// <param name="leverage">Account leverage ratio.</param>
    /// <returns>Required margin in quote currency.</returns>
    public static decimal CalculateMargin(
        CurrencyPair pair,
        decimal lotSize,
        decimal currentPrice,
        decimal leverage)
    {
        var units = lotSize * (int)LotSize.Standard;
        var notionalValue = units * currentPrice;
        return notionalValue / leverage;
    }
}

/// <summary>
/// Common currency pairs as static instances.
/// </summary>
public static class CommonPairs
{
    // Major Pairs
    public static CurrencyPair EURUSD { get; } = new("EUR", "USD");
    public static CurrencyPair GBPUSD { get; } = new("GBP", "USD");
    public static CurrencyPair USDJPY { get; } = new("USD", "JPY");
    public static CurrencyPair USDCHF { get; } = new("USD", "CHF");
    public static CurrencyPair AUDUSD { get; } = new("AUD", "USD");
    public static CurrencyPair USDCAD { get; } = new("USD", "CAD");
    public static CurrencyPair NZDUSD { get; } = new("NZD", "USD");

    // Cross Pairs
    public static CurrencyPair EURGBP { get; } = new("EUR", "GBP");
    public static CurrencyPair EURJPY { get; } = new("EUR", "JPY");
    public static CurrencyPair GBPJPY { get; } = new("GBP", "JPY");
    public static CurrencyPair EURCHF { get; } = new("EUR", "CHF");
    public static CurrencyPair AUDJPY { get; } = new("AUD", "JPY");
    public static CurrencyPair CADJPY { get; } = new("CAD", "JPY");
    public static CurrencyPair EURAUD { get; } = new("EUR", "AUD");
    public static CurrencyPair GBPAUD { get; } = new("GBP", "AUD");

    // Exotic Pairs
    public static CurrencyPair USDMXN { get; } = new("USD", "MXN");
    public static CurrencyPair USDZAR { get; } = new("USD", "ZAR");
    public static CurrencyPair USDTRY { get; } = new("USD", "TRY");
    public static CurrencyPair USDSEK { get; } = new("USD", "SEK");
    public static CurrencyPair USDNOK { get; } = new("USD", "NOK");
    public static CurrencyPair USDDKK { get; } = new("USD", "DKK");
    public static CurrencyPair USDSGD { get; } = new("USD", "SGD");
    public static CurrencyPair USDHKD { get; } = new("USD", "HKD");

    /// <summary>
    /// Gets all major pairs.
    /// </summary>
    public static IReadOnlyList<CurrencyPair> GetMajorPairs() => new[]
    {
        EURUSD, GBPUSD, USDJPY, USDCHF, AUDUSD, USDCAD, NZDUSD
    };

    /// <summary>
    /// Gets all cross pairs.
    /// </summary>
    public static IReadOnlyList<CurrencyPair> GetCrossPairs() => new[]
    {
        EURGBP, EURJPY, GBPJPY, EURCHF, AUDJPY, CADJPY, EURAUD, GBPAUD
    };
}

/// <summary>
/// Forex trading session information.
/// </summary>
public sealed class ForexSession
{
    /// <summary>Gets the session name.</summary>
    public string Name { get; }

    /// <summary>Gets the session open time in UTC.</summary>
    public TimeSpan OpenTimeUtc { get; }

    /// <summary>Gets the session close time in UTC.</summary>
    public TimeSpan CloseTimeUtc { get; }

    /// <summary>Gets the major financial center for this session.</summary>
    public string FinancialCenter { get; }

    private ForexSession(string name, TimeSpan openUtc, TimeSpan closeUtc, string center)
    {
        Name = name;
        OpenTimeUtc = openUtc;
        CloseTimeUtc = closeUtc;
        FinancialCenter = center;
    }

    /// <summary>Sydney session (10 PM - 7 AM UTC).</summary>
    public static ForexSession Sydney { get; } = new("Sydney", new TimeSpan(22, 0, 0), new TimeSpan(7, 0, 0), "Sydney");

    /// <summary>Tokyo session (12 AM - 9 AM UTC).</summary>
    public static ForexSession Tokyo { get; } = new("Tokyo", new TimeSpan(0, 0, 0), new TimeSpan(9, 0, 0), "Tokyo");

    /// <summary>London session (8 AM - 5 PM UTC).</summary>
    public static ForexSession London { get; } = new("London", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), "London");

    /// <summary>New York session (1 PM - 10 PM UTC).</summary>
    public static ForexSession NewYork { get; } = new("NewYork", new TimeSpan(13, 0, 0), new TimeSpan(22, 0, 0), "New York");

    /// <summary>
    /// Checks if the session is currently active.
    /// </summary>
    public bool IsActive(DateTime utcTime)
    {
        var timeOfDay = utcTime.TimeOfDay;

        if (CloseTimeUtc < OpenTimeUtc)
        {
            // Session spans midnight
            return timeOfDay >= OpenTimeUtc || timeOfDay < CloseTimeUtc;
        }

        return timeOfDay >= OpenTimeUtc && timeOfDay < CloseTimeUtc;
    }

    /// <summary>
    /// Gets all sessions active at a given UTC time.
    /// </summary>
    public static IReadOnlyList<ForexSession> GetActiveSessions(DateTime utcTime)
    {
        var sessions = new[] { Sydney, Tokyo, London, NewYork };
        return sessions.Where(s => s.IsActive(utcTime)).ToList();
    }

    /// <summary>
    /// Checks if sessions overlap (high liquidity periods).
    /// </summary>
    public static bool IsSessionOverlap(DateTime utcTime)
    {
        return GetActiveSessions(utcTime).Count > 1;
    }
}
