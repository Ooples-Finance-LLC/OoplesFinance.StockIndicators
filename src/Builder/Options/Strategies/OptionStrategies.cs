namespace OoplesFinance.StockIndicators.Builder.Options.Strategies;

/// <summary>
/// Base class for option strategies.
/// </summary>
public abstract class OptionStrategy
{
    /// <summary>Gets the strategy name.</summary>
    public abstract string Name { get; }

    /// <summary>Gets a description of the strategy.</summary>
    public abstract string Description { get; }

    /// <summary>Gets the strategy legs.</summary>
    public abstract IReadOnlyList<StrategyLeg> Legs { get; }

    /// <summary>Gets the maximum profit (null if unlimited).</summary>
    public abstract decimal? MaxProfit { get; }

    /// <summary>Gets the maximum loss (null if unlimited).</summary>
    public abstract decimal? MaxLoss { get; }

    /// <summary>Gets the break-even price(s).</summary>
    public abstract IReadOnlyList<decimal> BreakEvenPrices { get; }

    /// <summary>Gets the net debit (positive) or credit (negative).</summary>
    public abstract decimal NetPremium { get; }

    /// <summary>Gets whether this is a debit strategy.</summary>
    public bool IsDebit => NetPremium > 0;

    /// <summary>Gets whether this is a credit strategy.</summary>
    public bool IsCredit => NetPremium < 0;

    /// <summary>Gets the aggregated Greeks for the strategy.</summary>
    public virtual OptionGreeks AggregateGreeks
    {
        get
        {
            var result = new OptionGreeks();
            foreach (var leg in Legs)
            {
                var scaled = leg.Contract.Greeks.Scale(leg.Quantity * leg.Contract.Multiplier);
                result += scaled;
            }
            return result;
        }
    }

    /// <summary>
    /// Calculates the P&L at a given underlying price at expiration.
    /// </summary>
    public abstract decimal ProfitAtExpiration(decimal underlyingPrice);

    /// <summary>
    /// Calculates the P&L at various prices for charting.
    /// </summary>
    public IReadOnlyList<(decimal Price, decimal PnL)> GetPayoffCurve(
        decimal minPrice,
        decimal maxPrice,
        int points = 100)
    {
        var result = new List<(decimal Price, decimal PnL)>();
        var step = (maxPrice - minPrice) / (points - 1);

        for (var i = 0; i < points; i++)
        {
            var price = minPrice + step * i;
            var pnl = ProfitAtExpiration(price);
            result.Add((price, pnl));
        }

        return result;
    }

    /// <summary>
    /// Calculates the risk/reward ratio.
    /// </summary>
    public decimal? RiskRewardRatio
    {
        get
        {
            if (!MaxLoss.HasValue || !MaxProfit.HasValue || MaxLoss.Value == 0)
                return null;
            return MaxProfit.Value / Math.Abs(MaxLoss.Value);
        }
    }
}

/// <summary>
/// A single leg of an option strategy.
/// </summary>
public sealed class StrategyLeg
{
    /// <summary>Gets or sets the option contract.</summary>
    public OptionContract Contract { get; set; } = new();

    /// <summary>Gets or sets the quantity (positive = long, negative = short).</summary>
    public int Quantity { get; set; }

    /// <summary>Gets whether this is a long leg.</summary>
    public bool IsLong => Quantity > 0;

    /// <summary>Gets whether this is a short leg.</summary>
    public bool IsShort => Quantity < 0;

    /// <summary>Gets the leg cost (positive = debit, negative = credit).</summary>
    public decimal Cost
    {
        get
        {
            var price = IsLong ? Contract.Ask : Contract.Bid;
            return price * Quantity * Contract.Multiplier;
        }
    }
}

/// <summary>
/// Bull Call Spread: Buy lower strike call, sell higher strike call.
/// </summary>
public sealed class BullCallSpread : OptionStrategy
{
    private readonly StrategyLeg _longCall;
    private readonly StrategyLeg _shortCall;

    public BullCallSpread(OptionContract longCall, OptionContract shortCall)
    {
        if (longCall.StrikePrice >= shortCall.StrikePrice)
            throw new ArgumentException("Long call strike must be lower than short call strike");

        _longCall = new StrategyLeg { Contract = longCall, Quantity = 1 };
        _shortCall = new StrategyLeg { Contract = shortCall, Quantity = -1 };
    }

