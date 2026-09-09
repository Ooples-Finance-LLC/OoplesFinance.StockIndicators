namespace OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Comprehensive results from a backtest run.
/// </summary>
public sealed class BacktestResults
{
    /// <summary>
    /// Gets or sets the start date of the backtest.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the backtest.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Gets or sets the initial capital.
    /// </summary>
    public double InitialCapital { get; set; }

    /// <summary>
    /// Gets or sets the final capital (equity).
    /// </summary>
    public double FinalCapital { get; set; }

    /// <summary>
    /// Gets the total net profit (FinalCapital - InitialCapital).
    /// </summary>
    public double NetProfit => FinalCapital - InitialCapital;

    /// <summary>
    /// Gets the total return as a percentage.
    /// </summary>
    public double TotalReturnPercent => InitialCapital > 0 ? (NetProfit / InitialCapital) * 100 : 0;

    /// <summary>
    /// Gets or sets the annualized return percentage.
    /// </summary>
    public double AnnualizedReturnPercent { get; set; }

    /// <summary>
    /// Gets or sets the total number of trades executed.
    /// </summary>
    public int TotalTrades { get; set; }

    /// <summary>
    /// Gets or sets the number of winning trades.
    /// </summary>
    public int WinningTrades { get; set; }

    /// <summary>
    /// Gets or sets the number of losing trades.
    /// </summary>
    public int LosingTrades { get; set; }

    /// <summary>
    /// Gets the win rate as a percentage (0-100).
    /// </summary>
    public double WinRatePercent => TotalTrades > 0 ? ((double)WinningTrades / TotalTrades) * 100 : 0;

    /// <summary>
    /// Gets or sets the average profit per winning trade.
    /// </summary>
    public double AverageWin { get; set; }

    /// <summary>
    /// Gets or sets the average loss per losing trade.
    /// </summary>
    public double AverageLoss { get; set; }

    /// <summary>
    /// Gets or sets the profit factor (gross profit / gross loss).
    /// </summary>
    public double ProfitFactor { get; set; }

    /// <summary>
    /// Gets or sets the expectancy (average profit per trade).
    /// </summary>
    public double Expectancy { get; set; }

    /// <summary>
    /// Gets or sets the maximum drawdown as a dollar amount.
    /// </summary>
    public double MaxDrawdown { get; set; }

    /// <summary>
    /// Gets or sets the maximum drawdown as a percentage.
    /// </summary>
    public double MaxDrawdownPercent { get; set; }

    /// <summary>
    /// Gets or sets the maximum drawdown duration in days.
    /// </summary>
    public int MaxDrawdownDurationDays { get; set; }

    /// <summary>
    /// Gets or sets the Sharpe ratio (risk-adjusted return using standard deviation).
    /// </summary>
    public double SharpeRatio { get; set; }

    /// <summary>
    /// Gets or sets the Sortino ratio (risk-adjusted return using downside deviation).
    /// </summary>
    public double SortinoRatio { get; set; }

    /// <summary>
    /// Gets or sets the Calmar ratio (annualized return / max drawdown).
    /// </summary>
    public double CalmarRatio { get; set; }

    /// <summary>
    /// Gets or sets the total fees paid during the backtest.
    /// </summary>
    public double TotalFees { get; set; }

    /// <summary>
    /// Gets or sets the total slippage incurred during the backtest.
    /// </summary>
    public double TotalSlippage { get; set; }

    /// <summary>
    /// Gets or sets the longest winning streak (consecutive winning trades).
    /// </summary>
    public int LongestWinningStreak { get; set; }

    /// <summary>
    /// Gets or sets the longest losing streak (consecutive losing trades).
    /// </summary>
    public int LongestLosingStreak { get; set; }

    /// <summary>
    /// Gets or sets the largest single winning trade.
    /// </summary>
    public double LargestWin { get; set; }

    /// <summary>
    /// Gets or sets the largest single losing trade.
    /// </summary>
    public double LargestLoss { get; set; }

    /// <summary>
    /// Gets or sets the average trade duration in bars.
    /// </summary>
    public double AverageTradeDurationBars { get; set; }

    /// <summary>
    /// Gets or sets the daily equity curve.
    /// </summary>
    public IReadOnlyList<EquityPoint> EquityCurve { get; set; } = Array.Empty<EquityPoint>();

    /// <summary>
    /// Gets or sets the list of all trades.
    /// </summary>
    public IReadOnlyList<TradeRecord> Trades { get; set; } = Array.Empty<TradeRecord>();

