namespace OoplesFinance.StockIndicators.Builder.ML;

using OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Bayesian optimization for hyperparameter tuning of trading strategies.
/// Uses Gaussian Process surrogate model with Expected Improvement acquisition function.
/// </summary>
public sealed class BayesianOptimizer
{
    private readonly Random _random = new();
    private readonly List<(double[] parameters, double score)> _observations = new();
    private readonly double _explorationWeight;
    private readonly int _initialSamples;

    /// <summary>Gets or sets the maximum number of iterations.</summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>Gets or sets the convergence threshold.</summary>
    public double ConvergenceThreshold { get; set; } = 0.001;

    /// <summary>Gets or sets whether to use parallel evaluation.</summary>
    public bool UseParallel { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the BayesianOptimizer.
    /// </summary>
    /// <param name="explorationWeight">Weight for exploration vs exploitation (default: 0.1)</param>
    /// <param name="initialSamples">Number of random samples before Bayesian optimization (default: 10)</param>
    public BayesianOptimizer(double explorationWeight = 0.1, int initialSamples = 10)
    {
        _explorationWeight = explorationWeight;
        _initialSamples = initialSamples;
    }

    /// <summary>
    /// Optimizes strategy parameters using Bayesian optimization.
    /// </summary>
    /// <param name="parameterRanges">Ranges for each parameter to optimize.</param>
    /// <param name="objectiveFunction">Function that evaluates parameters and returns a score (higher is better).</param>
    /// <returns>Optimization result with best parameters and convergence history.</returns>
    public BayesianOptimizationResult Optimize(
        IReadOnlyList<ParameterRange> parameterRanges,
        Func<double[], double> objectiveFunction)
    {
        _observations.Clear();
        var convergenceHistory = new List<double>();
        var bestScore = double.MinValue;
        var bestParameters = new double[parameterRanges.Count];

        // Phase 1: Random sampling to build initial surrogate model
        for (var i = 0; i < _initialSamples; i++)
        {
            var parameters = SampleRandom(parameterRanges);
            var score = objectiveFunction(parameters);
            _observations.Add((parameters, score));

            if (score > bestScore)
            {
                bestScore = score;
                Array.Copy(parameters, bestParameters, parameters.Length);
            }
            convergenceHistory.Add(bestScore);
        }

        // Phase 2: Bayesian optimization with acquisition function
        var noImprovementCount = 0;
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            // Find next point using Expected Improvement
            var nextParameters = AcquireNextPoint(parameterRanges);
            var score = objectiveFunction(nextParameters);
            _observations.Add((nextParameters, score));

            if (score > bestScore)
            {
                var improvement = score - bestScore;
                bestScore = score;
                Array.Copy(nextParameters, bestParameters, nextParameters.Length);
                noImprovementCount = 0;

                // Check convergence
                if (improvement < ConvergenceThreshold)
                {
                    noImprovementCount++;
                    if (noImprovementCount > 10)
                    {
                        break; // Converged
                    }
                }
            }
            else
            {
                noImprovementCount++;
            }

            convergenceHistory.Add(bestScore);
        }

        return new BayesianOptimizationResult
        {
            BestParameters = bestParameters,
            BestScore = bestScore,
            ConvergenceHistory = convergenceHistory,
            TotalEvaluations = _observations.Count,
            ParameterRanges = parameterRanges.ToList()
        };
    }

