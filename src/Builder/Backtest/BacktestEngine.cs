using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Engine for running backtests on historical data.
/// </summary>
public sealed class BacktestEngine
{
    private readonly IndicatorRuntime _runtime;
    private readonly BacktestOptions _options;
    private readonly PositionSizingOptions _positionSizing;
    private readonly RiskManagementOptions _riskManagement;
    private readonly StockData _data;
    private readonly List<TradeRecord> _trades = new();
    private readonly List<EquityPoint> _equityCurve = new();
    private readonly Dictionary<string, double> _monthlyReturns = new();

    private double _equity;
    private double _peakEquity;
    private double _maxDrawdown;
    private double _maxDrawdownPercent;
    private DateTime _drawdownStart;
    private int _maxDrawdownDays;
    private Position? _currentPosition;
    private double _totalFees;
    private double _totalSlippage;
    private DateTime _dailyStart;
    private double _dailyStartEquity;

    /// <summary>
    /// Creates a new backtest engine.
    /// </summary>
    /// <param name="runtime">The indicator runtime with computed signals.</param>
    /// <param name="data">The historical stock data.</param>
    /// <param name="options">Backtest configuration options.</param>
    /// <param name="positionSizing">Position sizing configuration.</param>
    /// <param name="riskManagement">Risk management configuration.</param>
    public BacktestEngine(
        IndicatorRuntime runtime,
        StockData data,
        BacktestOptions? options = null,
        PositionSizingOptions? positionSizing = null,
        RiskManagementOptions? riskManagement = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _options = options ?? new BacktestOptions();
        _positionSizing = positionSizing ?? new PositionSizingOptions();
        _riskManagement = riskManagement ?? new RiskManagementOptions();
        _equity = _options.InitialCapital ?? 100_000;
        _peakEquity = _equity;
    }

    /// <summary>
    /// Runs the backtest and returns results.
    /// </summary>
    public BacktestResults Run()
    {
        var startDate = _options.StartDate ?? GetFirstDate();
        var endDate = _options.EndDate ?? GetLastDate();

        _equity = _options.InitialCapital ?? 100_000;
        _peakEquity = _equity;
        _maxDrawdown = 0;
        _maxDrawdownPercent = 0;
        _drawdownStart = startDate;
        _dailyStart = startDate;
        _dailyStartEquity = _equity;

        var snapshot = _runtime.Latest;
        if (snapshot is null)
        {
            return CreateEmptyResults(startDate, endDate);
        }

        var bars = _data.TickerDataList;
        var startIndex = FindBarIndex(bars, startDate);
        var endIndex = FindBarIndex(bars, endDate);

        for (var i = startIndex; i <= endIndex && i < bars.Count; i++)
        {
            var bar = bars[i];
            ProcessBar(bar, i, snapshot);
        }

        // Close any remaining position at end of backtest
        if (_currentPosition is not null)
        {
            var lastBar = bars[Math.Min(endIndex, bars.Count - 1)];
            ClosePosition(lastBar, endIndex, TradeExitReason.EndOfBacktest);
        }

        return CalculateResults(startDate, endDate);
    }

    private void ProcessBar(TickerData bar, int barIndex, IndicatorSnapshot snapshot)
    {
        // Check for new day
        if (bar.Date.Date != _dailyStart.Date)
        {
            _dailyStart = bar.Date;
            _dailyStartEquity = _equity;
        }

        // Update position P&L
        if (_currentPosition is not null)
        {
            UpdatePositionPnL(bar);

            // Check risk management exits
            if (CheckRiskManagementExit(bar, barIndex))
            {
                return; // Position was closed
            }
        }

        // Check for entry signals (only if no position)
        if (_currentPosition is null && !IsDailyLimitReached())
        {
            CheckEntrySignals(bar, barIndex, snapshot);
        }

        // Update equity curve
        UpdateEquityCurve(bar);
    }

    private void CheckEntrySignals(TickerData bar, int barIndex, IndicatorSnapshot snapshot)
    {
        // For now, use simple price-based entry logic
        // In full implementation, this would evaluate configured signals
        // This is a placeholder for signal evaluation
    }

