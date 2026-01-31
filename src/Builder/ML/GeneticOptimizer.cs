namespace OoplesFinance.StockIndicators.Builder.ML;

using OoplesFinance.StockIndicators.Builder.Backtest;

/// <summary>
/// Genetic algorithm optimizer for trading strategy parameter optimization.
/// Evolves a population of parameter sets to find optimal values.
/// </summary>
public sealed class GeneticOptimizer
{
    private readonly Random _random = new();

    /// <summary>Gets or sets the population size.</summary>
    public int PopulationSize { get; set; } = 100;

    /// <summary>Gets or sets the number of generations.</summary>
    public int Generations { get; set; } = 50;

    /// <summary>Gets or sets the crossover rate (0-1).</summary>
    public double CrossoverRate { get; set; } = 0.8;

    /// <summary>Gets or sets the mutation rate (0-1).</summary>
    public double MutationRate { get; set; } = 0.1;

    /// <summary>Gets or sets the elite count (top individuals to preserve).</summary>
    public int EliteCount { get; set; } = 5;

    /// <summary>Gets or sets the tournament size for selection.</summary>
    public int TournamentSize { get; set; } = 3;

    /// <summary>Gets or sets whether to use parallel evaluation.</summary>
    public bool UseParallel { get; set; } = true;

    /// <summary>Gets or sets whether to use adaptive mutation rate.</summary>
    public bool AdaptiveMutation { get; set; } = true;

    /// <summary>
    /// Optimizes strategy parameters using genetic algorithm.
    /// </summary>
    /// <param name="parameterRanges">Ranges for each parameter to optimize.</param>
    /// <param name="fitnessFunction">Function that evaluates fitness (higher is better).</param>
    /// <returns>Optimization result with best parameters and evolution history.</returns>
    public GeneticOptimizationResult Optimize(
        IReadOnlyList<ParameterRange> parameterRanges,
        Func<double[], double> fitnessFunction)
    {
        var population = InitializePopulation(parameterRanges);
        var fitnessValues = new double[PopulationSize];
        var bestFitness = double.MinValue;
        var bestIndividual = new double[parameterRanges.Count];
        var generationHistory = new List<GenerationStats>();
        var currentMutationRate = MutationRate;

        for (var generation = 0; generation < Generations; generation++)
        {
            // Evaluate fitness for all individuals
            if (UseParallel)
            {
                Parallel.For(0, PopulationSize, i =>
                {
                    fitnessValues[i] = fitnessFunction(population[i]);
                });
            }
            else
            {
                for (var i = 0; i < PopulationSize; i++)
                {
                    fitnessValues[i] = fitnessFunction(population[i]);
                }
            }

            // Track statistics
            var maxFitness = double.MinValue;
            var minFitness = double.MaxValue;
            var totalFitness = 0.0;
            var bestIndex = 0;

            for (var i = 0; i < PopulationSize; i++)
            {
                totalFitness += fitnessValues[i];
                if (fitnessValues[i] > maxFitness)
                {
                    maxFitness = fitnessValues[i];
                    bestIndex = i;
                }
                if (fitnessValues[i] < minFitness)
                {
                    minFitness = fitnessValues[i];
                }
            }

            // Update best overall
            if (maxFitness > bestFitness)
            {
                bestFitness = maxFitness;
                Array.Copy(population[bestIndex], bestIndividual, parameterRanges.Count);
            }

            // Record generation stats
            generationHistory.Add(new GenerationStats
            {
                Generation = generation,
                BestFitness = maxFitness,
                WorstFitness = minFitness,
                AverageFitness = totalFitness / PopulationSize,
                MutationRate = currentMutationRate
            });

            // Check for convergence
            if (generation > 10)
            {
                var recent = generationHistory.Skip(generation - 10).Take(10).ToList();
                var improvement = recent.Last().BestFitness - recent.First().BestFitness;
                if (Math.Abs(improvement) < 0.0001)
                {
                    break; // Converged
                }
            }

            // Adaptive mutation
            if (AdaptiveMutation && generation > 5)
            {
                var recentImprovement = generationHistory[generation].BestFitness -
                                       generationHistory[generation - 5].BestFitness;
                if (recentImprovement < 0.001)
                {
                    currentMutationRate = Math.Min(0.5, currentMutationRate * 1.2);
                }
                else
                {
                    currentMutationRate = Math.Max(0.01, currentMutationRate * 0.9);
                }
            }

            // Create next generation
            if (generation < Generations - 1)
            {
                population = CreateNextGeneration(population, fitnessValues, parameterRanges, currentMutationRate);
            }
        }

        return new GeneticOptimizationResult
        {
            BestParameters = bestIndividual,
            BestFitness = bestFitness,
            GenerationHistory = generationHistory,
            TotalGenerations = generationHistory.Count,
            ParameterRanges = parameterRanges.ToList()
        };
    }

    /// <summary>
    /// Optimizes using a backtest engine as the fitness function.
    /// </summary>
    public GeneticOptimizationResult OptimizeWithBacktest(
        IReadOnlyList<ParameterRange> parameterRanges,
        BacktestEngine backtestEngine,
        OptimizationObjective objective = OptimizationObjective.SharpeRatio)
    {
        return Optimize(parameterRanges, parameters =>
        {
            try
            {
                var result = backtestEngine.Run();

                return objective switch
                {
                    OptimizationObjective.SharpeRatio => result.SharpeRatio,
                    OptimizationObjective.SortinoRatio => result.SortinoRatio,
                    OptimizationObjective.TotalReturn => result.TotalReturnPercent,
                    OptimizationObjective.MaxDrawdown => -result.MaxDrawdownPercent,
                    OptimizationObjective.CalmarRatio => result.CalmarRatio,
                    OptimizationObjective.ProfitFactor => result.ProfitFactor,
                    _ => result.SharpeRatio
                };
            }
            catch
            {
                return double.MinValue;
            }
        });
    }

