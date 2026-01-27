namespace OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Position sizing method for backtesting.
/// </summary>
public enum PositionSizingMethod
{
    /// <summary>
    /// Fixed number of shares/units per trade.
    /// </summary>
    FixedUnits,

    /// <summary>
    /// Fixed dollar amount per trade.
    /// </summary>
    FixedDollar,

    /// <summary>
    /// Percentage of current equity per trade.
    /// </summary>
    PercentOfEquity,

    /// <summary>
    /// Risk-based sizing (risk X% of equity per trade).
    /// </summary>
    RiskPercent,

    /// <summary>
    /// Kelly criterion position sizing.
    /// </summary>
    Kelly,

    /// <summary>
    /// Volatility-adjusted position sizing (ATR-based).
    /// </summary>
    VolatilityAdjusted
}

/// <summary>
/// Options for position sizing in backtests.
/// </summary>
public sealed class PositionSizingOptions
{
    /// <summary>
    /// Gets or sets the position sizing method. Defaults to FixedUnits.
    /// </summary>
    public PositionSizingMethod Method { get; set; } = PositionSizingMethod.FixedUnits;

    /// <summary>
    /// Gets or sets the fixed unit count (when using FixedUnits method). Defaults to 100.
    /// </summary>
    public int? FixedUnits { get; set; } = 100;

    /// <summary>
    /// Gets or sets the fixed dollar amount (when using FixedDollar method).
    /// </summary>
    public double? FixedDollarAmount { get; set; }

    /// <summary>
    /// Gets or sets the percentage of equity (when using PercentOfEquity or RiskPercent).
    /// Value should be 0-100. Defaults to 10 (10% of equity).
    /// </summary>
    public double? EquityPercent { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Kelly fraction multiplier (when using Kelly method).
    /// Values less than 1 use fractional Kelly (safer). Defaults to 0.5 (half Kelly).
    /// </summary>
    public double? KellyFraction { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the ATR multiplier for volatility-based sizing.
    /// </summary>
    public double? AtrMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the ATR period for volatility-based sizing.
    /// </summary>
    public int? AtrPeriod { get; set; } = 14;

    /// <summary>
    /// Creates fixed unit position sizing.
    /// </summary>
    /// <param name="units">Number of units per trade.</param>
    public static PositionSizingOptions CreateFixedUnits(int units) => new()
    {
        Method = PositionSizingMethod.FixedUnits,
        FixedUnits = units
    };

    /// <summary>
    /// Creates fixed dollar position sizing.
    /// </summary>
    /// <param name="amount">Dollar amount per trade.</param>
    public static PositionSizingOptions FixedDollar(double amount) => new()
    {
        Method = PositionSizingMethod.FixedDollar,
        FixedDollarAmount = amount
    };

    /// <summary>
    /// Creates percent of equity position sizing.
    /// </summary>
    /// <param name="percent">Percentage of equity (0-100).</param>
    public static PositionSizingOptions PercentOfEquity(double percent) => new()
    {
        Method = PositionSizingMethod.PercentOfEquity,
        EquityPercent = percent
    };

    /// <summary>
    /// Creates risk-based position sizing.
    /// </summary>
    /// <param name="riskPercent">Percentage of equity to risk per trade (0-100).</param>
    public static PositionSizingOptions RiskPercent(double riskPercent) => new()
    {
        Method = PositionSizingMethod.RiskPercent,
        EquityPercent = riskPercent
    };
}