    public override string Name => "Bull Call Spread";
    public override string Description => "Bullish strategy with limited risk and limited profit";

    public override IReadOnlyList<StrategyLeg> Legs => new[] { _longCall, _shortCall };

    public override decimal NetPremium => _longCall.Cost + _shortCall.Cost;

    public override decimal? MaxProfit =>
        (_shortCall.Contract.StrikePrice - _longCall.Contract.StrikePrice) *
        _longCall.Contract.Multiplier - NetPremium;

    public override decimal? MaxLoss => NetPremium;

    public override IReadOnlyList<decimal> BreakEvenPrices =>
        new[] { _longCall.Contract.StrikePrice + NetPremium / _longCall.Contract.Multiplier };

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        var longValue = Math.Max(0, underlyingPrice - _longCall.Contract.StrikePrice);
        var shortValue = Math.Max(0, underlyingPrice - _shortCall.Contract.StrikePrice);
        return (longValue - shortValue) * _longCall.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Bear Put Spread: Buy higher strike put, sell lower strike put.
/// </summary>
public sealed class BearPutSpread : OptionStrategy
{
    private readonly StrategyLeg _longPut;
    private readonly StrategyLeg _shortPut;

    public BearPutSpread(OptionContract longPut, OptionContract shortPut)
    {
        if (longPut.StrikePrice <= shortPut.StrikePrice)
            throw new ArgumentException("Long put strike must be higher than short put strike");

        _longPut = new StrategyLeg { Contract = longPut, Quantity = 1 };
        _shortPut = new StrategyLeg { Contract = shortPut, Quantity = -1 };
    }

    public override string Name => "Bear Put Spread";
    public override string Description => "Bearish strategy with limited risk and limited profit";

    public override IReadOnlyList<StrategyLeg> Legs => new[] { _longPut, _shortPut };

    public override decimal NetPremium => _longPut.Cost + _shortPut.Cost;

    public override decimal? MaxProfit =>
        (_longPut.Contract.StrikePrice - _shortPut.Contract.StrikePrice) *
        _longPut.Contract.Multiplier - NetPremium;

    public override decimal? MaxLoss => NetPremium;

    public override IReadOnlyList<decimal> BreakEvenPrices =>
        new[] { _longPut.Contract.StrikePrice - NetPremium / _longPut.Contract.Multiplier };

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        var longValue = Math.Max(0, _longPut.Contract.StrikePrice - underlyingPrice);
        var shortValue = Math.Max(0, _shortPut.Contract.StrikePrice - underlyingPrice);
        return (longValue - shortValue) * _longPut.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Straddle: Buy both call and put at same strike.
/// </summary>
public sealed class Straddle : OptionStrategy
{
    private readonly StrategyLeg _call;
    private readonly StrategyLeg _put;
    private readonly decimal _strike;

    public Straddle(OptionContract call, OptionContract put)
    {
        if (call.StrikePrice != put.StrikePrice)
            throw new ArgumentException("Call and put must have same strike");

        _strike = call.StrikePrice;
        _call = new StrategyLeg { Contract = call, Quantity = 1 };
        _put = new StrategyLeg { Contract = put, Quantity = 1 };
    }

    public override string Name => "Long Straddle";
    public override string Description => "Profits from large moves in either direction";

    public override IReadOnlyList<StrategyLeg> Legs => new[] { _call, _put };

    public override decimal NetPremium => _call.Cost + _put.Cost;

    public override decimal? MaxProfit => null; // Unlimited on upside

    public override decimal? MaxLoss => NetPremium;

    public override IReadOnlyList<decimal> BreakEvenPrices => new[]
    {
        _strike - NetPremium / _call.Contract.Multiplier,
        _strike + NetPremium / _call.Contract.Multiplier
    };

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        var callValue = Math.Max(0, underlyingPrice - _call.Contract.StrikePrice);
        var putValue = Math.Max(0, _put.Contract.StrikePrice - underlyingPrice);
        return (callValue + putValue) * _call.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Strangle: Buy OTM call and OTM put.
/// </summary>
public sealed class Strangle : OptionStrategy
{
    private readonly StrategyLeg _call;
    private readonly StrategyLeg _put;