    /// <summary>
    /// Opens a new position (called by signal evaluation).
    /// </summary>
    internal void OpenPosition(TickerData bar, int barIndex, int direction, string? signalName = null)
    {
        if (_currentPosition is not null)
        {
            return; // Already in a position
        }

        var quantity = CalculatePositionSize(bar.Close);
        if (quantity <= 0)
        {
            return;
        }

        var fees = CalculateFees(bar.Close * quantity);
        var slippage = CalculateSlippage(bar.Close, direction);
        var entryPrice = bar.Close + slippage;

        _currentPosition = new Position
        {
            EntryTime = bar.Date,
            EntryPrice = entryPrice,
            EntryBar = barIndex,
            Direction = direction,
            Quantity = quantity,
            EntryFees = fees,
            EntrySlippage = Math.Abs(slippage) * quantity,
            EntrySignal = signalName,
            HighestPrice = entryPrice,
            LowestPrice = entryPrice
        };

        _equity -= fees;
        _totalFees += fees;
        _totalSlippage += Math.Abs(slippage) * quantity;
    }

    private void ClosePosition(TickerData bar, int barIndex, TradeExitReason reason, string? signalName = null)
    {
        if (_currentPosition is null)
        {
            return;
        }

        var pos = _currentPosition;
        var direction = pos.Direction;
        var slippage = CalculateSlippage(bar.Close, -direction); // Opposite direction for exit
        var exitPrice = bar.Close + slippage;
        var fees = CalculateFees(exitPrice * pos.Quantity);

        var grossPnL = (exitPrice - pos.EntryPrice) * pos.Quantity * direction;
        var netPnL = grossPnL - fees - pos.EntryFees;

        var trade = new TradeRecord
        {
            EntryTime = pos.EntryTime,
            ExitTime = bar.Date,
            Symbol = "STOCK",
            Direction = direction,
            EntryPrice = pos.EntryPrice,
            ExitPrice = exitPrice,
            Quantity = pos.Quantity,
            GrossPnL = grossPnL,
            Fees = fees + pos.EntryFees,
            Slippage = pos.EntrySlippage + Math.Abs(slippage) * pos.Quantity,
            ReturnPercent = pos.EntryPrice > 0 ? (exitPrice - pos.EntryPrice) / pos.EntryPrice * 100 * direction : 0,
            DurationBars = barIndex - pos.EntryBar,
            ExitReason = reason,
            EntrySignal = pos.EntrySignal,
            ExitSignal = signalName,
            MaxAdverseExcursion = direction > 0
                ? (pos.EntryPrice - pos.LowestPrice) * pos.Quantity
                : (pos.HighestPrice - pos.EntryPrice) * pos.Quantity,
            MaxFavorableExcursion = direction > 0
                ? (pos.HighestPrice - pos.EntryPrice) * pos.Quantity
                : (pos.EntryPrice - pos.LowestPrice) * pos.Quantity
        };

        _trades.Add(trade);
        _equity += netPnL - fees; // fees already subtracted at entry
        _totalFees += fees;
        _totalSlippage += Math.Abs(slippage) * pos.Quantity;

        // Track monthly returns
        var monthKey = bar.Date.ToString("yyyy-MM");
        if (!_monthlyReturns.ContainsKey(monthKey))
        {
            _monthlyReturns[monthKey] = 0;
        }
        _monthlyReturns[monthKey] += netPnL;

        _currentPosition = null;
    }

    private void UpdatePositionPnL(TickerData bar)
    {
        if (_currentPosition is null)
        {
            return;
        }

        // Track high/low for MAE/MFE
        if (bar.High > _currentPosition.HighestPrice)
        {
            _currentPosition.HighestPrice = bar.High;
        }
        if (bar.Low < _currentPosition.LowestPrice)
        {
            _currentPosition.LowestPrice = bar.Low;
        }
    }

