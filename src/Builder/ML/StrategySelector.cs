namespace OoplesFinance.StockIndicators.Builder.ML;

/// <summary>
/// Selects optimal trading strategies based on current market conditions.
/// Uses ensemble methods and regime detection for dynamic strategy allocation.
/// </summary>
public sealed class StrategySelector
{
    private readonly RegimeDetector _regimeDetector;
    private readonly Dictionary<MarketRegime, List<StrategyPerformance>> _regimePerformance;
    private readonly List<StrategyDefinition> _strategies;

    /// <summary>Gets or sets the lookback period for performance evaluation.</summary>
    public int LookbackPeriod { get; set; } = 252;

    /// <summary>Gets or sets the minimum trades required for evaluation.</summary>
    public int MinimumTrades { get; set; } = 20;

    /// <summary>Gets or sets the regime transition buffer (days).</summary>
    public int RegimeTransitionBuffer { get; set; } = 5;

    /// <summary>Gets or sets whether to use ensemble weighting.</summary>
    public bool UseEnsembleWeighting { get; set; } = true;

    /// <summary>Gets or sets the confidence threshold for strategy selection.</summary>
    public double ConfidenceThreshold { get; set; } = 0.6;

    /// <summary>
    /// Initializes a new instance of the StrategySelector.
    /// </summary>
    public StrategySelector()
    {
        _regimeDetector = new RegimeDetector();
        _regimePerformance = new Dictionary<MarketRegime, List<StrategyPerformance>>();
        _strategies = new List<StrategyDefinition>();

        // Initialize performance tracking for each regime
        foreach (MarketRegime regime in Enum.GetValues(typeof(MarketRegime)))
        {
            _regimePerformance[regime] = new List<StrategyPerformance>();
        }
    }

    /// <summary>
    /// Registers a strategy for selection.
    /// </summary>
    public void RegisterStrategy(StrategyDefinition strategy)
    {
        _strategies.Add(strategy);
    }

    /// <summary>
    /// Records strategy performance during a specific regime.
    /// </summary>
    public void RecordPerformance(
        string strategyId,
        MarketRegime regime,
        decimal sharpeRatio,
        decimal maxDrawdown,
        decimal totalReturn,
        int numTrades)
    {
        var performance = new StrategyPerformance
        {
            StrategyId = strategyId,
            Regime = regime,
            SharpeRatio = sharpeRatio,
            MaxDrawdown = maxDrawdown,
            TotalReturn = totalReturn,
            NumTrades = numTrades,
            RecordedAt = DateTime.UtcNow
        };

        _regimePerformance[regime].Add(performance);
    }

    /// <summary>
    /// Selects the best strategy for current market conditions.
    /// </summary>
    /// <param name="prices">Recent price history.</param>
    /// <returns>Strategy selection result.</returns>
    public StrategySelectionResult SelectStrategy(IReadOnlyList<decimal> prices)
    {
        var regimeAnalysis = _regimeDetector.AnalyzeRegime(prices);
        var currentRegime = regimeAnalysis.CurrentRegime;
        var confidence = regimeAnalysis.Confidence;

        // Get strategy scores for current regime
        var strategyScores = CalculateStrategyScores(currentRegime, regimeAnalysis);

        // If confidence is low, blend with neighboring regimes
        if (confidence < ConfidenceThreshold && UseEnsembleWeighting)
        {
            strategyScores = BlendWithNeighboringRegimes(strategyScores, regimeAnalysis);
        }

        // Select best strategy
        var bestStrategy = strategyScores
            .OrderByDescending(kv => kv.Value.Score)
            .FirstOrDefault();

        return new StrategySelectionResult
        {
            SelectedStrategyId = bestStrategy.Key,
            CurrentRegime = currentRegime,
            RegimeConfidence = confidence,
            StrategyScores = strategyScores,
            RecommendedAllocation = CalculateAllocation(strategyScores),
            SwitchRecommended = ShouldSwitchStrategy(bestStrategy.Key, currentRegime)
        };
    }