    public Strangle(OptionContract call, OptionContract put)
    {
        if (call.StrikePrice <= put.StrikePrice)
            throw new ArgumentException("Call strike must be higher than put strike");

        _call = new StrategyLeg { Contract = call, Quantity = 1 };
        _put = new StrategyLeg { Contract = put, Quantity = 1 };
    }

    public override string Name => "Long Strangle";
    public override string Description => "Profits from large moves, cheaper than straddle";

    public override IReadOnlyList<StrategyLeg> Legs => new[] { _call, _put };

    public override decimal NetPremium => _call.Cost + _put.Cost;

    public override decimal? MaxProfit => null; // Unlimited on upside

    public override decimal? MaxLoss => NetPremium;

    public override IReadOnlyList<decimal> BreakEvenPrices => new[]
    {
        _put.Contract.StrikePrice - NetPremium / _call.Contract.Multiplier,
        _call.Contract.StrikePrice + NetPremium / _call.Contract.Multiplier
    };

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        var callValue = Math.Max(0, underlyingPrice - _call.Contract.StrikePrice);
        var putValue = Math.Max(0, _put.Contract.StrikePrice - underlyingPrice);
        return (callValue + putValue) * _call.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Iron Condor: Sell OTM put spread + sell OTM call spread.
/// </summary>
public sealed class IronCondor : OptionStrategy
{
    private readonly StrategyLeg _longPut;
    private readonly StrategyLeg _shortPut;
    private readonly StrategyLeg _shortCall;
    private readonly StrategyLeg _longCall;

    public IronCondor(
        OptionContract longPut,   // Lowest strike
        OptionContract shortPut,  // Lower middle
        OptionContract shortCall, // Upper middle
        OptionContract longCall)  // Highest strike
    {
        if (longPut.StrikePrice >= shortPut.StrikePrice ||
            shortPut.StrikePrice >= shortCall.StrikePrice ||
            shortCall.StrikePrice >= longCall.StrikePrice)
        {
            throw new ArgumentException("Strikes must be in ascending order: longPut < shortPut < shortCall < longCall");
        }

        _longPut = new StrategyLeg { Contract = longPut, Quantity = 1 };
        _shortPut = new StrategyLeg { Contract = shortPut, Quantity = -1 };
        _shortCall = new StrategyLeg { Contract = shortCall, Quantity = -1 };
        _longCall = new StrategyLeg { Contract = longCall, Quantity = 1 };
    }

    public override string Name => "Iron Condor";
    public override string Description => "Profits from low volatility, price staying in range";

    public override IReadOnlyList<StrategyLeg> Legs =>
        new[] { _longPut, _shortPut, _shortCall, _longCall };

    public override decimal NetPremium =>
        _longPut.Cost + _shortPut.Cost + _shortCall.Cost + _longCall.Cost;

    public override decimal? MaxProfit => -NetPremium; // Credit received

    public override decimal? MaxLoss
    {
        get
        {
            var putSpreadWidth = _shortPut.Contract.StrikePrice - _longPut.Contract.StrikePrice;
            var callSpreadWidth = _longCall.Contract.StrikePrice - _shortCall.Contract.StrikePrice;
            var maxWidth = Math.Max(putSpreadWidth, callSpreadWidth);
            return maxWidth * _longPut.Contract.Multiplier + NetPremium;
        }
    }

    public override IReadOnlyList<decimal> BreakEvenPrices => new[]
    {
        _shortPut.Contract.StrikePrice + NetPremium / _longPut.Contract.Multiplier,
        _shortCall.Contract.StrikePrice - NetPremium / _longPut.Contract.Multiplier
    };

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        var longPutValue = Math.Max(0, _longPut.Contract.StrikePrice - underlyingPrice);
        var shortPutValue = Math.Max(0, _shortPut.Contract.StrikePrice - underlyingPrice);
        var shortCallValue = Math.Max(0, underlyingPrice - _shortCall.Contract.StrikePrice);
        var longCallValue = Math.Max(0, underlyingPrice - _longCall.Contract.StrikePrice);

        return (longPutValue - shortPutValue - shortCallValue + longCallValue) *
            _longPut.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Butterfly Spread: Long 1 lower, short 2 middle, long 1 higher strike.
/// </summary>
public sealed class ButterflySpread : OptionStrategy
{
    private readonly StrategyLeg _lowerLong;
    private readonly StrategyLeg _middleShort;
    private readonly StrategyLeg _upperLong;
    private readonly OptionType _type;

