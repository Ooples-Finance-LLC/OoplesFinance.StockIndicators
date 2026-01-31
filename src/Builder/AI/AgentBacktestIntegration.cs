using AiDotNet.Tensors.LinearAlgebra;
using OoplesFinance.StockIndicators.Builder.Backtest;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Integrates RL trading agents with the backtesting system.
/// Enables validation, sim-to-real pipeline, and performance comparison.
/// </summary>
/// <remarks>
/// <para>
/// <b>For Beginners:</b> Before letting an AI trade with real money, you want to test it
/// on historical data. This class connects your trained AI agent to the backtesting system.
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var validation = await AgentBacktestIntegration.Create()
///     .WithAgent(trainedAgent)
///     .WithHistoricalData(stockData)
///     .WithInitialCapital(100000)
///     .RunValidationAsync();
///
/// if (validation.MeetsProductionCriteria)
/// {
///     await validation.PromoteToPaperTradingAsync();
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AgentBacktestIntegration
{
    private AgentBacktestIntegration() { }

    /// <summary>
    /// Creates a new agent backtest integration builder.
    /// </summary>
    public static AgentBacktestBuilder Create() => new();
}

/// <summary>
/// Builder for agent backtest validation.
/// </summary>
public sealed class AgentBacktestBuilder
{
    private TradingAgentResult? _tradingAgent;
    private PortfolioAgentResult? _portfolioAgent;
    private StockData? _data;
    private double _initialCapital = 100000;
    private int _windowSize = 20;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private ValidationCriteria _criteria = new();
    private Action<BacktestProgressEvent>? _onProgress;

    internal AgentBacktestBuilder() { }

    /// <summary>
    /// Sets the trading agent to validate.
    /// </summary>
    public AgentBacktestBuilder WithAgent(TradingAgentResult agent)
    {
        _tradingAgent = agent;
        return this;
    }

    /// <summary>
    /// Sets the portfolio agent to validate.
    /// </summary>
    public AgentBacktestBuilder WithPortfolioAgent(PortfolioAgentResult agent)
    {
        _portfolioAgent = agent;
        return this;
    }

    /// <summary>
    /// Sets the historical data for backtesting.
    /// </summary>
    public AgentBacktestBuilder WithHistoricalData(StockData data)
    {
        _data = data;
        return this;
    }

    /// <summary>
    /// Sets the initial capital.
    /// </summary>
    public AgentBacktestBuilder WithInitialCapital(double capital)
    {
        _initialCapital = capital;
        return this;
    }

    /// <summary>
    /// Sets the observation window size.
    /// </summary>
    public AgentBacktestBuilder WithWindowSize(int size)
    {
        _windowSize = size;
        return this;
    }

    /// <summary>
    /// Sets the backtest date range.
    /// </summary>
    public AgentBacktestBuilder WithDateRange(DateTime start, DateTime end)
    {
        _startDate = start;
        _endDate = end;
        return this;
    }

    /// <summary>
    /// Sets validation criteria for promotion.
    /// </summary>
    public AgentBacktestBuilder WithValidationCriteria(ValidationCriteria criteria)
    {
        _criteria = criteria;
        return this;
    }

    /// <summary>
    /// Sets progress callback.
    /// </summary>
    public AgentBacktestBuilder OnProgress(Action<BacktestProgressEvent> callback)
    {
        _onProgress = callback;
        return this;
    }