    private bool CheckRiskManagementExit(TickerData bar, int barIndex)
    {
        if (_currentPosition is null)
        {
            return false;
        }

        var pos = _currentPosition;

        // Check stop loss
        if (_riskManagement.StopLossType != StopLossType.None)
        {
            var stopPrice = CalculateStopPrice(pos);
            if ((pos.Direction > 0 && bar.Low <= stopPrice) ||
                (pos.Direction < 0 && bar.High >= stopPrice))
            {
                var reason = _riskManagement.StopLossType == StopLossType.TrailingPercent ||
                             _riskManagement.StopLossType == StopLossType.TrailingDollar
                    ? TradeExitReason.TrailingStop
                    : TradeExitReason.StopLoss;
                ClosePosition(bar, barIndex, reason);
                return true;
            }
        }

        // Check take profit
        if (_riskManagement.TakeProfitType != TakeProfitType.None)
        {
            var targetPrice = CalculateTakeProfitPrice(pos);
            if ((pos.Direction > 0 && bar.High >= targetPrice) ||
                (pos.Direction < 0 && bar.Low <= targetPrice))
            {
                ClosePosition(bar, barIndex, TradeExitReason.TakeProfit);
                return true;
            }
        }

        // Check max holding period
        if (_riskManagement.MaxHoldingPeriod.HasValue)
        {
            var holdingPeriod = barIndex - pos.EntryBar;
            if (holdingPeriod >= _riskManagement.MaxHoldingPeriod.Value)
            {
                ClosePosition(bar, barIndex, TradeExitReason.MaxHoldingPeriod);
                return true;
            }
        }

        // Check max drawdown
        if (_riskManagement.MaxDrawdownPercent.HasValue)
        {
            var currentDrawdown = (_peakEquity - _equity) / _peakEquity * 100;
            if (currentDrawdown >= _riskManagement.MaxDrawdownPercent.Value)
            {
                ClosePosition(bar, barIndex, TradeExitReason.RiskLimit);
                return true;
            }
        }

        return false;
    }

    private double CalculateStopPrice(Position pos)
    {
        var stopValue = _riskManagement.StopLossValue ?? 2.0;

        return _riskManagement.StopLossType switch
        {
            StopLossType.FixedPercent => pos.Direction > 0
                ? pos.EntryPrice * (1 - stopValue / 100)
                : pos.EntryPrice * (1 + stopValue / 100),

            StopLossType.FixedDollar => pos.Direction > 0
                ? pos.EntryPrice - stopValue
                : pos.EntryPrice + stopValue,

            StopLossType.TrailingPercent => pos.Direction > 0
                ? pos.HighestPrice * (1 - stopValue / 100)
                : pos.LowestPrice * (1 + stopValue / 100),

            StopLossType.TrailingDollar => pos.Direction > 0
                ? pos.HighestPrice - stopValue
                : pos.LowestPrice + stopValue,

            _ => pos.Direction > 0 ? 0 : double.MaxValue
        };
    }

    private double CalculateTakeProfitPrice(Position pos)
    {
        var profitValue = _riskManagement.TakeProfitValue ?? 3.0;

        return _riskManagement.TakeProfitType switch
        {
            TakeProfitType.FixedPercent => pos.Direction > 0
                ? pos.EntryPrice * (1 + profitValue / 100)
                : pos.EntryPrice * (1 - profitValue / 100),

            TakeProfitType.FixedDollar => pos.Direction > 0
                ? pos.EntryPrice + profitValue
                : pos.EntryPrice - profitValue,

            TakeProfitType.RiskRewardRatio => CalculateRiskRewardTarget(pos, profitValue),

            _ => pos.Direction > 0 ? double.MaxValue : 0
        };
    }

    private double CalculateRiskRewardTarget(Position pos, double ratio)
    {
        var stopValue = _riskManagement.StopLossValue ?? 2.0;
        var riskAmount = pos.EntryPrice * (stopValue / 100);
        var rewardAmount = riskAmount * ratio;

        return pos.Direction > 0
            ? pos.EntryPrice + rewardAmount
            : pos.EntryPrice - rewardAmount;
    }