    public ButterflySpread(
        OptionContract lower,
        OptionContract middle,
        OptionContract upper,
        OptionType type)
    {
        if (lower.StrikePrice >= middle.StrikePrice || middle.StrikePrice >= upper.StrikePrice)
            throw new ArgumentException("Strikes must be in ascending order");

        _type = type;
        _lowerLong = new StrategyLeg { Contract = lower, Quantity = 1 };
        _middleShort = new StrategyLeg { Contract = middle, Quantity = -2 };
        _upperLong = new StrategyLeg { Contract = upper, Quantity = 1 };
    }

    public override string Name => $"Butterfly Spread ({_type})";
    public override string Description => "Profits when price stays near middle strike";

    public override IReadOnlyList<StrategyLeg> Legs =>
        new[] { _lowerLong, _middleShort, _upperLong };

    public override decimal NetPremium =>
        _lowerLong.Cost + _middleShort.Cost + _upperLong.Cost;

    public override decimal? MaxProfit =>
        (_middleShort.Contract.StrikePrice - _lowerLong.Contract.StrikePrice) *
        _lowerLong.Contract.Multiplier - NetPremium;

    public override decimal? MaxLoss => NetPremium;

    public override IReadOnlyList<decimal> BreakEvenPrices => new[]
    {
        _lowerLong.Contract.StrikePrice + NetPremium / _lowerLong.Contract.Multiplier,
        _upperLong.Contract.StrikePrice - NetPremium / _lowerLong.Contract.Multiplier
    };

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        decimal lowerValue, middleValue, upperValue;

        if (_type == OptionType.Call)
        {
            lowerValue = Math.Max(0, underlyingPrice - _lowerLong.Contract.StrikePrice);
            middleValue = Math.Max(0, underlyingPrice - _middleShort.Contract.StrikePrice);
            upperValue = Math.Max(0, underlyingPrice - _upperLong.Contract.StrikePrice);
        }
        else
        {
            lowerValue = Math.Max(0, _lowerLong.Contract.StrikePrice - underlyingPrice);
            middleValue = Math.Max(0, _middleShort.Contract.StrikePrice - underlyingPrice);
            upperValue = Math.Max(0, _upperLong.Contract.StrikePrice - underlyingPrice);
        }

        return (lowerValue - 2 * middleValue + upperValue) *
            _lowerLong.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Calendar Spread: Sell near-term, buy longer-term at same strike.
/// </summary>
public sealed class CalendarSpread : OptionStrategy
{
    private readonly StrategyLeg _nearTerm;
    private readonly StrategyLeg _farTerm;

    public CalendarSpread(OptionContract nearTerm, OptionContract farTerm)
    {
        if (nearTerm.StrikePrice != farTerm.StrikePrice)
            throw new ArgumentException("Contracts must have same strike");
        if (nearTerm.ExpirationDate >= farTerm.ExpirationDate)
            throw new ArgumentException("Near-term must expire before far-term");

        _nearTerm = new StrategyLeg { Contract = nearTerm, Quantity = -1 };
        _farTerm = new StrategyLeg { Contract = farTerm, Quantity = 1 };
    }

    public override string Name => "Calendar Spread";
    public override string Description => "Profits from time decay differential";

    public override IReadOnlyList<StrategyLeg> Legs => new[] { _nearTerm, _farTerm };

    public override decimal NetPremium => _nearTerm.Cost + _farTerm.Cost;

    public override decimal? MaxProfit => null; // Depends on IV at near-term expiration

    public override decimal? MaxLoss => NetPremium;

    public override IReadOnlyList<decimal> BreakEvenPrices =>
        Array.Empty<decimal>(); // Complex, depends on IV at near-term expiration

    public override decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        // At near-term expiration, far-term still has time value
        // This is a simplified calculation assuming far-term at same IV
        var nearValue = _nearTerm.Contract.OptionType == OptionType.Call
            ? Math.Max(0, underlyingPrice - _nearTerm.Contract.StrikePrice)
            : Math.Max(0, _nearTerm.Contract.StrikePrice - underlyingPrice);

        return -nearValue * _nearTerm.Contract.Multiplier - NetPremium;
    }
}

/// <summary>
/// Covered Call: Long stock + short call.
/// </summary>
public sealed class CoveredCall
{
    private readonly decimal _stockPrice;
    private readonly int _shareQuantity;
    private readonly OptionContract _shortCall;