    /// <summary>
    /// Runs the validation backtest.
    /// </summary>
    public async Task<AgentValidationResult> RunValidationAsync(CancellationToken cancellationToken = default)
    {
        if (_data is null)
        {
            throw new InvalidOperationException("Historical data is required.");
        }

        if (_tradingAgent is null && _portfolioAgent is null)
        {
            throw new InvalidOperationException("An agent is required.");
        }

        var bars = _data.TickerDataList;
        var prices = bars.Select(b => (double)b.Close).ToArray();

        if (prices.Length < _windowSize + 10)
        {
            throw new InvalidOperationException($"Insufficient data. Need at least {_windowSize + 10} bars.");
        }

        // Build observation windows and run agent decisions
        var trades = new List<SimulatedTrade>();
        var equityCurve = new List<EquityPoint>();
        var equity = _initialCapital;
        var position = 0.0;
        var entryPrice = 0.0;
        var peakEquity = _initialCapital;
        var maxDrawdown = 0.0;

        var startIndex = _windowSize;
        var endIndex = prices.Length - 1;

        if (_startDate.HasValue)
        {
            startIndex = Math.Max(startIndex, FindBarIndex(bars, _startDate.Value));
        }
        if (_endDate.HasValue)
        {
            endIndex = Math.Min(endIndex, FindBarIndex(bars, _endDate.Value));
        }

        for (var i = startIndex; i <= endIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build observation state
            var state = BuildObservation(prices, i, position, equity);

            // Get agent action
            Vector<double> action;
            if (_tradingAgent?.Model is not null)
            {
                action = _tradingAgent.Model.Predict(state);
            }
            else if (_portfolioAgent?.Model is not null)
            {
                action = _portfolioAgent.Model.Predict(state);
            }
            else
            {
                continue;
            }

            // Interpret action and execute trade
            var signal = InterpretAction(action);
            var currentPrice = prices[i];
            var bar = bars[i];

            // Execute trading logic
            if (signal > 0.3 && position <= 0) // Buy signal
            {
                if (position < 0) // Close short
                {
                    var pnl = (entryPrice - currentPrice) * Math.Abs(position);
                    equity += pnl;
                    trades.Add(new SimulatedTrade
                    {
                        EntryTime = bars[i - 1].Date,
                        ExitTime = bar.Date,
                        Direction = -1,
                        EntryPrice = entryPrice,
                        ExitPrice = currentPrice,
                        PnL = pnl
                    });
                }
                // Open long
                var shares = CalculatePositionSize(equity, currentPrice);
                position = shares;
                entryPrice = currentPrice;
            }
            else if (signal < -0.3 && position >= 0) // Sell signal
            {
                if (position > 0) // Close long
                {
                    var pnl = (currentPrice - entryPrice) * position;
                    equity += pnl;
                    trades.Add(new SimulatedTrade
                    {
                        EntryTime = bars[i - 1].Date,
                        ExitTime = bar.Date,
                        Direction = 1,
                        EntryPrice = entryPrice,
                        ExitPrice = currentPrice,
                        PnL = pnl
                    });
                }
                // Open short (if allowed)
                position = 0; // For now, just flatten
            }

            // Update unrealized P&L
            var unrealizedPnL = position > 0
                ? (currentPrice - entryPrice) * position
                : position < 0
                    ? (entryPrice - currentPrice) * Math.Abs(position)
                    : 0;

            var currentEquity = equity + unrealizedPnL;
            peakEquity = Math.Max(peakEquity, currentEquity);
            var drawdown = (peakEquity - currentEquity) / peakEquity;
            maxDrawdown = Math.Max(maxDrawdown, drawdown);

            equityCurve.Add(new EquityPoint
            {
                Date = bar.Date,
                Equity = currentEquity,
                Drawdown = drawdown
            });

            // Report progress
            if (_onProgress is not null && i % 100 == 0)
            {
                _onProgress(new BacktestProgressEvent
                {
                    CurrentBar = i,
                    TotalBars = endIndex - startIndex + 1,
                    CurrentEquity = currentEquity,
                    TradeCount = trades.Count
                });
            }
        }

        // Close final position
        if (position != 0)
        {
            var finalPrice = prices[endIndex];
            var pnl = position > 0
                ? (finalPrice - entryPrice) * position
                : (entryPrice - finalPrice) * Math.Abs(position);
            equity += pnl;
            trades.Add(new SimulatedTrade
            {
                EntryTime = bars[endIndex - 1].Date,
                ExitTime = bars[endIndex].Date,
                Direction = position > 0 ? 1 : -1,
                EntryPrice = entryPrice,
                ExitPrice = finalPrice,
                PnL = pnl
            });
        }

        // Calculate metrics
        var metrics = CalculateMetrics(trades, equityCurve, _initialCapital, equity);
        var meetsProduction = EvaluateCriteria(metrics, _criteria);

        return new AgentValidationResult
        {
            Metrics = metrics,
            Trades = trades,
            EquityCurve = equityCurve,
            MeetsProductionCriteria = meetsProduction,
            ValidationCriteria = _criteria,
            StartDate = bars[startIndex].Date,
            EndDate = bars[endIndex].Date,
            InitialCapital = _initialCapital,
            FinalEquity = equity
        };
    }