    private double[][] InitializePopulation(IReadOnlyList<ParameterRange> ranges)
    {
        var population = new double[PopulationSize][];

        for (var i = 0; i < PopulationSize; i++)
        {
            population[i] = new double[ranges.Count];
            for (var j = 0; j < ranges.Count; j++)
            {
                population[i][j] = RandomInRange(ranges[j]);
            }
        }

        return population;
    }

    private double[][] CreateNextGeneration(
        double[][] currentPopulation,
        double[] fitnessValues,
        IReadOnlyList<ParameterRange> ranges,
        double mutationRate)
    {
        var nextPopulation = new double[PopulationSize][];

        // Sort by fitness to get elite individuals
        var sortedIndices = Enumerable.Range(0, PopulationSize)
            .OrderByDescending(i => fitnessValues[i])
            .ToArray();

        // Elitism: preserve top individuals
        for (var i = 0; i < EliteCount; i++)
        {
            nextPopulation[i] = (double[])currentPopulation[sortedIndices[i]].Clone();
        }

        // Fill rest with crossover and mutation
        for (var i = EliteCount; i < PopulationSize; i++)
        {
            var parent1 = TournamentSelection(currentPopulation, fitnessValues);
            var parent2 = TournamentSelection(currentPopulation, fitnessValues);

            double[] child;
            if (_random.NextDouble() < CrossoverRate)
            {
                child = Crossover(parent1, parent2, ranges);
            }
            else
            {
                child = (double[])(_random.NextDouble() < 0.5 ? parent1 : parent2).Clone();
            }

            // Mutation
            Mutate(child, ranges, mutationRate);

            nextPopulation[i] = child;
        }

        return nextPopulation;
    }

    private double[] TournamentSelection(double[][] population, double[] fitnessValues)
    {
        var bestIndex = -1;
        var bestFitness = double.MinValue;

        for (var i = 0; i < TournamentSize; i++)
        {
            var index = _random.Next(PopulationSize);
            if (fitnessValues[index] > bestFitness)
            {
                bestFitness = fitnessValues[index];
                bestIndex = index;
            }
        }

        return population[bestIndex];
    }

    private double[] Crossover(double[] parent1, double[] parent2, IReadOnlyList<ParameterRange> ranges)
    {
        var child = new double[parent1.Length];

        // BLX-alpha crossover (blend crossover)
        const double alpha = 0.5;

        for (var i = 0; i < parent1.Length; i++)
        {
            var min = Math.Min(parent1[i], parent2[i]);
            var max = Math.Max(parent1[i], parent2[i]);
            var range = max - min;

            var newMin = Math.Max(ranges[i].Min, min - alpha * range);
            var newMax = Math.Min(ranges[i].Max, max + alpha * range);

            child[i] = newMin + _random.NextDouble() * (newMax - newMin);

            if (ranges[i].IsInteger)
            {
                child[i] = Math.Round(child[i]);
            }
        }

        return child;
    }

    private void Mutate(double[] individual, IReadOnlyList<ParameterRange> ranges, double mutationRate)
    {
        for (var i = 0; i < individual.Length; i++)
        {
            if (_random.NextDouble() < mutationRate)
            {
                // Gaussian mutation
                var range = ranges[i].Max - ranges[i].Min;
                var mutation = _random.NextGaussian() * range * 0.1;
                individual[i] = Math.Max(ranges[i].Min, Math.Min(ranges[i].Max, individual[i] + mutation));

                if (ranges[i].IsInteger)
                {
                    individual[i] = Math.Round(individual[i]);
                }
            }
        }
    }

    private double RandomInRange(ParameterRange range)
    {
        var value = range.Min + _random.NextDouble() * (range.Max - range.Min);
        if (range.IsInteger)
        {
            value = Math.Round(value);
        }
        return value;
    }
}

/// <summary>
/// Result of genetic optimization.
/// </summary>
public sealed class GeneticOptimizationResult
{
    /// <summary>Gets or sets the best parameters found.</summary>
    public double[] BestParameters { get; set; } = Array.Empty<double>();

    /// <summary>Gets or sets the best fitness achieved.</summary>
    public double BestFitness { get; set; }

    /// <summary>Gets or sets the generation history.</summary>
    public List<GenerationStats> GenerationHistory { get; set; } = new();

    /// <summary>Gets or sets the total number of generations.</summary>
    public int TotalGenerations { get; set; }

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
/// Statistics for a single generation.
/// </summary>
public sealed class GenerationStats
{
    /// <summary>Gets or sets the generation number.</summary>
    public int Generation { get; set; }

    /// <summary>Gets or sets the best fitness in this generation.</summary>
    public double BestFitness { get; set; }

    /// <summary>Gets or sets the worst fitness in this generation.</summary>
    public double WorstFitness { get; set; }

    /// <summary>Gets or sets the average fitness in this generation.</summary>
    public double AverageFitness { get; set; }

    /// <summary>Gets or sets the mutation rate used in this generation.</summary>
    public double MutationRate { get; set; }
}

/// <summary>
/// Extension methods for random number generation.
/// </summary>
internal static class RandomExtensions
{
    /// <summary>
    /// Generates a random number from standard normal distribution.
    /// </summary>
    public static double NextGaussian(this Random random)
    {
        // Box-Muller transform
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