    public CoveredCall(decimal stockPrice, int shareQuantity, OptionContract shortCall)
    {
        if (shareQuantity != shortCall.Multiplier)
            throw new ArgumentException($"Share quantity must match contract multiplier ({shortCall.Multiplier})");

        _stockPrice = stockPrice;
        _shareQuantity = shareQuantity;
        _shortCall = shortCall;
    }

    public string Name => "Covered Call";
    public string Description => "Income strategy on long stock";

    public decimal PremiumReceived => _shortCall.Bid * _shortCall.Multiplier;
    public decimal CostBasis => _stockPrice * _shareQuantity;
    public decimal EffectiveCostBasis => CostBasis - PremiumReceived;

    public decimal MaxProfit =>
        (_shortCall.StrikePrice - _stockPrice) * _shareQuantity + PremiumReceived;

    public decimal BreakEvenPrice => _stockPrice - _shortCall.Bid;

    public decimal ProfitAtExpiration(decimal underlyingPrice)
    {
        var stockPnL = (underlyingPrice - _stockPrice) * _shareQuantity;
        var callPnL = -Math.Max(0, underlyingPrice - _shortCall.StrikePrice) * _shortCall.Multiplier
            + PremiumReceived;
        return stockPnL + callPnL;
    }

    public decimal? MaxLoss => EffectiveCostBasis; // Stock goes to zero
}

/// <summary>
/// Factory for creating common option strategies.
/// </summary>
public static class StrategyFactory
{
    /// <summary>
    /// Creates a bull call spread from an options chain.
    /// </summary>
    public static BullCallSpread? CreateBullCallSpread(
        OptionExpiration expiration,
        decimal underlyingPrice,
        decimal targetDeltaLong = 0.40m,
        decimal spreadWidth = 5m)
    {
        var calls = expiration.Calls.OrderBy(c => c.StrikePrice).ToList();
        var longCall = calls.FirstOrDefault(c =>
            c.StrikePrice <= underlyingPrice &&
            c.Greeks.Delta >= targetDeltaLong);

        if (longCall is null) return null;

        var shortCall = calls.FirstOrDefault(c =>
            c.StrikePrice >= longCall.StrikePrice + spreadWidth);

        if (shortCall is null) return null;

        return new BullCallSpread(longCall, shortCall);
    }

    /// <summary>
    /// Creates an iron condor from an options chain.
    /// </summary>
    public static IronCondor? CreateIronCondor(
        OptionExpiration expiration,
        decimal underlyingPrice,
        decimal targetDelta = 0.15m,
        decimal wingWidth = 5m)
    {
        var calls = expiration.Calls.OrderBy(c => c.StrikePrice).ToList();
        var puts = expiration.Puts.OrderByDescending(p => p.StrikePrice).ToList();

        // Find short put (target delta)
        var shortPut = puts.FirstOrDefault(p =>
            p.StrikePrice < underlyingPrice &&
            Math.Abs(p.Greeks.Delta) <= targetDelta);

        if (shortPut is null) return null;

        // Find long put (wing)
        var longPut = puts.FirstOrDefault(p =>
            p.StrikePrice <= shortPut.StrikePrice - wingWidth);

        if (longPut is null) return null;

        // Find short call (target delta)
        var shortCall = calls.FirstOrDefault(c =>
            c.StrikePrice > underlyingPrice &&
            c.Greeks.Delta <= targetDelta);

        if (shortCall is null) return null;

        // Find long call (wing)
        var longCall = calls.FirstOrDefault(c =>
            c.StrikePrice >= shortCall.StrikePrice + wingWidth);

        if (longCall is null) return null;

        return new IronCondor(longPut, shortPut, shortCall, longCall);
    }

    /// <summary>
    /// Creates an ATM straddle from an options chain.
    /// </summary>
    public static Straddle? CreateAtmStraddle(OptionExpiration expiration, decimal underlyingPrice)
    {
        var atmStrike = expiration.AtTheMoneyStrike(underlyingPrice);
        var call = expiration.GetCall(atmStrike);
        var put = expiration.GetPut(atmStrike);

        if (call is null || put is null) return null;

        return new Straddle(call, put);
    }
}
