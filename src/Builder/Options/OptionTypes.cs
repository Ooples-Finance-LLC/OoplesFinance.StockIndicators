namespace OoplesFinance.StockIndicators.Builder.Options;

/// <summary>
/// Option type (call or put).
/// </summary>
public enum OptionType
{
    /// <summary>Call option - right to buy.</summary>
    Call,

    /// <summary>Put option - right to sell.</summary>
    Put
}

/// <summary>
/// Option exercise style.
/// </summary>
public enum ExerciseStyle
{
    /// <summary>American - can be exercised any time before expiration.</summary>
    American,

    /// <summary>European - can only be exercised at expiration.</summary>
    European
}

/// <summary>
/// The Greeks for an option.
/// </summary>
public sealed class OptionGreeks
{
    /// <summary>Gets or sets Delta: price sensitivity to underlying price changes.</summary>
    public decimal Delta { get; set; }

    /// <summary>Gets or sets Gamma: rate of change of Delta.</summary>
    public decimal Gamma { get; set; }

    /// <summary>Gets or sets Theta: time decay per day.</summary>
    public decimal Theta { get; set; }

    /// <summary>Gets or sets Vega: sensitivity to implied volatility.</summary>
    public decimal Vega { get; set; }

    /// <summary>Gets or sets Rho: sensitivity to interest rates.</summary>
    public decimal Rho { get; set; }

    /// <summary>Gets or sets Vanna: sensitivity of Delta to IV (dDelta/dIV).</summary>
    public decimal Vanna { get; set; }

    /// <summary>Gets or sets Charm: rate of change of Delta over time.</summary>
    public decimal Charm { get; set; }

    /// <summary>Gets or sets Volga (Vomma): sensitivity of Vega to IV.</summary>
    public decimal Volga { get; set; }

    /// <summary>Creates an empty Greeks instance.</summary>
    public static OptionGreeks Empty => new();

    /// <summary>
    /// Adds two Greeks instances together.
    /// </summary>
    public static OptionGreeks operator +(OptionGreeks a, OptionGreeks b)
    {
        return new OptionGreeks
        {
            Delta = a.Delta + b.Delta,
            Gamma = a.Gamma + b.Gamma,
            Theta = a.Theta + b.Theta,
            Vega = a.Vega + b.Vega,
            Rho = a.Rho + b.Rho,
            Vanna = a.Vanna + b.Vanna,
            Charm = a.Charm + b.Charm,
            Volga = a.Volga + b.Volga
        };
    }

    /// <summary>
    /// Multiplies Greeks by a scalar (for position sizing).
    /// </summary>
    public static OptionGreeks operator *(OptionGreeks g, decimal scalar)
    {
        return new OptionGreeks
        {
            Delta = g.Delta * scalar,
            Gamma = g.Gamma * scalar,
            Theta = g.Theta * scalar,
            Vega = g.Vega * scalar,
            Rho = g.Rho * scalar,
            Vanna = g.Vanna * scalar,
            Charm = g.Charm * scalar,
            Volga = g.Volga * scalar
        };
    }

    /// <summary>
    /// Scales Greeks by a quantity.
    /// </summary>
    public OptionGreeks Scale(decimal quantity) => this * quantity;

    /// <summary>
    /// Negates Greeks (for short positions).
    /// </summary>
    public OptionGreeks Negate() => this * -1m;
}

/// <summary>
/// A single option contract.
/// </summary>
public sealed class OptionContract
{
    /// <summary>Gets or sets the option symbol (e.g., AAPL230120C00150000).</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the underlying symbol (e.g., AAPL).</summary>
    public string UnderlyingSymbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the option type (call/put).</summary>
    public OptionType OptionType { get; set; }

    /// <summary>Gets or sets the strike price.</summary>
    public decimal StrikePrice { get; set; }

    /// <summary>Gets or sets the expiration date.</summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>Gets or sets the exercise style.</summary>
    public ExerciseStyle ExerciseStyle { get; set; } = ExerciseStyle.American;

    /// <summary>Gets or sets the contract multiplier (typically 100).</summary>
    public int Multiplier { get; set; } = 100;

    /// <summary>Gets or sets the current bid price.</summary>
    public decimal Bid { get; set; }

    /// <summary>Gets or sets the current ask price.</summary>
    public decimal Ask { get; set; }

    /// <summary>Gets or sets the last traded price.</summary>
    public decimal Last { get; set; }

    /// <summary>Gets or sets the bid size.</summary>
    public int BidSize { get; set; }

    /// <summary>Gets or sets the ask size.</summary>
    public int AskSize { get; set; }

    /// <summary>Gets or sets the open interest.</summary>
    public long OpenInterest { get; set; }

    /// <summary>Gets or sets the volume.</summary>
    public long Volume { get; set; }