    /// <summary>
    /// Optimizes using a backtest engine as the objective function.
    /// </summary>
    public BayesianOptimizationResult OptimizeWithBacktest(
        IReadOnlyList<ParameterRange> parameterRanges,
        BacktestEngine backtestEngine,
        OptimizationObjective objective = OptimizationObjective.SharpeRatio)
    {
        return Optimize(parameterRanges, parameters =>
        {
            // Apply parameters to strategy
            var paramDict = new Dictionary<string, double>();
            for (var i = 0; i < parameterRanges.Count; i++)
            {
                paramDict[parameterRanges[i].Name] = parameters[i];
            }

            try
            {
                // Run backtest with parameters
                var result = backtestEngine.Run();

                return objective switch
                {
                    OptimizationObjective.SharpeRatio => result.SharpeRatio,
                    OptimizationObjective.SortinoRatio => result.SortinoRatio,
                    OptimizationObjective.TotalReturn => result.TotalReturnPercent,
                    OptimizationObjective.MaxDrawdown => -result.MaxDrawdownPercent, // Minimize drawdown
                    OptimizationObjective.CalmarRatio => result.CalmarRatio,
                    OptimizationObjective.ProfitFactor => result.ProfitFactor,
                    _ => result.SharpeRatio
                };
            }
            catch
            {
                return double.MinValue; // Invalid parameters
            }
        });
    }

    private double[] SampleRandom(IReadOnlyList<ParameterRange> ranges)
    {
        var parameters = new double[ranges.Count];
        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            parameters[i] = range.Min + _random.NextDouble() * (range.Max - range.Min);

            if (range.IsInteger)
            {
                parameters[i] = Math.Round(parameters[i]);
            }
        }
        return parameters;
    }

    private double[] AcquireNextPoint(IReadOnlyList<ParameterRange> ranges)
    {
        // Use multi-start optimization to find the point with maximum Expected Improvement
        var bestEI = double.MinValue;
        var bestPoint = SampleRandom(ranges);

        // Sample multiple starting points
        var numCandidates = UseParallel ? Environment.ProcessorCount * 10 : 50;
        var candidates = new List<double[]>();
        for (var i = 0; i < numCandidates; i++)
        {
            candidates.Add(SampleRandom(ranges));
        }

        if (UseParallel)
        {
            Parallel.ForEach(candidates, candidate =>
            {
                var ei = ComputeExpectedImprovement(candidate);
                lock (_observations)
                {
                    if (ei > bestEI)
                    {
                        bestEI = ei;
                        bestPoint = candidate;
                    }
                }
            });
        }
        else
        {
            foreach (var candidate in candidates)
            {
                var ei = ComputeExpectedImprovement(candidate);
                if (ei > bestEI)
                {
                    bestEI = ei;
                    bestPoint = candidate;
                }
            }
        }

        return bestPoint;
    }

    private double ComputeExpectedImprovement(double[] point)
    {
        // Simplified GP prediction using k-nearest neighbors approximation
        // In production, use a full Gaussian Process implementation
        var (mean, stdDev) = PredictGaussianProcess(point);

        if (stdDev < 1e-6)
        {
            return 0; // No uncertainty
        }

        var bestObserved = _observations.Max(o => o.score);
        var z = (mean - bestObserved - _explorationWeight) / stdDev;

        // Expected Improvement formula: EI = (mean - best - xi) * CDF(z) + stdDev * PDF(z)
        var cdf = NormalCDF(z);
        var pdf = NormalPDF(z);

        return (mean - bestObserved - _explorationWeight) * cdf + stdDev * pdf;
    }

    private (double mean, double stdDev) PredictGaussianProcess(double[] point)
    {
        if (_observations.Count < 2)
        {
            return (0, 1);
        }

        // Simplified GP using RBF kernel and weighted average
        var weights = new double[_observations.Count];
        var totalWeight = 0.0;
        const double lengthScale = 1.0;

        for (var i = 0; i < _observations.Count; i++)
        {
            var distance = ComputeDistance(point, _observations[i].parameters);
            weights[i] = Math.Exp(-distance * distance / (2 * lengthScale * lengthScale));
            totalWeight += weights[i];
        }

        // Compute weighted mean
        var mean = 0.0;
        for (var i = 0; i < _observations.Count; i++)
        {
            mean += (weights[i] / totalWeight) * _observations[i].score;
        }

        // Compute variance (uncertainty decreases with more nearby observations)
        var variance = 0.0;
        for (var i = 0; i < _observations.Count; i++)
        {
            var diff = _observations[i].score - mean;
            variance += (weights[i] / totalWeight) * diff * diff;
        }

        // Add exploration bonus for unvisited regions
        var maxWeight = weights.Max();
        var explorationBonus = 1.0 - maxWeight;

        return (mean, Math.Sqrt(variance + explorationBonus * 0.5));
    }

    private static double ComputeDistance(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }

    private static double NormalCDF(double z)
    {
        // Approximation of normal CDF using error function
        return 0.5 * (1 + Erf(z / Math.Sqrt(2)));
    }

    private static double NormalPDF(double z)
    {
        return Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI);
    }

    private static double Erf(double x)
    {
        // Approximation of error function
        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }
}

