namespace OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Walk-Forward Analysis for avoiding overfitting in strategy optimization.
/// Divides data into in-sample (optimization) and out-of-sample (validation) periods.
/// </summary>
public sealed class WalkForwardAnalyzer
{
    /// <summary>Gets or sets the in-sample period size in bars.</summary>
    public int InSamplePeriodBars { get; set; } = 252; // ~1 year

    /// <summary>Gets or sets the out-of-sample period size in bars.</summary>
    public int OutOfSamplePeriodBars { get; set; } = 63; // ~3 months

    /// <summary>Gets or sets the number of walk-forward folds.</summary>
    public int NumFolds { get; set; } = 4;

    /// <summary>Gets or sets whether to use anchored walk-forward (expanding window).</summary>
    public bool UseAnchoredWindow { get; set; } = false;

    /// <summary>
    /// Performs walk-forward analysis on a strategy.
    /// </summary>
    /// <param name="bars">Historical price data.</param>
    /// <param name="strategyFactory">Factory to create strategy instances.</param>
    /// <param name="optimizer">Function to optimize strategy parameters on in-sample data.</param>
    /// <param name="evaluator">Function to evaluate strategy on out-of-sample data.</param>
    /// <returns>Walk-forward analysis result.</returns>
    public WalkForwardResult Analyze<TStrategy, TParams>(
        IReadOnlyList<BarData> bars,
        Func<TParams, TStrategy> strategyFactory,
        Func<IReadOnlyList<BarData>, TParams> optimizer,
        Func<TStrategy, IReadOnlyList<BarData>, FoldResult> evaluator)
    {
        var result = new WalkForwardResult
        {
            TotalBars = bars.Count,
            InSampleBars = InSamplePeriodBars,
            OutOfSampleBars = OutOfSamplePeriodBars,
            NumFolds = NumFolds
        };

        var foldResults = new List<FoldResult>();
        var totalRequiredBars = InSamplePeriodBars + OutOfSamplePeriodBars;

        // Calculate step size between folds
        var availableBars = bars.Count - InSamplePeriodBars;
        var stepSize = Math.Max(1, (availableBars - OutOfSamplePeriodBars) / (NumFolds - 1));

        for (var fold = 0; fold < NumFolds; fold++)
        {
            var oosStartIndex = UseAnchoredWindow
                ? InSamplePeriodBars + fold * stepSize
                : fold * stepSize + InSamplePeriodBars;

            var oosEndIndex = Math.Min(oosStartIndex + OutOfSamplePeriodBars, bars.Count);

            if (oosStartIndex >= bars.Count) break;

            // Get in-sample data
            var isStartIndex = UseAnchoredWindow ? 0 : fold * stepSize;
            var inSampleData = bars.Skip(isStartIndex).Take(oosStartIndex - isStartIndex).ToList();

            // Optimize on in-sample
            var optimizedParams = optimizer(inSampleData);

            // Create strategy with optimized params
            var strategy = strategyFactory(optimizedParams);

            // Get out-of-sample data
            var outOfSampleData = bars.Skip(oosStartIndex).Take(oosEndIndex - oosStartIndex).ToList();

            // Evaluate on out-of-sample
            var foldResult = evaluator(strategy, outOfSampleData);
            foldResult.FoldNumber = fold + 1;
            foldResult.InSampleStartIndex = isStartIndex;
            foldResult.InSampleEndIndex = oosStartIndex - 1;
            foldResult.OutOfSampleStartIndex = oosStartIndex;
            foldResult.OutOfSampleEndIndex = oosEndIndex - 1;

            foldResults.Add(foldResult);
        }

        result.FoldResults = foldResults;
        CalculateAggregateMetrics(result);

        return result;
    }