    /// <summary>Gets or sets the implied volatility.</summary>
    public decimal ImpliedVolatility { get; set; }

    /// <summary>Gets or sets the calculated Greeks.</summary>
    public OptionGreeks Greeks { get; set; } = new();

    /// <summary>Gets or sets the theoretical value.</summary>
    public decimal TheoreticalValue { get; set; }

    /// <summary>Gets or sets the underlying price when Greeks were calculated.</summary>
    public decimal UnderlyingPrice { get; set; }

    /// <summary>Gets or sets the timestamp of the data.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets the mid price.</summary>
    public decimal Mid => (Bid + Ask) / 2m;

    /// <summary>Gets the spread.</summary>
    public decimal Spread => Ask - Bid;

    /// <summary>Gets the spread as percent of mid.</summary>
    public decimal SpreadPercent => Mid > 0 ? Spread / Mid * 100m : 0m;

    /// <summary>Gets days to expiration.</summary>
    public int DaysToExpiration => Math.Max(0, (ExpirationDate.Date - DateTime.UtcNow.Date).Days);

    /// <summary>Gets whether the option is expired.</summary>
    public bool IsExpired => DateTime.UtcNow.Date > ExpirationDate.Date;

    /// <summary>Gets whether this is a call option.</summary>
    public bool IsCall => OptionType == OptionType.Call;

    /// <summary>Gets whether this is a put option.</summary>
    public bool IsPut => OptionType == OptionType.Put;

    /// <summary>Gets intrinsic value.</summary>
    public decimal IntrinsicValue
    {
        get
        {
            if (UnderlyingPrice <= 0) return 0m;
            return IsCall
                ? Math.Max(0, UnderlyingPrice - StrikePrice)
                : Math.Max(0, StrikePrice - UnderlyingPrice);
        }
    }

    /// <summary>Gets extrinsic (time) value.</summary>
    public decimal ExtrinsicValue => Math.Max(0, Mid - IntrinsicValue);

    /// <summary>Gets whether the option is in the money.</summary>
    public bool IsInTheMoney => IntrinsicValue > 0;

    /// <summary>Gets whether the option is at the money.</summary>
    public bool IsAtTheMoney => Math.Abs(UnderlyingPrice - StrikePrice) / UnderlyingPrice < 0.01m;

    /// <summary>Gets whether the option is out of the money.</summary>
    public bool IsOutOfTheMoney => !IsInTheMoney && !IsAtTheMoney;

    /// <summary>Gets the moneyness (S/K for calls, K/S for puts).</summary>
    public decimal Moneyness
    {
        get
        {
            if (StrikePrice <= 0 || UnderlyingPrice <= 0) return 1m;
            return IsCall
                ? UnderlyingPrice / StrikePrice
                : StrikePrice / UnderlyingPrice;
        }
    }

    /// <summary>Gets the contract notional value.</summary>
    public decimal NotionalValue => UnderlyingPrice * Multiplier;

    /// <summary>
    /// Calculates the break-even price for a long position.
    /// </summary>
    public decimal BreakEvenPrice
    {
        get
        {
            var premium = Ask; // Cost to buy
            return IsCall
                ? StrikePrice + premium
                : StrikePrice - premium;
        }
    }
}

/// <summary>
/// Collection of options for a single expiration date.
/// </summary>
public sealed class OptionExpiration
{
    /// <summary>Gets or sets the expiration date.</summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>Gets or sets the days to expiration.</summary>
    public int DaysToExpiration => Math.Max(0, (ExpirationDate.Date - DateTime.UtcNow.Date).Days);

    /// <summary>Gets or sets the call contracts.</summary>
    public IReadOnlyList<OptionContract> Calls { get; set; } = Array.Empty<OptionContract>();

    /// <summary>Gets or sets the put contracts.</summary>
    public IReadOnlyList<OptionContract> Puts { get; set; } = Array.Empty<OptionContract>();

    /// <summary>Gets all contracts (calls and puts).</summary>
    public IEnumerable<OptionContract> AllContracts => Calls.Concat(Puts);

    /// <summary>Gets the distinct strikes.</summary>
    public IReadOnlyList<decimal> Strikes =>
        Calls.Select(c => c.StrikePrice)
            .Union(Puts.Select(p => p.StrikePrice))
            .OrderBy(s => s)
            .ToList();