    private bool IsDailyLimitReached()
    {
        if (_riskManagement.DailyLossLimitPercent.HasValue)
        {
            var dailyLoss = (_dailyStartEquity - _equity) / _dailyStartEquity * 100;
            if (dailyLoss >= _riskManagement.DailyLossLimitPercent.Value)
            {
                return true;
            }
        }

        if (_riskManagement.DailyProfitTargetPercent.HasValue)
        {
            var dailyProfit = (_equity - _dailyStartEquity) / _dailyStartEquity * 100;
            if (dailyProfit >= _riskManagement.DailyProfitTargetPercent.Value)
            {
                return true;
            }
        }

        return false;
    }

    private double CalculatePositionSize(double price)
    {
        if (price <= 0)
        {
            return 0;
        }

        return _positionSizing.Method switch
        {
            PositionSizingMethod.FixedUnits => _positionSizing.FixedUnits ?? 100,

            PositionSizingMethod.FixedDollar => Math.Floor((_positionSizing.FixedDollarAmount ?? 10000) / price),

            PositionSizingMethod.PercentOfEquity => Math.Floor(_equity * (_positionSizing.EquityPercent ?? 10) / 100 / price),

            PositionSizingMethod.RiskPercent => CalculateRiskBasedSize(price),

            PositionSizingMethod.Kelly => CalculateKellySize(price),

            _ => _positionSizing.FixedUnits ?? 100
        };
    }

    private double CalculateRiskBasedSize(double price)
    {
        var riskPercent = _positionSizing.EquityPercent ?? 2;
        var riskAmount = _equity * (riskPercent / 100);
        var stopPercent = _riskManagement.StopLossValue ?? 2;
        var stopAmount = price * (stopPercent / 100);

        if (stopAmount <= 0)
        {
            return 0;
        }

        return Math.Floor(riskAmount / stopAmount);
    }

    private double CalculateKellySize(double price)
    {
        // Calculate Kelly fraction based on historical win rate and avg win/loss
        if (_trades.Count < 10)
        {
            // Not enough data, use fixed sizing
            return Math.Floor(_equity * 0.02 / price);
        }

        var wins = _trades.Count(t => t.NetPnL > 0);
        var losses = _trades.Count(t => t.NetPnL < 0);
        var winRate = (double)wins / _trades.Count;
        var avgWin = _trades.Where(t => t.NetPnL > 0).Select(t => t.NetPnL).DefaultIfEmpty(0).Average();
        var avgLoss = Math.Abs(_trades.Where(t => t.NetPnL < 0).Select(t => t.NetPnL).DefaultIfEmpty(0).Average());

        if (avgLoss <= 0)
        {
            return 0;
        }

        var kellyFraction = winRate - ((1 - winRate) / (avgWin / avgLoss));
        kellyFraction = Math.Max(0, Math.Min(kellyFraction, 1));
        kellyFraction *= _positionSizing.KellyFraction ?? 0.5; // Apply fractional Kelly

        var investAmount = _equity * kellyFraction;
        return Math.Floor(investAmount / price);
    }

    private double CalculateFees(double tradeValue)
    {
        return (_options.FeeModel ?? FeeModel.None) switch
        {
            FeeModel.Fixed => _options.FixedFee ?? 0,
            FeeModel.Percent => tradeValue * (_options.PercentFee ?? 0) / 100,
            _ => 0
        };
    }

    private double CalculateSlippage(double price, int direction)
    {
        var slippageAmount = (_options.SlippageModel ?? SlippageModel.None) switch
        {
            SlippageModel.FixedTicks => (_options.SlippageTicks ?? 0) * 0.01, // Assume 1 tick = $0.01
            SlippageModel.Percent => price * (_options.SlippagePercent ?? 0) / 100,
            _ => 0
        };

        // Slippage is adverse: buy at higher price, sell at lower price
        return slippageAmount * direction;
    }

    private void UpdateEquityCurve(TickerData bar)
    {
        // Calculate unrealized P&L if in a position
        var unrealizedPnL = 0.0;
        if (_currentPosition is not null)
        {
            unrealizedPnL = (bar.Close - _currentPosition.EntryPrice) * _currentPosition.Quantity * _currentPosition.Direction;
        }

        var totalEquity = _equity + unrealizedPnL;

        // Update peak and drawdown
        if (totalEquity > _peakEquity)
        {
            _peakEquity = totalEquity;
            _drawdownStart = bar.Date;
        }

        var drawdown = _peakEquity - totalEquity;
        var drawdownPercent = _peakEquity > 0 ? drawdown / _peakEquity * 100 : 0;

        if (drawdown > _maxDrawdown)
        {
            _maxDrawdown = drawdown;
            _maxDrawdownPercent = drawdownPercent;
            _maxDrawdownDays = (bar.Date - _drawdownStart).Days;
        }

        _equityCurve.Add(new EquityPoint(bar.Date, totalEquity, drawdown, drawdownPercent));
    }