    /// <summary>
    /// Recommends a portfolio of strategies with allocation weights.
    /// </summary>
    public PortfolioAllocationResult RecommendPortfolio(
        IReadOnlyList<decimal> prices,
        int maxStrategies = 3)
    {
        var selectionResult = SelectStrategy(prices);
        var topStrategies = selectionResult.StrategyScores
            .OrderByDescending(kv => kv.Value.Score)
            .Take(maxStrategies)
            .ToList();

        // Calculate allocation weights based on scores
        var totalScore = topStrategies.Sum(s => Math.Max(0, s.Value.Score));
        var allocations = new Dictionary<string, decimal>();

        foreach (var strategy in topStrategies)
        {
            var weight = totalScore > 0
                ? (decimal)(Math.Max(0, strategy.Value.Score) / totalScore)
                : 1.0m / maxStrategies;

            allocations[strategy.Key] = Math.Round(weight, 4);
        }

        // Normalize to ensure sum = 1
        var sum = allocations.Values.Sum();
        if (sum > 0 && Math.Abs(sum - 1) > 0.0001m)
        {
            var keys = allocations.Keys.ToList();
            foreach (var key in keys)
            {
                allocations[key] = allocations[key] / sum;
            }
        }

        return new PortfolioAllocationResult
        {
            Allocations = allocations,
            CurrentRegime = selectionResult.CurrentRegime,
            RegimeConfidence = selectionResult.RegimeConfidence,
            ExpectedSharpe = CalculateExpectedSharpe(allocations, selectionResult.CurrentRegime),
            RebalanceRecommended = CheckRebalanceNeeded(allocations)
        };
    }