    /// <summary>
    /// Gets the at-the-money strike.
    /// </summary>
    public decimal AtTheMoneyStrike(decimal underlyingPrice)
    {
        return Strikes
            .OrderBy(s => Math.Abs(s - underlyingPrice))
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets a call contract by strike.
    /// </summary>
    public OptionContract? GetCall(decimal strike) =>
        Calls.FirstOrDefault(c => c.StrikePrice == strike);

    /// <summary>
    /// Gets a put contract by strike.
    /// </summary>
    public OptionContract? GetPut(decimal strike) =>
        Puts.FirstOrDefault(p => p.StrikePrice == strike);
}

/// <summary>
/// Full options chain for an underlying.
/// </summary>
public sealed class FullOptionsChain
{
    /// <summary>Gets or sets the underlying symbol.</summary>
    public string UnderlyingSymbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the underlying price.</summary>
    public decimal UnderlyingPrice { get; set; }

    /// <summary>Gets or sets the expirations.</summary>
    public IReadOnlyList<OptionExpiration> Expirations { get; set; } = Array.Empty<OptionExpiration>();

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets all available expiration dates.</summary>
    public IReadOnlyList<DateTime> ExpirationDates =>
        Expirations.Select(e => e.ExpirationDate).OrderBy(d => d).ToList();

    /// <summary>
    /// Gets options for a specific expiration date.
    /// </summary>
    public OptionExpiration? GetExpiration(DateTime date) =>
        Expirations.FirstOrDefault(e => e.ExpirationDate.Date == date.Date);

    /// <summary>
    /// Gets the nearest expiration to a target number of days.
    /// </summary>
    public OptionExpiration? GetNearestExpiration(int targetDays)
    {
        return Expirations
            .OrderBy(e => Math.Abs(e.DaysToExpiration - targetDays))
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets all calls across all expirations.
    /// </summary>
    public IEnumerable<OptionContract> AllCalls => Expirations.SelectMany(e => e.Calls);

    /// <summary>
    /// Gets all puts across all expirations.
    /// </summary>
    public IEnumerable<OptionContract> AllPuts => Expirations.SelectMany(e => e.Puts);

    /// <summary>
    /// Gets all contracts across all expirations.
    /// </summary>
    public IEnumerable<OptionContract> AllContracts => Expirations.SelectMany(e => e.AllContracts);

    /// <summary>
    /// Finds options by criteria.
    /// </summary>
    public IEnumerable<OptionContract> FindOptions(
        OptionType? type = null,
        decimal? minStrike = null,
        decimal? maxStrike = null,
        int? minDte = null,
        int? maxDte = null,
        decimal? minDelta = null,
        decimal? maxDelta = null)
    {
        var query = AllContracts.AsEnumerable();

        if (type.HasValue)
            query = query.Where(c => c.OptionType == type.Value);

        if (minStrike.HasValue)
            query = query.Where(c => c.StrikePrice >= minStrike.Value);

        if (maxStrike.HasValue)
            query = query.Where(c => c.StrikePrice <= maxStrike.Value);

        if (minDte.HasValue)
            query = query.Where(c => c.DaysToExpiration >= minDte.Value);

        if (maxDte.HasValue)
            query = query.Where(c => c.DaysToExpiration <= maxDte.Value);

        if (minDelta.HasValue)
            query = query.Where(c => Math.Abs(c.Greeks.Delta) >= minDelta.Value);

        if (maxDelta.HasValue)
            query = query.Where(c => Math.Abs(c.Greeks.Delta) <= maxDelta.Value);

        return query;
    }
}

/// <summary>
/// Position in an option contract.
/// </summary>
public sealed class OptionPosition
{
    /// <summary>Gets or sets the option contract.</summary>
    public OptionContract Contract { get; set; } = new();

    /// <summary>Gets or sets the quantity (positive = long, negative = short).</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets the average entry price.</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Gets or sets when the position was opened.</summary>
    public DateTime OpenedAt { get; set; }

    /// <summary>Gets whether this is a long position.</summary>
    public bool IsLong => Quantity > 0;

    /// <summary>Gets whether this is a short position.</summary>
    public bool IsShort => Quantity < 0;

    /// <summary>Gets the position value.</summary>
    public decimal MarketValue => Contract.Mid * Math.Abs(Quantity) * Contract.Multiplier;

    /// <summary>Gets the cost basis.</summary>
    public decimal CostBasis => AveragePrice * Math.Abs(Quantity) * Contract.Multiplier;

    /// <summary>Gets the unrealized P&L.</summary>
    public decimal UnrealizedPnL
    {
        get
        {
            var currentValue = Contract.Mid * Math.Abs(Quantity) * Contract.Multiplier;
            return IsLong
                ? currentValue - CostBasis
                : CostBasis - currentValue;
        }
    }

    /// <summary>Gets the position Greeks (scaled by quantity).</summary>
    public OptionGreeks PositionGreeks
    {
        get
        {
            var greeks = Contract.Greeks.Scale(Math.Abs(Quantity) * Contract.Multiplier);
            return IsShort ? greeks.Negate() : greeks;
        }
    }
}