    private BacktestResults CalculateResults(DateTime startDate, DateTime endDate)
    {
        var initialCapital = _options.InitialCapital ?? 100_000;
        var totalDays = Math.Max(1, (endDate - startDate).TotalDays);
        var years = totalDays / 365.25;

        var winningTrades = _trades.Where(t => t.NetPnL > 0).ToList();
        var losingTrades = _trades.Where(t => t.NetPnL < 0).ToList();

        var grossProfit = winningTrades.Sum(t => t.NetPnL);
        var grossLoss = Math.Abs(losingTrades.Sum(t => t.NetPnL));

        var avgWin = winningTrades.Count > 0 ? winningTrades.Average(t => t.NetPnL) : 0;
        var avgLoss = losingTrades.Count > 0 ? Math.Abs(losingTrades.Average(t => t.NetPnL)) : 0;

        var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? double.PositiveInfinity : 0;
        var expectancy = _trades.Count > 0 ? _trades.Average(t => t.NetPnL) : 0;

        // Calculate Sharpe and Sortino ratios
        var returns = CalculateDailyReturns();
        var sharpeRatio = CalculateSharpeRatio(returns);
        var sortinoRatio = CalculateSortinoRatio(returns);

        // Calculate streaks
        var (longestWinStreak, longestLoseStreak) = CalculateStreaks();

        // Calculate CAGR
        var cagr = years > 0 ? (Math.Pow(_equity / initialCapital, 1 / years) - 1) * 100 : 0;

        // Annualized return
        var annualizedReturn = years > 0 ? ((_equity / initialCapital - 1) / years) * 100 : 0;

        // Calmar ratio
        var calmarRatio = _maxDrawdownPercent > 0 ? annualizedReturn / _maxDrawdownPercent : 0;

        return new BacktestResults
        {
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            FinalCapital = _equity,
            AnnualizedReturnPercent = annualizedReturn,
            TotalTrades = _trades.Count,
            WinningTrades = winningTrades.Count,
            LosingTrades = losingTrades.Count,
            AverageWin = avgWin,
            AverageLoss = avgLoss,
            ProfitFactor = profitFactor,
            Expectancy = expectancy,
            MaxDrawdown = _maxDrawdown,
            MaxDrawdownPercent = _maxDrawdownPercent,
            MaxDrawdownDurationDays = _maxDrawdownDays,
            SharpeRatio = sharpeRatio,
            SortinoRatio = sortinoRatio,
            CalmarRatio = calmarRatio,
            TotalFees = _totalFees,
            TotalSlippage = _totalSlippage,
            LongestWinningStreak = longestWinStreak,
            LongestLosingStreak = longestLoseStreak,
            LargestWin = winningTrades.Count > 0 ? winningTrades.Max(t => t.NetPnL) : 0,
            LargestLoss = losingTrades.Count > 0 ? Math.Abs(losingTrades.Min(t => t.NetPnL)) : 0,
            AverageTradeDurationBars = _trades.Count > 0 ? _trades.Average(t => t.DurationBars) : 0,
            EquityCurve = _equityCurve.ToArray(),
            Trades = _trades.ToArray(),
            MonthlyReturns = _monthlyReturns,
            Cagr = cagr
        };
    }

    private List<double> CalculateDailyReturns()
    {
        var returns = new List<double>();
        if (_equityCurve.Count < 2)
        {
            return returns;
        }

        for (var i = 1; i < _equityCurve.Count; i++)
        {
            var prev = _equityCurve[i - 1].Equity;
            var curr = _equityCurve[i].Equity;
            if (prev > 0)
            {
                returns.Add((curr - prev) / prev);
            }
        }

        return returns;
    }

