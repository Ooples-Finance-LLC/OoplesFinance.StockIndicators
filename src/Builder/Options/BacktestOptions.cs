using OoplesFinance.StockIndicators.Builder.Backtest;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for backtesting configuration.
/// </summary>
public sealed class BacktestOptions
{
    /// <summary>
    /// Gets or sets the backtest start date.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the backtest end date.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the initial capital. Defaults to 100,000.
    /// </summary>
    public double? InitialCapital { get; set; } = 100_000d;

    /// <summary>
    /// Gets or sets the fee model.
    /// </summary>
    public FeeModel? FeeModel { get; set; }

    /// <summary>
    /// Gets or sets the slippage model.
    /// </summary>
    public SlippageModel? SlippageModel { get; set; }

    /// <summary>
    /// Gets or sets the fixed fee amount (when using Fixed fee model).
    /// </summary>
    public double? FixedFee { get; set; }

    /// <summary>
    /// Gets or sets the percentage fee (when using Percent fee model).
    /// </summary>
    public double? PercentFee { get; set; }

    /// <summary>
    /// Gets or sets the fixed slippage ticks (when using FixedTicks slippage model).
    /// </summary>
    public int? SlippageTicks { get; set; }

    /// <summary>
    /// Gets or sets the percentage slippage (when using Percent slippage model).
    /// </summary>
    public double? SlippagePercent { get; set; }

    /// <summary>
    /// Gets or sets the position sizing options.
    /// </summary>
    public PositionSizingOptions? PositionSizing { get; set; }

    /// <summary>
    /// Gets or sets the risk management options.
    /// </summary>
    public RiskManagementOptions? RiskManagement { get; set; }

    /// <summary>
    /// Gets or sets the number of bars to skip for indicator warmup.
    /// If null, warmup is automatically detected based on indicator lookback periods.
    /// </summary>
    public int? WarmupBars { get; set; }

    /// <summary>
    /// Gets or sets the benchmark symbol for comparison (e.g., "SPY").
    /// </summary>
    public string? BenchmarkSymbol { get; set; }

    /// <summary>
    /// Gets or sets whether to include detailed trade-by-trade analysis.
    /// Defaults to true.
    /// </summary>
    public bool IncludeTradeDetails { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the equity curve in results.
    /// Defaults to true.
    /// </summary>
    public bool IncludeEquityCurve { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum trade interval in bars.
    /// Prevents rapid-fire trading. Defaults to 1.
    /// </summary>
    public int? MinTradingInterval { get; set; } = 1;
}