    /// <summary>
    /// Performs walk-forward with simple return-based evaluation.
    /// </summary>
    public WalkForwardResult AnalyzeReturns(
        IReadOnlyList<BarData> bars,
        Func<IReadOnlyList<BarData>, IReadOnlyList<decimal>> signalGenerator,
        Func<IReadOnlyList<BarData>, object?> optimizer)
    {
        var result = new WalkForwardResult
        {
            TotalBars = bars.Count,
            InSampleBars = InSamplePeriodBars,
            OutOfSampleBars = OutOfSamplePeriodBars,
            NumFolds = NumFolds
        };

        var foldResults = new List<FoldResult>();
        var availableBars = bars.Count - InSamplePeriodBars;
        var stepSize = Math.Max(1, (availableBars - OutOfSamplePeriodBars) / Math.Max(1, NumFolds - 1));

        for (var fold = 0; fold < NumFolds; fold++)
        {
            var oosStartIndex = UseAnchoredWindow
                ? InSamplePeriodBars + fold * stepSize
                : fold * stepSize + InSamplePeriodBars;

            var oosEndIndex = Math.Min(oosStartIndex + OutOfSamplePeriodBars, bars.Count);
            if (oosStartIndex >= bars.Count) break;

            var isStartIndex = UseAnchoredWindow ? 0 : fold * stepSize;
            var inSampleData = bars.Skip(isStartIndex).Take(oosStartIndex - isStartIndex).ToList();

            // Optimize
            optimizer(inSampleData);

            var outOfSampleData = bars.Skip(oosStartIndex).Take(oosEndIndex - oosStartIndex).ToList();

            // Generate signals and calculate returns
            var signals = signalGenerator(outOfSampleData);
            var returns = CalculateStrategyReturns(outOfSampleData, signals);

            var foldResult = new FoldResult
            {
                FoldNumber = fold + 1,
                InSampleStartIndex = isStartIndex,
                InSampleEndIndex = oosStartIndex - 1,
                OutOfSampleStartIndex = oosStartIndex,
                OutOfSampleEndIndex = oosEndIndex - 1,
                TotalReturn = returns.Sum(),
                AverageReturn = returns.Count > 0 ? returns.Average() : 0m,
                WinRate = returns.Count > 0 ? (decimal)returns.Count(r => r > 0) / returns.Count : 0m
            };

            // Calculate Sharpe for the fold
            if (returns.Count > 1)
            {
                var mean = returns.Average();
                var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
                var stdDev = (decimal)Math.Sqrt((double)variance);
                foldResult.SharpeRatio = stdDev > 0 ? mean / stdDev * (decimal)Math.Sqrt(252) : 0m;
            }

            foldResults.Add(foldResult);
        }

        result.FoldResults = foldResults;
        CalculateAggregateMetrics(result);

        return result;
    }

    private static IReadOnlyList<decimal> CalculateStrategyReturns(
        IReadOnlyList<BarData> bars,
        IReadOnlyList<decimal> signals)
    {
        var returns = new List<decimal>();
        for (var i = 1; i < bars.Count && i < signals.Count; i++)
        {
            var dailyReturn = (bars[i].Close - bars[i - 1].Close) / bars[i - 1].Close;
            var position = signals[i - 1]; // Previous day's signal
            returns.Add(dailyReturn * position);
        }
        return returns;
    }

    private static void CalculateAggregateMetrics(WalkForwardResult result)
    {
        if (result.FoldResults.Count == 0) return;

        result.AverageOOSReturn = result.FoldResults.Average(f => f.TotalReturn);
        result.AverageOOSSharpe = result.FoldResults.Average(f => f.SharpeRatio);
        result.AverageWinRate = result.FoldResults.Average(f => f.WinRate);

        // Calculate consistency (% of positive OOS folds)
        result.Consistency = (decimal)result.FoldResults.Count(f => f.TotalReturn > 0) / result.FoldResults.Count;

        // Calculate stability (std dev of returns across folds)
        var mean = result.AverageOOSReturn;
        var variance = result.FoldResults.Sum(f => (f.TotalReturn - mean) * (f.TotalReturn - mean)) / result.FoldResults.Count;
        result.ReturnStability = (decimal)Math.Sqrt((double)variance);
    }
}

/// <summary>
/// Result of walk-forward analysis.
/// </summary>
public sealed class WalkForwardResult
{
    /// <summary>Gets or sets total bars in data.</summary>
    public int TotalBars { get; set; }