    private Vector<double> BuildObservation(double[] prices, int currentIndex, double position, double equity)
    {
        // Build observation: [windowed prices (normalized), position, equity ratio]
        var obsSize = _windowSize + 2;
        var obs = new double[obsSize];

        // Normalized price window
        var windowPrices = prices.Skip(currentIndex - _windowSize).Take(_windowSize).ToArray();
        var basePrice = windowPrices[0];
        for (int i = 0; i < _windowSize; i++)
        {
            obs[i] = basePrice > 0 ? (windowPrices[i] / basePrice) - 1.0 : 0;
        }

        // Position indicator (-1 short, 0 flat, 1 long)
        obs[_windowSize] = Math.Sign(position);

        // Equity ratio
        obs[_windowSize + 1] = equity / _initialCapital - 1.0;

        return new Vector<double>(obs);
    }

    private static double InterpretAction(Vector<double> action)
    {
        if (action.Length == 0) return 0;
        return Math.Tanh(action[0]); // Normalize to [-1, 1]
    }

    private double CalculatePositionSize(double equity, double price)
    {
        // Risk 2% of equity per trade
        var riskAmount = equity * 0.02;
        var shares = Math.Floor(riskAmount / (price * 0.05)); // Assuming 5% stop loss
        return Math.Max(1, shares);
    }

    private static int FindBarIndex(IList<TickerData> bars, DateTime date)
    {
        for (int i = 0; i < bars.Count; i++)
        {
            if (bars[i].Date >= date) return i;
        }
        return bars.Count - 1;
    }

    private static AgentPerformanceMetrics CalculateMetrics(
        List<SimulatedTrade> trades,
        List<EquityPoint> equityCurve,
        double initialCapital,
        double finalEquity)
    {
        var winningTrades = trades.Count(t => t.PnL > 0);
        var losingTrades = trades.Count(t => t.PnL < 0);
        var grossProfit = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
        var grossLoss = Math.Abs(trades.Where(t => t.PnL < 0).Sum(t => t.PnL));

        var totalReturn = (finalEquity - initialCapital) / initialCapital;
        var maxDrawdown = equityCurve.Count > 0 ? equityCurve.Max(e => e.Drawdown) : 0;

        // Calculate Sharpe ratio (simplified)
        var returns = new List<double>();
        for (int i = 1; i < equityCurve.Count; i++)
        {
            var dailyReturn = (equityCurve[i].Equity - equityCurve[i - 1].Equity) / equityCurve[i - 1].Equity;
            returns.Add(dailyReturn);
        }

        var avgReturn = returns.Count > 0 ? returns.Average() : 0;
        var stdDev = returns.Count > 1
            ? Math.Sqrt(returns.Sum(r => Math.Pow(r - avgReturn, 2)) / (returns.Count - 1))
            : 0;
        var sharpeRatio = stdDev > 0 ? (avgReturn * Math.Sqrt(252)) / (stdDev * Math.Sqrt(252)) : 0;

        return new AgentPerformanceMetrics
        {
            TotalReturn = totalReturn,
            SharpeRatio = sharpeRatio,
            MaxDrawdown = maxDrawdown,
            WinRate = trades.Count > 0 ? (double)winningTrades / trades.Count : 0,
            ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? double.MaxValue : 0,
            TotalTrades = trades.Count,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            AverageWin = winningTrades > 0 ? grossProfit / winningTrades : 0,
            AverageLoss = losingTrades > 0 ? grossLoss / losingTrades : 0,
            LargestWin = trades.Count > 0 ? trades.Max(t => t.PnL) : 0,
            LargestLoss = trades.Count > 0 ? trades.Min(t => t.PnL) : 0
        };
    }