    private double CalculateSharpeRatio(List<double> returns)
    {
        if (returns.Count < 2)
        {
            return 0;
        }

        var avgReturn = returns.Average();
        var stdDev = CalculateStdDev(returns, avgReturn);
        if (stdDev <= 0)
        {
            return 0;
        }

        // Annualize (assuming daily returns, 252 trading days)
        return avgReturn / stdDev * Math.Sqrt(252);
    }

    private double CalculateSortinoRatio(List<double> returns)
    {
        if (returns.Count < 2)
        {
            return 0;
        }

        var avgReturn = returns.Average();
        var negativeReturns = returns.Where(r => r < 0).ToList();
        if (negativeReturns.Count == 0)
        {
            return avgReturn > 0 ? double.PositiveInfinity : 0;
        }

        var downsideDev = CalculateStdDev(negativeReturns, 0);
        if (downsideDev <= 0)
        {
            return 0;
        }

        // Annualize
        return avgReturn / downsideDev * Math.Sqrt(252);
    }

    private static double CalculateStdDev(List<double> values, double mean)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }

    private (int winStreak, int loseStreak) CalculateStreaks()
    {
        var maxWinStreak = 0;
        var maxLoseStreak = 0;
        var currentWinStreak = 0;
        var currentLoseStreak = 0;

        foreach (var trade in _trades)
        {
            if (trade.NetPnL > 0)
            {
                currentWinStreak++;
                currentLoseStreak = 0;
                maxWinStreak = Math.Max(maxWinStreak, currentWinStreak);
            }
            else if (trade.NetPnL < 0)
            {
                currentLoseStreak++;
                currentWinStreak = 0;
                maxLoseStreak = Math.Max(maxLoseStreak, currentLoseStreak);
            }
        }

        return (maxWinStreak, maxLoseStreak);
    }

    private BacktestResults CreateEmptyResults(DateTime startDate, DateTime endDate)
    {
        var initialCapital = _options.InitialCapital ?? 100_000;
        return new BacktestResults
        {
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = initialCapital,
            FinalCapital = initialCapital,
            AnnualizedReturnPercent = 0,
            TotalTrades = 0,
            WinningTrades = 0,
            LosingTrades = 0,
            AverageWin = 0,
            AverageLoss = 0,
            ProfitFactor = 0,
            Expectancy = 0,
            MaxDrawdown = 0,
            MaxDrawdownPercent = 0,
            MaxDrawdownDurationDays = 0,
            SharpeRatio = 0,
            SortinoRatio = 0,
            CalmarRatio = 0,
            TotalFees = 0,
            TotalSlippage = 0,
            LongestWinningStreak = 0,
            LongestLosingStreak = 0,
            LargestWin = 0,
            LargestLoss = 0,
            AverageTradeDurationBars = 0,
            EquityCurve = Array.Empty<EquityPoint>(),
            Trades = Array.Empty<TradeRecord>(),
            MonthlyReturns = new Dictionary<string, double>(),
            Cagr = 0
        };
    }

    private DateTime GetFirstDate()
    {
        var bars = _data.TickerDataList;
        return bars.Count > 0 ? bars[0].Date : DateTime.MinValue;
    }

    private DateTime GetLastDate()
    {
        var bars = _data.TickerDataList;
        return bars.Count > 0 ? bars[bars.Count - 1].Date : DateTime.MaxValue;
    }

    private static int FindBarIndex(IList<TickerData> bars, DateTime date)
    {
        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i].Date >= date)
            {
                return i;
            }
        }
        return Math.Max(0, bars.Count - 1);
    }

    /// <summary>
    /// Internal position tracking.
    /// </summary>
    private sealed class Position
    {
        public DateTime EntryTime { get; set; }
        public double EntryPrice { get; set; }
        public int EntryBar { get; set; }
        public int Direction { get; set; }
        public double Quantity { get; set; }
        public double EntryFees { get; set; }
        public double EntrySlippage { get; set; }
        public string? EntrySignal { get; set; }
        public double HighestPrice { get; set; }
        public double LowestPrice { get; set; }
    }
}