    /// <summary>
    /// Gets or sets the monthly returns.
    /// </summary>
    public IReadOnlyDictionary<string, double> MonthlyReturns { get; set; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets the number of break-even trades.
    /// </summary>
    public int BreakEvenTrades => TotalTrades - WinningTrades - LosingTrades;

    /// <summary>
    /// Gets the average risk/reward ratio of trades.
    /// </summary>
    public double AverageRiskRewardRatio => AverageLoss != 0 ? Math.Abs(AverageWin / AverageLoss) : 0;

    /// <summary>
    /// Gets the recovery factor (net profit / max drawdown).
    /// </summary>
    public double RecoveryFactor => MaxDrawdown != 0 ? Math.Abs(NetProfit / MaxDrawdown) : 0;

    /// <summary>
    /// Gets or sets the CAGR (Compound Annual Growth Rate).
    /// </summary>
    public double Cagr { get; set; }

    /// <summary>
    /// Gets or sets the benchmark return for comparison (if benchmark was specified).
    /// </summary>
    public double? BenchmarkReturn { get; set; }

    /// <summary>
    /// Gets the alpha (excess return over benchmark).
    /// </summary>
    public double? Alpha => BenchmarkReturn.HasValue ? TotalReturnPercent - BenchmarkReturn.Value : null;

    /// <summary>
    /// Gets a summary string of the backtest results.
    /// </summary>
    public override string ToString()
    {
        return $"Backtest Results: {StartDate:d} to {EndDate:d}\n" +
               $"  Net Profit: {NetProfit:C2} ({TotalReturnPercent:F2}%)\n" +
               $"  Total Trades: {TotalTrades} (Win Rate: {WinRatePercent:F1}%)\n" +
               $"  Profit Factor: {ProfitFactor:F2}\n" +
               $"  Max Drawdown: {MaxDrawdownPercent:F2}%\n" +
               $"  Sharpe Ratio: {SharpeRatio:F2}\n" +
               $"  Sortino Ratio: {SortinoRatio:F2}";
    }
}

/// <summary>
/// A point on the equity curve.
/// </summary>
public struct EquityPoint
{
    /// <summary>
    /// Creates a new equity point.
    /// </summary>
    public EquityPoint(DateTime date, double equity, double drawdown, double drawdownPercent)
    {
        Date = date;
        Equity = equity;
        Drawdown = drawdown;
        DrawdownPercent = drawdownPercent;
    }

    /// <summary>
    /// Gets the date.
    /// </summary>
    public DateTime Date { get; }

    /// <summary>
    /// Gets the equity value.
    /// </summary>
    public double Equity { get; }

    /// <summary>
    /// Gets the drawdown amount.
    /// </summary>
    public double Drawdown { get; }

    /// <summary>
    /// Gets the drawdown percentage.
    /// </summary>
    public double DrawdownPercent { get; }
}

/// <summary>
/// Record of a single trade.
/// </summary>
public sealed class TradeRecord
{
    /// <summary>
    /// Gets or sets the entry date/time.
    /// </summary>
    public DateTime EntryTime { get; set; }

    /// <summary>
    /// Gets or sets the exit date/time.
    /// </summary>
    public DateTime ExitTime { get; set; }

    /// <summary>
    /// Gets or sets the symbol traded.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trade direction (1 for long, -1 for short).
    /// </summary>
    public int Direction { get; set; }

    /// <summary>
    /// Gets or sets the entry price.
    /// </summary>
    public double EntryPrice { get; set; }

    /// <summary>
    /// Gets or sets the exit price.
    /// </summary>
    public double ExitPrice { get; set; }

    /// <summary>
    /// Gets or sets the position size (number of shares/units).
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Gets or sets the gross profit/loss.
    /// </summary>
    public double GrossPnL { get; set; }

    /// <summary>
    /// Gets or sets the fees for this trade.
    /// </summary>
    public double Fees { get; set; }

    /// <summary>
    /// Gets or sets the slippage for this trade.
    /// </summary>
    public double Slippage { get; set; }

    /// <summary>
    /// Gets the net profit/loss (GrossPnL - Fees - Slippage).
    /// </summary>
    public double NetPnL => GrossPnL - Fees - Slippage;

    /// <summary>
    /// Gets or sets the return percentage for this trade.
    /// </summary>
    public double ReturnPercent { get; set; }

    /// <summary>
    /// Gets or sets the duration of the trade in bars.
    /// </summary>
    public int DurationBars { get; set; }

    /// <summary>
    /// Gets or sets the exit reason.
    /// </summary>
    public TradeExitReason ExitReason { get; set; }

    /// <summary>
    /// Gets or sets the signal name that triggered entry.
    /// </summary>
    public string? EntrySignal { get; set; }

    /// <summary>
    /// Gets or sets the signal name that triggered exit (if any).
    /// </summary>
    public string? ExitSignal { get; set; }

    /// <summary>
    /// Gets or sets the maximum adverse excursion (MAE) - maximum unrealized loss.
    /// </summary>
    public double MaxAdverseExcursion { get; set; }

    /// <summary>
    /// Gets or sets the maximum favorable excursion (MFE) - maximum unrealized profit.
    /// </summary>
    public double MaxFavorableExcursion { get; set; }
}

/// <summary>
/// Reason for trade exit.
/// </summary>
public enum TradeExitReason
{
    /// <summary>
    /// Exit signal triggered.
    /// </summary>
    Signal,

    /// <summary>
    /// Stop loss hit.
    /// </summary>
    StopLoss,

    /// <summary>
    /// Take profit hit.
    /// </summary>
    TakeProfit,

    /// <summary>
    /// Trailing stop hit.
    /// </summary>
    TrailingStop,

    /// <summary>
    /// Maximum holding period reached.
    /// </summary>
    MaxHoldingPeriod,

    /// <summary>
    /// End of day exit.
    /// </summary>
    EndOfDay,

    /// <summary>
    /// End of backtest period.
    /// </summary>
    EndOfBacktest,

    /// <summary>
    /// Risk limit exceeded (drawdown, daily loss, etc.).
    /// </summary>
    RiskLimit,

    /// <summary>
    /// Manual or unknown exit.
    /// </summary>
    Other
}
