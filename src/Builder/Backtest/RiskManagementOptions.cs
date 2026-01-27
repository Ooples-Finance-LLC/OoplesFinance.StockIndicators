namespace OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Stop loss type for risk management.
/// </summary>
public enum StopLossType
{
    /// <summary>
    /// No stop loss.
    /// </summary>
    None,

    /// <summary>
    /// Fixed percentage stop loss from entry.
    /// </summary>
    FixedPercent,

    /// <summary>
    /// Fixed dollar amount stop loss from entry.
    /// </summary>
    FixedDollar,

    /// <summary>
    /// Trailing percentage stop loss.
    /// </summary>
    TrailingPercent,

    /// <summary>
    /// Trailing dollar amount stop loss.
    /// </summary>
    TrailingDollar,

    /// <summary>
    /// ATR-based stop loss.
    /// </summary>
    AtrBased
}

/// <summary>
/// Take profit type for risk management.
/// </summary>
public enum TakeProfitType
{
    /// <summary>
    /// No take profit.
    /// </summary>
    None,

    /// <summary>
    /// Fixed percentage take profit from entry.
    /// </summary>
    FixedPercent,

    /// <summary>
    /// Fixed dollar amount take profit from entry.
    /// </summary>
    FixedDollar,

    /// <summary>
    /// Risk/reward ratio based take profit.
    /// </summary>
    RiskRewardRatio,

    /// <summary>
    /// ATR-based take profit.
    /// </summary>
    AtrBased
}

/// <summary>
/// Options for risk management in backtests.
/// </summary>
public sealed class RiskManagementOptions
{
    /// <summary>
    /// Gets or sets the stop loss type.
    /// </summary>
    public StopLossType StopLossType { get; set; } = StopLossType.None;

    /// <summary>
    /// Gets or sets the stop loss value (percentage or dollar amount depending on type).
    /// </summary>
    public double? StopLossValue { get; set; }

    /// <summary>
    /// Gets or sets the take profit type.
    /// </summary>
    public TakeProfitType TakeProfitType { get; set; } = TakeProfitType.None;

    /// <summary>
    /// Gets or sets the take profit value (percentage, dollar amount, or R:R ratio depending on type).
    /// </summary>
    public double? TakeProfitValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum drawdown percentage (0-100) before stopping trading.
    /// Null means no drawdown limit.
    /// </summary>
    public double? MaxDrawdownPercent { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent positions.
    /// Null means unlimited.
    /// </summary>
    public int? MaxPositions { get; set; }

    /// <summary>
    /// Gets or sets the daily loss limit as a percentage of starting equity (0-100).
    /// Trading stops for the day when this limit is reached.
    /// </summary>
    public double? DailyLossLimitPercent { get; set; }

    /// <summary>
    /// Gets or sets the daily profit target as a percentage of starting equity (0-100).
    /// Trading stops for the day when this target is reached.
    /// </summary>
    public double? DailyProfitTargetPercent { get; set; }

    /// <summary>
    /// Gets or sets the ATR period for ATR-based stop/profit.
    /// </summary>
    public int? AtrPeriod { get; set; } = 14;

    /// <summary>
    /// Gets or sets the ATR multiplier for ATR-based stop loss.
    /// </summary>
    public double? AtrStopMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the ATR multiplier for ATR-based take profit.
    /// </summary>
    public double? AtrProfitMultiplier { get; set; } = 3.0;

    /// <summary>
    /// Gets or sets whether to use time-based exit (close at end of day).
    /// </summary>
    public bool? CloseAtEndOfDay { get; set; }

    /// <summary>
    /// Gets or sets the maximum holding period in bars.
    /// Position is closed after this many bars regardless of profit/loss.
    /// </summary>
    public int? MaxHoldingPeriod { get; set; }

    /// <summary>
    /// Creates a fixed percentage stop loss.
    /// </summary>
    /// <param name="percent">Stop loss percentage (e.g., 2 for 2%).</param>
    public static RiskManagementOptions FixedStopLoss(double percent) => new()
    {
        StopLossType = StopLossType.FixedPercent,
        StopLossValue = percent
    };

    /// <summary>
    /// Creates a trailing percentage stop loss.
    /// </summary>
    /// <param name="percent">Trailing stop percentage (e.g., 5 for 5%).</param>
    public static RiskManagementOptions TrailingStop(double percent) => new()
    {
        StopLossType = StopLossType.TrailingPercent,
        StopLossValue = percent
    };

    /// <summary>
    /// Creates stop loss and take profit based on risk/reward ratio.
    /// </summary>
    /// <param name="stopPercent">Stop loss percentage.</param>
    /// <param name="riskRewardRatio">Risk/reward ratio (e.g., 2 for 1:2 risk/reward).</param>
    public static RiskManagementOptions RiskReward(double stopPercent, double riskRewardRatio) => new()
    {
        StopLossType = StopLossType.FixedPercent,
        StopLossValue = stopPercent,
        TakeProfitType = TakeProfitType.RiskRewardRatio,
        TakeProfitValue = riskRewardRatio
    };
}