    /// <summary>Gets or sets in-sample period size.</summary>
    public int InSampleBars { get; set; }

    /// <summary>Gets or sets out-of-sample period size.</summary>
    public int OutOfSampleBars { get; set; }

    /// <summary>Gets or sets number of folds.</summary>
    public int NumFolds { get; set; }

    /// <summary>Gets or sets fold results.</summary>
    public IReadOnlyList<FoldResult> FoldResults { get; set; } = Array.Empty<FoldResult>();

    /// <summary>Gets or sets average out-of-sample return.</summary>
    public decimal AverageOOSReturn { get; set; }

    /// <summary>Gets or sets average out-of-sample Sharpe ratio.</summary>
    public decimal AverageOOSSharpe { get; set; }

    /// <summary>Gets or sets average win rate.</summary>
    public decimal AverageWinRate { get; set; }

    /// <summary>Gets or sets consistency (% of profitable folds).</summary>
    public decimal Consistency { get; set; }

    /// <summary>Gets or sets return stability (std dev across folds).</summary>
    public decimal ReturnStability { get; set; }

    /// <summary>Gets whether the strategy shows robustness.</summary>
    public bool IsRobust => Consistency >= 0.6m && AverageOOSSharpe > 0.5m;
}

/// <summary>
/// Result of a single walk-forward fold.
/// </summary>
public sealed class FoldResult
{
    /// <summary>Gets or sets the fold number.</summary>
    public int FoldNumber { get; set; }

    /// <summary>Gets or sets in-sample start index.</summary>
    public int InSampleStartIndex { get; set; }

    /// <summary>Gets or sets in-sample end index.</summary>
    public int InSampleEndIndex { get; set; }

    /// <summary>Gets or sets out-of-sample start index.</summary>
    public int OutOfSampleStartIndex { get; set; }

    /// <summary>Gets or sets out-of-sample end index.</summary>
    public int OutOfSampleEndIndex { get; set; }

    /// <summary>Gets or sets the total return.</summary>
    public decimal TotalReturn { get; set; }

    /// <summary>Gets or sets the average return.</summary>
    public decimal AverageReturn { get; set; }

    /// <summary>Gets or sets the Sharpe ratio.</summary>
    public decimal SharpeRatio { get; set; }

    /// <summary>Gets or sets the win rate.</summary>
    public decimal WinRate { get; set; }

    /// <summary>Gets or sets the max drawdown.</summary>
    public decimal MaxDrawdown { get; set; }

    /// <summary>Gets or sets optimized parameters.</summary>
    public Dictionary<string, object> OptimizedParameters { get; set; } = new();
}

/// <summary>
/// Monte Carlo simulation for strategy robustness testing.
/// </summary>
public sealed class MonteCarloSimulator
{
    private readonly Random _random;

    /// <summary>Gets or sets number of simulations.</summary>
    public int NumSimulations { get; set; } = 1000;

    /// <summary>Gets or sets confidence level for intervals.</summary>
    public decimal ConfidenceLevel { get; set; } = 0.95m;