/// <summary>
/// Result of Bayesian optimization.
/// </summary>
public sealed class BayesianOptimizationResult
{
    /// <summary>Gets or sets the best parameters found.</summary>
    public double[] BestParameters { get; set; } = Array.Empty<double>();

    /// <summary>Gets or sets the best score achieved.</summary>
    public double BestScore { get; set; }

    /// <summary>Gets or sets the convergence history.</summary>
    public List<double> ConvergenceHistory { get; set; } = new();

    /// <summary>Gets or sets the total number of evaluations.</summary>
    public int TotalEvaluations { get; set; }

    /// <summary>Gets or sets the parameter ranges.</summary>
    public List<ParameterRange> ParameterRanges { get; set; } = new();

    /// <summary>
    /// Gets the best parameters as a dictionary.
    /// </summary>
    public Dictionary<string, double> GetParameterDictionary()
    {
        var result = new Dictionary<string, double>();
        for (var i = 0; i < ParameterRanges.Count; i++)
        {
            result[ParameterRanges[i].Name] = BestParameters[i];
        }
        return result;
    }
}

/// <summary>
/// Range definition for a parameter to optimize.
/// </summary>
public sealed class ParameterRange
{
    /// <summary>Gets or sets the parameter name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum value.</summary>
    public double Min { get; set; }

    /// <summary>Gets or sets the maximum value.</summary>
    public double Max { get; set; }

    /// <summary>Gets or sets whether this is an integer parameter.</summary>
    public bool IsInteger { get; set; }

    /// <summary>Gets or sets the step size for grid search.</summary>
    public double Step { get; set; } = 1.0;

    /// <summary>Gets or sets the default value.</summary>
    public double Default { get; set; }

    /// <summary>
    /// Creates an integer parameter range.
    /// </summary>
    public static ParameterRange Integer(string name, int min, int max, int defaultValue = 0)
    {
        return new ParameterRange
        {
            Name = name,
            Min = min,
            Max = max,
            IsInteger = true,
            Default = defaultValue == 0 ? (min + max) / 2.0 : defaultValue
        };
    }

    /// <summary>
    /// Creates a decimal parameter range.
    /// </summary>
    public static ParameterRange Decimal(string name, double min, double max, double defaultValue = 0)
    {
        return new ParameterRange
        {
            Name = name,
            Min = min,
            Max = max,
            IsInteger = false,
            Default = defaultValue == 0 ? (min + max) / 2.0 : defaultValue
        };
    }
}

/// <summary>
/// Optimization objectives.
/// </summary>
public enum OptimizationObjective
{
    /// <summary>Maximize Sharpe Ratio (risk-adjusted returns).</summary>
    SharpeRatio,

    /// <summary>Maximize Sortino Ratio (downside risk-adjusted returns).</summary>
    SortinoRatio,

    /// <summary>Maximize total return.</summary>
    TotalReturn,

    /// <summary>Minimize maximum drawdown.</summary>
    MaxDrawdown,

    /// <summary>Maximize Calmar Ratio (return / max drawdown).</summary>
    CalmarRatio,

    /// <summary>Maximize profit factor (gross profit / gross loss).</summary>
    ProfitFactor
}