    private static bool EvaluateCriteria(AgentPerformanceMetrics metrics, ValidationCriteria criteria)
    {
        if (criteria.MinSharpeRatio.HasValue && metrics.SharpeRatio < criteria.MinSharpeRatio.Value)
            return false;

        if (criteria.MaxDrawdown.HasValue && metrics.MaxDrawdown > criteria.MaxDrawdown.Value)
            return false;

        if (criteria.MinWinRate.HasValue && metrics.WinRate < criteria.MinWinRate.Value)
            return false;

        if (criteria.MinProfitFactor.HasValue && metrics.ProfitFactor < criteria.MinProfitFactor.Value)
            return false;

        if (criteria.MinTrades.HasValue && metrics.TotalTrades < criteria.MinTrades.Value)
            return false;

        if (criteria.MinTotalReturn.HasValue && metrics.TotalReturn < criteria.MinTotalReturn.Value)
            return false;

        return true;
    }
}

/// <summary>
/// Sim-to-Real pipeline for graduated deployment of agents.
/// </summary>
public sealed class SimToRealPipeline
{
    private readonly SimToRealOptions _options;
    private DeploymentStage _currentStage = DeploymentStage.Backtest;
    private readonly List<StageTransition> _transitions = new();

    /// <summary>
    /// Creates a new sim-to-real pipeline.
    /// </summary>
    public SimToRealPipeline(SimToRealOptions? options = null)
    {
        _options = options ?? new SimToRealOptions();
    }

    /// <summary>
    /// Gets the current deployment stage.
    /// </summary>
    public DeploymentStage CurrentStage => _currentStage;

    /// <summary>
    /// Gets capital allocation for current stage.
    /// </summary>
    public double CurrentCapitalAllocation => _currentStage switch
    {
        DeploymentStage.Backtest => 0,
        DeploymentStage.PaperTrading => 0,
        DeploymentStage.SmallLive => _options.SmallLivePercent,
        DeploymentStage.MediumLive => _options.MediumLivePercent,
        DeploymentStage.FullLive => 1.0,
        _ => 0
    };

    /// <summary>
    /// Evaluates whether the agent can be promoted to the next stage.
    /// </summary>
    public async Task<PromotionResult> EvaluatePromotionAsync(
        AgentValidationResult validation,
        CancellationToken cancellationToken = default)
    {
        var targetStage = GetNextStage(_currentStage);
        if (targetStage == _currentStage)
        {
            return new PromotionResult
            {
                CanPromote = false,
                Reason = "Already at final stage",
                CurrentStage = _currentStage,
                TargetStage = targetStage
            };
        }

        var criteria = GetCriteriaForStage(targetStage);
        var meetsRequirements = EvaluateStageCriteria(validation.Metrics, criteria);

        if (!meetsRequirements.Success)
        {
            return new PromotionResult
            {
                CanPromote = false,
                Reason = meetsRequirements.FailureReason ?? "Criteria not met",
                CurrentStage = _currentStage,
                TargetStage = targetStage,
                RequiredMetrics = criteria,
                ActualMetrics = validation.Metrics
            };
        }

        return new PromotionResult
        {
            CanPromote = true,
            Reason = "All criteria met",
            CurrentStage = _currentStage,
            TargetStage = targetStage,
            RequiredMetrics = criteria,
            ActualMetrics = validation.Metrics
        };
    }