    /// <summary>
    /// Trains the strategy selector on historical data.
    /// </summary>
    public TrainingResult Train(
        IReadOnlyList<decimal> prices,
        IReadOnlyList<StrategyBacktestResult> backtestResults)
    {
        // Detect regime transitions in historical data
        var transitions = _regimeDetector.DetectRegimeTransitions(prices);

        // Create regime periods
        var regimePeriods = CreateRegimePeriods(prices, transitions);

        // Evaluate each strategy in each regime
        foreach (var strategy in _strategies)
        {
            var backtestResult = backtestResults.FirstOrDefault(b => b.StrategyId == strategy.Id);
            if (backtestResult is null) continue;

            foreach (var period in regimePeriods)
            {
                var periodTrades = backtestResult.Trades
                    .Where(t => t.EntryIndex >= period.StartIndex && t.EntryIndex < period.EndIndex)
                    .ToList();

                if (periodTrades.Count < MinimumTrades) continue;

                var periodReturn = periodTrades.Sum(t => t.ProfitLoss);
                var periodDrawdown = CalculateMaxDrawdown(periodTrades);
                var periodSharpe = CalculateSharpeRatio(periodTrades);

                RecordPerformance(
                    strategy.Id,
                    period.Regime,
                    periodSharpe,
                    periodDrawdown,
                    periodReturn,
                    periodTrades.Count);
            }
        }

        return new TrainingResult
        {
            PeriodsAnalyzed = regimePeriods.Count,
            StrategiesEvaluated = _strategies.Count,
            RegimeDistribution = regimePeriods.GroupBy(p => p.Regime)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private Dictionary<string, StrategyScore> CalculateStrategyScores(
        MarketRegime regime,
        RegimeAnalysis analysis)
    {
        var scores = new Dictionary<string, StrategyScore>();

        foreach (var strategy in _strategies)
        {
            var performances = _regimePerformance[regime]
                .Where(p => p.StrategyId == strategy.Id)
                .OrderByDescending(p => p.RecordedAt)
                .Take(10)
                .ToList();

            if (performances.Count == 0)
            {
                scores[strategy.Id] = new StrategyScore
                {
                    Score = 0,
                    Confidence = 0,
                    SampleSize = 0
                };
                continue;
            }

            // Calculate weighted score based on recency
            var totalWeight = 0.0;
            var weightedSharpe = 0.0;
            var weightedReturn = 0.0;
            var weightedDrawdown = 0.0;

            for (var i = 0; i < performances.Count; i++)
            {
                var weight = Math.Exp(-i * 0.2); // Exponential decay
                totalWeight += weight;
                weightedSharpe += weight * (double)performances[i].SharpeRatio;
                weightedReturn += weight * (double)performances[i].TotalReturn;
                weightedDrawdown += weight * (double)performances[i].MaxDrawdown;
            }

            if (totalWeight > 0)
            {
                weightedSharpe /= totalWeight;
                weightedReturn /= totalWeight;
                weightedDrawdown /= totalWeight;
            }

            // Composite score: prioritize Sharpe, penalize drawdown
            var compositeScore = weightedSharpe * 0.5 +
                                 weightedReturn * 0.3 -
                                 weightedDrawdown * 0.2;

            // Confidence based on sample size
            var confidence = Math.Min(1.0, performances.Count / 10.0);

            scores[strategy.Id] = new StrategyScore
            {
                Score = compositeScore,
                Confidence = confidence,
                SampleSize = performances.Count,
                AverageSharpe = weightedSharpe,
                AverageReturn = weightedReturn,
                AverageDrawdown = weightedDrawdown
            };
        }

        return scores;
    }

    private Dictionary<string, StrategyScore> BlendWithNeighboringRegimes(
        Dictionary<string, StrategyScore> currentScores,
        RegimeAnalysis analysis)
    {
        // Get neighboring regime based on regime probabilities
        var regimeProbabilities = analysis.RegimeProbabilities;
        var blendedScores = new Dictionary<string, StrategyScore>();

        foreach (var strategyId in currentScores.Keys)
        {
            var blendedScore = 0.0;
            var blendedConfidence = 0.0;
            var totalProbability = 0.0;

            foreach (var kvp in regimeProbabilities)
            {
                var regime = kvp.Key;
                var probability = kvp.Value;
                if (probability < 0.1) continue;

                var regimeScores = CalculateStrategyScores(regime, analysis);
                if (regimeScores.TryGetValue(strategyId, out var score))
                {
                    blendedScore += probability * score.Score;
                    blendedConfidence += probability * score.Confidence;
                    totalProbability += probability;
                }
            }

            if (totalProbability > 0)
            {
                blendedScores[strategyId] = new StrategyScore
                {
                    Score = blendedScore / totalProbability,
                    Confidence = blendedConfidence / totalProbability,
                    SampleSize = currentScores[strategyId].SampleSize,
                    IsBlended = true
                };
            }
            else
            {
                blendedScores[strategyId] = currentScores[strategyId];
            }
        }

        return blendedScores;
    }

    private Dictionary<string, decimal> CalculateAllocation(Dictionary<string, StrategyScore> scores)
    {
        var allocation = new Dictionary<string, decimal>();
        var totalScore = scores.Values.Sum(s => Math.Max(0, s.Score * s.Confidence));

        foreach (var kvp in scores)
        {
            var strategyId = kvp.Key;
            var score = kvp.Value;
            var weight = totalScore > 0
                ? Math.Max(0, score.Score * score.Confidence) / totalScore
                : 0;

            allocation[strategyId] = (decimal)Math.Round(weight, 4);
        }

        return allocation;
    }

    private bool ShouldSwitchStrategy(string newStrategyId, MarketRegime currentRegime)
    {
        // Implement hysteresis to prevent frequent switching
        // Only switch if new strategy is significantly better
        return true; // Simplified
    }

    private decimal CalculateExpectedSharpe(Dictionary<string, decimal> allocations, MarketRegime regime)
    {
        var expectedSharpe = 0m;

        foreach (var kvp in allocations)
        {
            var strategyId = kvp.Key;
            var weight = kvp.Value;
            var performances = _regimePerformance[regime]
                .Where(p => p.StrategyId == strategyId)
                .ToList();

            if (performances.Count > 0)
            {
                var avgSharpe = performances.Average(p => p.SharpeRatio);
                expectedSharpe += weight * avgSharpe;
            }
        }

        return expectedSharpe;
    }

    private bool CheckRebalanceNeeded(Dictionary<string, decimal> currentAllocations)
    {
        // Check if any allocation has drifted significantly
        return false; // Simplified
    }

    private List<RegimePeriod> CreateRegimePeriods(
        IReadOnlyList<decimal> prices,
        List<RegimeTransition> transitions)
    {
        var periods = new List<RegimePeriod>();

        if (transitions.Count == 0)
        {
            var regime = _regimeDetector.DetectRegime(prices);
            periods.Add(new RegimePeriod
            {
                Regime = regime,
                StartIndex = 0,
                EndIndex = prices.Count
            });
            return periods;
        }

        // First period
        periods.Add(new RegimePeriod
        {
            Regime = transitions[0].FromRegime,
            StartIndex = 0,
            EndIndex = transitions[0].Index
        });

        // Middle periods
        for (var i = 0; i < transitions.Count - 1; i++)
        {
            periods.Add(new RegimePeriod
            {
                Regime = transitions[i].ToRegime,
                StartIndex = transitions[i].Index,
                EndIndex = transitions[i + 1].Index
            });
        }

        // Last period
        periods.Add(new RegimePeriod
        {
            Regime = transitions[^1].ToRegime,
            StartIndex = transitions[^1].Index,
            EndIndex = prices.Count
        });

        return periods;
    }

    private static decimal CalculateSharpeRatio(List<TradeResult> trades)
    {
        if (trades.Count < 2) return 0;

        var returns = trades.Select(t => (double)t.ReturnPercent).ToArray();
        var avgReturn = returns.Average();
        var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());

        return stdDev > 0 ? (decimal)(avgReturn / stdDev * Math.Sqrt(252)) : 0;
    }

    private static decimal CalculateMaxDrawdown(List<TradeResult> trades)
    {
        if (trades.Count == 0) return 0;

        var equity = 10000m;
        var peak = equity;
        var maxDrawdown = 0m;

        foreach (var trade in trades)
        {
            equity += trade.ProfitLoss;
            if (equity > peak) peak = equity;

            var drawdown = (peak - equity) / peak;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }
}

/// <summary>
/// Definition of a trading strategy.
/// </summary>
public sealed class StrategyDefinition
{
    /// <summary>Gets or sets the strategy ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the strategy name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the strategy description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the strategy type.</summary>
    public StrategyType Type { get; set; }

    /// <summary>Gets or sets the parameters.</summary>
    public Dictionary<string, double> Parameters { get; set; } = new();
}

/// <summary>
/// Strategy types.
/// </summary>
public enum StrategyType
{
    /// <summary>Trend following strategy.</summary>
    TrendFollowing,

    /// <summary>Mean reversion strategy.</summary>
    MeanReversion,

    /// <summary>Momentum strategy.</summary>
    Momentum,

    /// <summary>Volatility strategy.</summary>
    Volatility,

    /// <summary>Statistical arbitrage.</summary>
    StatArb,

    /// <summary>Market making.</summary>
    MarketMaking
}

/// <summary>
/// Result of strategy selection.
/// </summary>
public sealed class StrategySelectionResult
{
    /// <summary>Gets or sets the selected strategy ID.</summary>
    public string SelectedStrategyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the current market regime.</summary>
    public MarketRegime CurrentRegime { get; set; }

    /// <summary>Gets or sets the regime confidence.</summary>
    public double RegimeConfidence { get; set; }

    /// <summary>Gets or sets the scores for all strategies.</summary>
    public Dictionary<string, StrategyScore> StrategyScores { get; set; } = new();

    /// <summary>Gets or sets the recommended allocation.</summary>
    public Dictionary<string, decimal> RecommendedAllocation { get; set; } = new();

    /// <summary>Gets or sets whether a switch is recommended.</summary>
    public bool SwitchRecommended { get; set; }
}

/// <summary>
/// Score for a strategy.
/// </summary>
public sealed class StrategyScore
{
    /// <summary>Gets or sets the composite score.</summary>
    public double Score { get; set; }

    /// <summary>Gets or sets the confidence level.</summary>
    public double Confidence { get; set; }

    /// <summary>Gets or sets the sample size.</summary>
    public int SampleSize { get; set; }

    /// <summary>Gets or sets the average Sharpe ratio.</summary>
    public double AverageSharpe { get; set; }

    /// <summary>Gets or sets the average return.</summary>
    public double AverageReturn { get; set; }

    /// <summary>Gets or sets the average drawdown.</summary>
    public double AverageDrawdown { get; set; }

    /// <summary>Gets or sets whether this is a blended score.</summary>
    public bool IsBlended { get; set; }
}

/// <summary>
/// Result of portfolio allocation.
/// </summary>
public sealed class PortfolioAllocationResult
{
    /// <summary>Gets or sets the allocations by strategy.</summary>
    public Dictionary<string, decimal> Allocations { get; set; } = new();

    /// <summary>Gets or sets the current regime.</summary>
    public MarketRegime CurrentRegime { get; set; }

    /// <summary>Gets or sets the regime confidence.</summary>
    public double RegimeConfidence { get; set; }

    /// <summary>Gets or sets the expected Sharpe ratio.</summary>
    public decimal ExpectedSharpe { get; set; }

    /// <summary>Gets or sets whether rebalance is recommended.</summary>
    public bool RebalanceRecommended { get; set; }
}

/// <summary>
/// Performance record for a strategy.
/// </summary>
public sealed class StrategyPerformance
{
    /// <summary>Gets or sets the strategy ID.</summary>
    public string StrategyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the regime.</summary>
    public MarketRegime Regime { get; set; }

    /// <summary>Gets or sets the Sharpe ratio.</summary>
    public decimal SharpeRatio { get; set; }

    /// <summary>Gets or sets the max drawdown.</summary>
    public decimal MaxDrawdown { get; set; }

    /// <summary>Gets or sets the total return.</summary>
    public decimal TotalReturn { get; set; }

    /// <summary>Gets or sets the number of trades.</summary>
    public int NumTrades { get; set; }

    /// <summary>Gets or sets when this was recorded.</summary>
    public DateTime RecordedAt { get; set; }
}

/// <summary>
/// Backtest result for a strategy.
/// </summary>
public sealed class StrategyBacktestResult
{
    /// <summary>Gets or sets the strategy ID.</summary>
    public string StrategyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the trades.</summary>
    public List<TradeResult> Trades { get; set; } = new();
}

/// <summary>
/// Result of a single trade.
/// </summary>
public sealed class TradeResult
{
    /// <summary>Gets or sets the entry index.</summary>
    public int EntryIndex { get; set; }

    /// <summary>Gets or sets the exit index.</summary>
    public int ExitIndex { get; set; }

    /// <summary>Gets or sets the profit/loss.</summary>
    public decimal ProfitLoss { get; set; }

    /// <summary>Gets or sets the return percentage.</summary>
    public decimal ReturnPercent { get; set; }
}

/// <summary>
/// Training result.
/// </summary>
public sealed class TrainingResult
{
    /// <summary>Gets or sets the number of periods analyzed.</summary>
    public int PeriodsAnalyzed { get; set; }

    /// <summary>Gets or sets the number of strategies evaluated.</summary>
    public int StrategiesEvaluated { get; set; }

    /// <summary>Gets or sets the regime distribution.</summary>
    public Dictionary<MarketRegime, int> RegimeDistribution { get; set; } = new();
}

/// <summary>
/// A period of a specific market regime.
/// </summary>
internal sealed class RegimePeriod
{
    public MarketRegime Regime { get; set; }
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
}