    /// <summary>Creates a new Monte Carlo simulator.</summary>
    public MonteCarloSimulator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Performs Monte Carlo simulation on historical trade returns.
    /// </summary>
    /// <param name="tradeReturns">Historical trade returns.</param>
    /// <returns>Monte Carlo simulation result.</returns>
    public MonteCarloResult Simulate(IReadOnlyList<decimal> tradeReturns)
    {
        if (tradeReturns.Count == 0)
        {
            return new MonteCarloResult();
        }

        var simulatedReturns = new List<decimal>();
        var simulatedDrawdowns = new List<decimal>();
        var simulatedSharpes = new List<decimal>();

        for (var sim = 0; sim < NumSimulations; sim++)
        {
            // Bootstrap resample the trades
            var resampledReturns = new List<decimal>();
            for (var i = 0; i < tradeReturns.Count; i++)
            {
                var randomIndex = _random.Next(tradeReturns.Count);
                resampledReturns.Add(tradeReturns[randomIndex]);
            }

            // Calculate metrics for this simulation
            var totalReturn = resampledReturns.Sum();
            simulatedReturns.Add(totalReturn);

            // Calculate max drawdown
            var equity = 1m;
            var peak = 1m;
            var maxDrawdown = 0m;
            foreach (var ret in resampledReturns)
            {
                equity *= (1 + ret);
                if (equity > peak) peak = equity;
                var drawdown = (peak - equity) / peak;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            simulatedDrawdowns.Add(maxDrawdown);

            // Calculate Sharpe
            if (resampledReturns.Count > 1)
            {
                var mean = resampledReturns.Average();
                var variance = resampledReturns.Sum(r => (r - mean) * (r - mean)) / (resampledReturns.Count - 1);
                var stdDev = (decimal)Math.Sqrt((double)variance);
                var sharpe = stdDev > 0 ? mean / stdDev : 0m;
                simulatedSharpes.Add(sharpe);
            }
        }

        return BuildResult(simulatedReturns, simulatedDrawdowns, simulatedSharpes);
    }

    /// <summary>
    /// Performs Monte Carlo simulation with trade shuffling (preserves trade distribution).
    /// </summary>
    public MonteCarloResult SimulateWithShuffle(IReadOnlyList<decimal> tradeReturns)
    {
        if (tradeReturns.Count == 0)
        {
            return new MonteCarloResult();
        }

        var simulatedReturns = new List<decimal>();
        var simulatedDrawdowns = new List<decimal>();

        for (var sim = 0; sim < NumSimulations; sim++)
        {
            // Shuffle the trade order
            var shuffled = tradeReturns.OrderBy(_ => _random.Next()).ToList();

            var totalReturn = shuffled.Sum();
            simulatedReturns.Add(totalReturn);

            // Calculate drawdown on shuffled sequence
            var equity = 1m;
            var peak = 1m;
            var maxDrawdown = 0m;
            foreach (var ret in shuffled)
            {
                equity *= (1 + ret);
                if (equity > peak) peak = equity;
                var drawdown = (peak - equity) / peak;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            simulatedDrawdowns.Add(maxDrawdown);
        }

        return BuildResult(simulatedReturns, simulatedDrawdowns, new List<decimal>());
    }

    private MonteCarloResult BuildResult(
        List<decimal> returns,
        List<decimal> drawdowns,
        List<decimal> sharpes)
    {
        var sortedReturns = returns.OrderBy(r => r).ToList();
        var sortedDrawdowns = drawdowns.OrderBy(d => d).ToList();

        var lowerIndex = (int)((1 - ConfidenceLevel) / 2 * NumSimulations);
        var upperIndex = (int)((1 + ConfidenceLevel) / 2 * NumSimulations) - 1;

        return new MonteCarloResult
        {
            NumSimulations = NumSimulations,
            ConfidenceLevel = ConfidenceLevel,

            MeanReturn = returns.Average(),
            MedianReturn = sortedReturns[sortedReturns.Count / 2],
            ReturnLowerBound = sortedReturns[lowerIndex],
            ReturnUpperBound = sortedReturns[upperIndex],
            ReturnStdDev = CalculateStdDev(returns),

            MeanMaxDrawdown = drawdowns.Average(),
            MedianMaxDrawdown = sortedDrawdowns[sortedDrawdowns.Count / 2],
            DrawdownLowerBound = sortedDrawdowns[lowerIndex],
            DrawdownUpperBound = sortedDrawdowns[upperIndex],
            DrawdownStdDev = CalculateStdDev(drawdowns),

            MeanSharpe = sharpes.Count > 0 ? sharpes.Average() : 0m,

            ProbabilityOfProfit = (decimal)returns.Count(r => r > 0) / returns.Count,
            ProbabilityOfLoss = (decimal)returns.Count(r => r < 0) / returns.Count,

            WorstCaseReturn = sortedReturns.First(),
            BestCaseReturn = sortedReturns.Last(),

            ReturnDistribution = BuildHistogram(returns, 20),
            DrawdownDistribution = BuildHistogram(drawdowns, 20)
        };
    }

    private static decimal CalculateStdDev(List<decimal> values)
    {
        if (values.Count < 2) return 0m;
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
    }

    private static IReadOnlyList<HistogramBin> BuildHistogram(List<decimal> values, int bins)
    {
        if (values.Count == 0) return Array.Empty<HistogramBin>();

        var min = values.Min();
        var max = values.Max();
        var binWidth = (max - min) / bins;
        if (binWidth == 0) binWidth = 1;

        var histogram = new List<HistogramBin>();
        for (var i = 0; i < bins; i++)
        {
            var binStart = min + i * binWidth;
            var binEnd = binStart + binWidth;
            var count = values.Count(v => v >= binStart && (i == bins - 1 ? v <= binEnd : v < binEnd));

            histogram.Add(new HistogramBin
            {
                BinStart = binStart,
                BinEnd = binEnd,
                Count = count,
                Frequency = (decimal)count / values.Count
            });
        }

        return histogram;
    }
}

/// <summary>
/// Result of Monte Carlo simulation.
/// </summary>
public sealed class MonteCarloResult
{
    /// <summary>Gets or sets number of simulations run.</summary>
    public int NumSimulations { get; set; }

    /// <summary>Gets or sets confidence level.</summary>
    public decimal ConfidenceLevel { get; set; }

    /// <summary>Gets or sets mean simulated return.</summary>
    public decimal MeanReturn { get; set; }

    /// <summary>Gets or sets median simulated return.</summary>
    public decimal MedianReturn { get; set; }

    /// <summary>Gets or sets return confidence interval lower bound.</summary>
    public decimal ReturnLowerBound { get; set; }

    /// <summary>Gets or sets return confidence interval upper bound.</summary>
    public decimal ReturnUpperBound { get; set; }

    /// <summary>Gets or sets return standard deviation.</summary>
    public decimal ReturnStdDev { get; set; }

    /// <summary>Gets or sets mean max drawdown.</summary>
    public decimal MeanMaxDrawdown { get; set; }

    /// <summary>Gets or sets median max drawdown.</summary>
    public decimal MedianMaxDrawdown { get; set; }

    /// <summary>Gets or sets drawdown lower bound.</summary>
    public decimal DrawdownLowerBound { get; set; }

    /// <summary>Gets or sets drawdown upper bound.</summary>
    public decimal DrawdownUpperBound { get; set; }

    /// <summary>Gets or sets drawdown standard deviation.</summary>
    public decimal DrawdownStdDev { get; set; }

    /// <summary>Gets or sets mean Sharpe ratio.</summary>
    public decimal MeanSharpe { get; set; }

    /// <summary>Gets or sets probability of profit.</summary>
    public decimal ProbabilityOfProfit { get; set; }

    /// <summary>Gets or sets probability of loss.</summary>
    public decimal ProbabilityOfLoss { get; set; }

    /// <summary>Gets or sets worst case return.</summary>
    public decimal WorstCaseReturn { get; set; }

    /// <summary>Gets or sets best case return.</summary>
    public decimal BestCaseReturn { get; set; }

    /// <summary>Gets or sets return distribution histogram.</summary>
    public IReadOnlyList<HistogramBin> ReturnDistribution { get; set; } = Array.Empty<HistogramBin>();

    /// <summary>Gets or sets drawdown distribution histogram.</summary>
    public IReadOnlyList<HistogramBin> DrawdownDistribution { get; set; } = Array.Empty<HistogramBin>();
}

/// <summary>
/// A bin in a histogram.
/// </summary>
public sealed class HistogramBin
{
    /// <summary>Gets or sets the bin start value.</summary>
    public decimal BinStart { get; set; }

    /// <summary>Gets or sets the bin end value.</summary>
    public decimal BinEnd { get; set; }

    /// <summary>Gets or sets the count in this bin.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets the frequency (count / total).</summary>
    public decimal Frequency { get; set; }
}

/// <summary>
/// Bar data for backtesting.
/// </summary>
public sealed class BarData
{
    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the open price.</summary>
    public decimal Open { get; set; }

    /// <summary>Gets or sets the high price.</summary>
    public decimal High { get; set; }

    /// <summary>Gets or sets the low price.</summary>
    public decimal Low { get; set; }

    /// <summary>Gets or sets the close price.</summary>
    public decimal Close { get; set; }

    /// <summary>Gets or sets the volume.</summary>
    public long Volume { get; set; }
}

/// <summary>
/// Parameter optimizer for strategy optimization.
/// </summary>
public sealed class ParameterOptimizer
{
    /// <summary>Gets or sets the optimization method.</summary>
    public OptimizationMethod Method { get; set; } = OptimizationMethod.GridSearch;

    /// <summary>Gets or sets the objective function.</summary>
    public OptimizationObjective Objective { get; set; } = OptimizationObjective.SharpeRatio;

    /// <summary>
    /// Performs grid search optimization.
    /// </summary>
    public OptimizationResult GridSearch(
        IReadOnlyList<ParameterRange> parameters,
        Func<Dictionary<string, decimal>, decimal> objectiveFunction)
    {
        var result = new OptimizationResult();
        var bestObjective = decimal.MinValue;
        Dictionary<string, decimal>? bestParams = null;

        // Generate all parameter combinations
        var combinations = GenerateCombinations(parameters);

        foreach (var combo in combinations)
        {
            var objective = objectiveFunction(combo);
            result.AllResults.Add((combo, objective));

            if (objective > bestObjective)
            {
                bestObjective = objective;
                bestParams = combo;
            }
        }

        result.BestParameters = bestParams ?? new Dictionary<string, decimal>();
        result.BestObjectiveValue = bestObjective;

        return result;
    }

    private static IEnumerable<Dictionary<string, decimal>> GenerateCombinations(
        IReadOnlyList<ParameterRange> parameters)
    {
        if (parameters.Count == 0)
        {
            yield return new Dictionary<string, decimal>();
            yield break;
        }

        var first = parameters[0];
        var rest = parameters.Skip(1).ToList();

        var values = Enumerable.Range(0, first.Steps)
            .Select(i => first.Min + (first.Max - first.Min) * i / Math.Max(1, first.Steps - 1));

        foreach (var value in values)
        {
            foreach (var restCombo in GenerateCombinations(rest))
            {
                var combo = new Dictionary<string, decimal>(restCombo)
                {
                    [first.Name] = value
                };
                yield return combo;
            }
        }
    }
}

/// <summary>
/// Optimization methods.
/// </summary>
public enum OptimizationMethod
{
    /// <summary>Grid search - exhaustive.</summary>
    GridSearch,

    /// <summary>Random search.</summary>
    RandomSearch,

    /// <summary>Genetic algorithm.</summary>
    GeneticAlgorithm
}

/// <summary>
/// Optimization objectives.
/// </summary>
public enum OptimizationObjective
{
    /// <summary>Maximize Sharpe ratio.</summary>
    SharpeRatio,

    /// <summary>Maximize total return.</summary>
    TotalReturn,

    /// <summary>Minimize max drawdown.</summary>
    MinDrawdown,

    /// <summary>Maximize Sortino ratio.</summary>
    SortinoRatio,

    /// <summary>Maximize Calmar ratio.</summary>
    CalmarRatio
}

/// <summary>
/// Parameter range for optimization.
/// </summary>
public sealed class ParameterRange
{
    /// <summary>Gets or sets the parameter name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum value.</summary>
    public decimal Min { get; set; }

    /// <summary>Gets or sets the maximum value.</summary>
    public decimal Max { get; set; }

    /// <summary>Gets or sets the number of steps.</summary>
    public int Steps { get; set; } = 10;
}

/// <summary>
/// Result of parameter optimization.
/// </summary>
public sealed class OptimizationResult
{
    /// <summary>Gets or sets the best parameters.</summary>
    public Dictionary<string, decimal> BestParameters { get; set; } = new();

    /// <summary>Gets or sets the best objective value.</summary>
    public decimal BestObjectiveValue { get; set; }

    /// <summary>Gets or sets all results.</summary>
    public List<(Dictionary<string, decimal> Parameters, decimal Objective)> AllResults { get; set; } = new();
}