    /// <summary>
    /// Promotes the agent to the next stage.
    /// </summary>
    public void Promote(PromotionResult result)
    {
        if (!result.CanPromote)
        {
            throw new InvalidOperationException($"Cannot promote: {result.Reason}");
        }

        _transitions.Add(new StageTransition
        {
            FromStage = _currentStage,
            ToStage = result.TargetStage,
            TransitionTime = DateTime.UtcNow,
            Metrics = result.ActualMetrics
        });

        _currentStage = result.TargetStage;
    }

    /// <summary>
    /// Demotes the agent to a previous stage (e.g., after poor live performance).
    /// </summary>
    public void Demote(string reason)
    {
        var previousStage = GetPreviousStage(_currentStage);

        _transitions.Add(new StageTransition
        {
            FromStage = _currentStage,
            ToStage = previousStage,
            TransitionTime = DateTime.UtcNow,
            Reason = reason
        });

        _currentStage = previousStage;
    }

    private static DeploymentStage GetNextStage(DeploymentStage current) => current switch
    {
        DeploymentStage.Backtest => DeploymentStage.PaperTrading,
        DeploymentStage.PaperTrading => DeploymentStage.SmallLive,
        DeploymentStage.SmallLive => DeploymentStage.MediumLive,
        DeploymentStage.MediumLive => DeploymentStage.FullLive,
        _ => current
    };

    private static DeploymentStage GetPreviousStage(DeploymentStage current) => current switch
    {
        DeploymentStage.FullLive => DeploymentStage.MediumLive,
        DeploymentStage.MediumLive => DeploymentStage.SmallLive,
        DeploymentStage.SmallLive => DeploymentStage.PaperTrading,
        DeploymentStage.PaperTrading => DeploymentStage.Backtest,
        _ => current
    };

    private ValidationCriteria GetCriteriaForStage(DeploymentStage stage) => stage switch
    {
        DeploymentStage.PaperTrading => _options.PaperTradingCriteria,
        DeploymentStage.SmallLive => _options.SmallLiveCriteria,
        DeploymentStage.MediumLive => _options.MediumLiveCriteria,
        DeploymentStage.FullLive => _options.FullLiveCriteria,
        _ => new ValidationCriteria()
    };

    private static (bool Success, string? FailureReason) EvaluateStageCriteria(
        AgentPerformanceMetrics metrics,
        ValidationCriteria criteria)
    {
        if (criteria.MinSharpeRatio.HasValue && metrics.SharpeRatio < criteria.MinSharpeRatio.Value)
            return (false, $"Sharpe ratio {metrics.SharpeRatio:F2} below minimum {criteria.MinSharpeRatio.Value:F2}");

        if (criteria.MaxDrawdown.HasValue && metrics.MaxDrawdown > criteria.MaxDrawdown.Value)
            return (false, $"Max drawdown {metrics.MaxDrawdown:P1} exceeds maximum {criteria.MaxDrawdown.Value:P1}");

        if (criteria.MinWinRate.HasValue && metrics.WinRate < criteria.MinWinRate.Value)
            return (false, $"Win rate {metrics.WinRate:P1} below minimum {criteria.MinWinRate.Value:P1}");

        return (true, null);
    }
}

#region Types

/// <summary>
/// Validation criteria for agent promotion.
/// </summary>
public sealed class ValidationCriteria
{
    public double? MinSharpeRatio { get; set; } = 1.0;
    public double? MaxDrawdown { get; set; } = 0.20;
    public double? MinWinRate { get; set; } = 0.45;
    public double? MinProfitFactor { get; set; } = 1.2;
    public int? MinTrades { get; set; } = 30;
    public double? MinTotalReturn { get; set; } = 0.05;
}

/// <summary>
/// Result from agent validation.
/// </summary>
public sealed class AgentValidationResult
{
    public AgentPerformanceMetrics Metrics { get; init; } = new();
    public IReadOnlyList<SimulatedTrade> Trades { get; init; } = Array.Empty<SimulatedTrade>();
    public IReadOnlyList<EquityPoint> EquityCurve { get; init; } = Array.Empty<EquityPoint>();
    public bool MeetsProductionCriteria { get; init; }
    public ValidationCriteria ValidationCriteria { get; init; } = new();
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public double InitialCapital { get; init; }
    public double FinalEquity { get; init; }
}

/// <summary>
/// Performance metrics from agent backtest.
/// </summary>
public sealed class AgentPerformanceMetrics
{
    public double TotalReturn { get; init; }
    public double SharpeRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public double WinRate { get; init; }
    public double ProfitFactor { get; init; }
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public double AverageWin { get; init; }
    public double AverageLoss { get; init; }
    public double LargestWin { get; init; }
    public double LargestLoss { get; init; }
}

/// <summary>
/// Simulated trade from backtest.
/// </summary>
public sealed class SimulatedTrade
{
    public DateTime EntryTime { get; init; }
    public DateTime ExitTime { get; init; }
    public int Direction { get; init; }
    public double EntryPrice { get; init; }
    public double ExitPrice { get; init; }
    public double PnL { get; init; }
}

/// <summary>
/// Equity curve point.
/// </summary>
public sealed class EquityPoint
{
    public DateTime Date { get; init; }
    public double Equity { get; init; }
    public double Drawdown { get; init; }
}

/// <summary>
/// Backtest progress event.
/// </summary>
public sealed class BacktestProgressEvent
{
    public int CurrentBar { get; init; }
    public int TotalBars { get; init; }
    public double CurrentEquity { get; init; }
    public int TradeCount { get; init; }
    public double ProgressPercent => TotalBars > 0 ? (double)CurrentBar / TotalBars : 0;
}

/// <summary>
/// Deployment stages in sim-to-real pipeline.
/// </summary>
public enum DeploymentStage
{
    Backtest,
    PaperTrading,
    SmallLive,
    MediumLive,
    FullLive
}

/// <summary>
/// Options for sim-to-real pipeline.
/// </summary>
public sealed class SimToRealOptions
{
    public double SmallLivePercent { get; set; } = 0.10;
    public double MediumLivePercent { get; set; } = 0.25;

    public ValidationCriteria PaperTradingCriteria { get; set; } = new()
    {
        MinSharpeRatio = 1.0,
        MaxDrawdown = 0.20,
        MinTrades = 30
    };

    public ValidationCriteria SmallLiveCriteria { get; set; } = new()
    {
        MinSharpeRatio = 1.2,
        MaxDrawdown = 0.15,
        MinTrades = 50
    };

    public ValidationCriteria MediumLiveCriteria { get; set; } = new()
    {
        MinSharpeRatio = 1.5,
        MaxDrawdown = 0.12,
        MinTrades = 100
    };

    public ValidationCriteria FullLiveCriteria { get; set; } = new()
    {
        MinSharpeRatio = 1.5,
        MaxDrawdown = 0.10,
        MinTrades = 200
    };
}

/// <summary>
/// Result from promotion evaluation.
/// </summary>
public sealed class PromotionResult
{
    public bool CanPromote { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DeploymentStage CurrentStage { get; init; }
    public DeploymentStage TargetStage { get; init; }
    public ValidationCriteria? RequiredMetrics { get; init; }
    public AgentPerformanceMetrics? ActualMetrics { get; init; }
}

/// <summary>
/// Record of a stage transition.
/// </summary>
public sealed class StageTransition
{
    public DeploymentStage FromStage { get; init; }
    public DeploymentStage ToStage { get; init; }
    public DateTime TransitionTime { get; init; }
    public AgentPerformanceMetrics? Metrics { get; init; }
    public string? Reason { get; init; }
}

#endregion
