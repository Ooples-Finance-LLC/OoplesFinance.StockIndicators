namespace OoplesFinance.StockIndicators.Builder.ML;

/// <summary>
/// Analyzes feature importance for trading strategy inputs.
/// Identifies which indicators and parameters have the most predictive power.
/// </summary>
public sealed class FeatureImportanceAnalyzer
{
    /// <summary>Gets or sets the number of permutation iterations.</summary>
    public int PermutationIterations { get; set; } = 10;

    /// <summary>Gets or sets whether to use parallel computation.</summary>
    public bool UseParallel { get; set; } = true;

    /// <summary>
    /// Calculates feature importance using permutation importance method.
    /// </summary>
    /// <param name="features">Feature matrix (rows = samples, cols = features).</param>
    /// <param name="targets">Target values.</param>
    /// <param name="featureNames">Names of each feature.</param>
    /// <param name="scoreFunction">Function to evaluate model performance.</param>
    /// <returns>Feature importance analysis result.</returns>
    public FeatureImportanceResult AnalyzePermutationImportance(
        double[,] features,
        double[] targets,
        IReadOnlyList<string> featureNames,
        Func<double[,], double[], double> scoreFunction)
    {
        var numSamples = features.GetLength(0);
        var numFeatures = features.GetLength(1);
        var baselineScore = scoreFunction(features, targets);
        var importances = new Dictionary<string, FeatureImportanceScore>();

        var random = new Random();

        for (var featureIdx = 0; featureIdx < numFeatures; featureIdx++)
        {
            var scores = new double[PermutationIterations];

            for (var iter = 0; iter < PermutationIterations; iter++)
            {
                // Create permuted copy
                var permutedFeatures = (double[,])features.Clone();
                var permutation = Enumerable.Range(0, numSamples).OrderBy(_ => random.Next()).ToArray();

                // Permute only this feature column
                for (var i = 0; i < numSamples; i++)
                {
                    permutedFeatures[i, featureIdx] = features[permutation[i], featureIdx];
                }

                scores[iter] = scoreFunction(permutedFeatures, targets);
            }

            var avgPermutedScore = scores.Average();
            var importance = baselineScore - avgPermutedScore;
            var stdDev = Math.Sqrt(scores.Select(s => Math.Pow(s - avgPermutedScore, 2)).Average());

            importances[featureNames[featureIdx]] = new FeatureImportanceScore
            {
                FeatureName = featureNames[featureIdx],
                Importance = importance,
                StandardDeviation = stdDev,
                BaselineScore = baselineScore,
                PermutedScoreMean = avgPermutedScore,
                Rank = 0 // Set after sorting
            };
        }

        // Assign ranks
        var ranked = importances.Values.OrderByDescending(f => f.Importance).ToList();
        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return new FeatureImportanceResult
        {
            FeatureImportances = importances,
            BaselineScore = baselineScore,
            TopFeatures = ranked.Take(10).Select(f => f.FeatureName).ToList()
        };
    }

    /// <summary>
    /// Calculates feature importance using correlation analysis.
    /// </summary>
    public FeatureImportanceResult AnalyzeCorrelationImportance(
        double[,] features,
        double[] targets,
        IReadOnlyList<string> featureNames)
    {
        var numFeatures = features.GetLength(1);
        var importances = new Dictionary<string, FeatureImportanceScore>();

        for (var featureIdx = 0; featureIdx < numFeatures; featureIdx++)
        {
            var featureValues = new double[features.GetLength(0)];
            for (var i = 0; i < features.GetLength(0); i++)
            {
                featureValues[i] = features[i, featureIdx];
            }

            var correlation = CalculateCorrelation(featureValues, targets);
            var absCorrelation = Math.Abs(correlation);

            importances[featureNames[featureIdx]] = new FeatureImportanceScore
            {
                FeatureName = featureNames[featureIdx],
                Importance = absCorrelation,
                Correlation = correlation,
                Rank = 0
            };
        }

        // Assign ranks
        var ranked = importances.Values.OrderByDescending(f => f.Importance).ToList();
        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return new FeatureImportanceResult
        {
            FeatureImportances = importances,
            TopFeatures = ranked.Take(10).Select(f => f.FeatureName).ToList()
        };
    }

    /// <summary>
    /// Calculates feature importance using mutual information.
    /// </summary>
    public FeatureImportanceResult AnalyzeMutualInformation(
        double[,] features,
        double[] targets,
        IReadOnlyList<string> featureNames,
        int numBins = 10)
    {
        var numFeatures = features.GetLength(1);
        var importances = new Dictionary<string, FeatureImportanceScore>();

        for (var featureIdx = 0; featureIdx < numFeatures; featureIdx++)
        {
            var featureValues = new double[features.GetLength(0)];
            for (var i = 0; i < features.GetLength(0); i++)
            {
                featureValues[i] = features[i, featureIdx];
            }

            var mi = CalculateMutualInformation(featureValues, targets, numBins);

            importances[featureNames[featureIdx]] = new FeatureImportanceScore
            {
                FeatureName = featureNames[featureIdx],
                Importance = mi,
                MutualInformation = mi,
                Rank = 0
            };
        }

        // Assign ranks
        var ranked = importances.Values.OrderByDescending(f => f.Importance).ToList();
        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return new FeatureImportanceResult
        {
            FeatureImportances = importances,
            TopFeatures = ranked.Take(10).Select(f => f.FeatureName).ToList()
        };
    }

    /// <summary>
    /// Performs recursive feature elimination to find optimal feature subset.
    /// </summary>
    public RecursiveFeatureEliminationResult RecursiveFeatureElimination(
        double[,] features,
        double[] targets,
        IReadOnlyList<string> featureNames,
        Func<double[,], double[], double> scoreFunction,
        int minFeatures = 1)
    {
        var numFeatures = features.GetLength(1);
        var remainingFeatures = Enumerable.Range(0, numFeatures).ToList();
        var eliminationOrder = new List<string>();
        var scores = new List<double>();
        var currentFeatures = features;

        while (remainingFeatures.Count > minFeatures)
        {
            // Calculate importance for remaining features
            var currentFeatureNames = remainingFeatures.Select(i => featureNames[i]).ToList();
            var importance = AnalyzePermutationImportance(
                currentFeatures, targets, currentFeatureNames, scoreFunction);

            // Find least important feature
            var leastImportant = importance.FeatureImportances
                .OrderBy(kv => kv.Value.Importance)
                .First();

            // Record elimination
            eliminationOrder.Add(leastImportant.Key);
            scores.Add(importance.BaselineScore);

            // Remove feature
            var featureIdxToRemove = currentFeatureNames.IndexOf(leastImportant.Key);
            remainingFeatures.RemoveAt(featureIdxToRemove);

            // Create reduced feature matrix
            var numSamples = currentFeatures.GetLength(0);
            var newFeatures = new double[numSamples, remainingFeatures.Count];
            for (var i = 0; i < numSamples; i++)
            {
                var newCol = 0;
                for (var j = 0; j < currentFeatures.GetLength(1); j++)
                {
                    if (j != featureIdxToRemove)
                    {
                        newFeatures[i, newCol++] = currentFeatures[i, j];
                    }
                }
            }
            currentFeatures = newFeatures;
        }

        // Find optimal number of features
        var bestScore = scores.Max();
        var optimalIndex = scores.IndexOf(bestScore);
        var optimalFeatureCount = numFeatures - optimalIndex;

        return new RecursiveFeatureEliminationResult
        {
            EliminationOrder = eliminationOrder,
            ScoreHistory = scores,
            OptimalFeatureCount = optimalFeatureCount,
            OptimalFeatures = featureNames.Except(eliminationOrder.Take(optimalIndex)).ToList(),
            BestScore = bestScore
        };
    }

    /// <summary>
    /// Analyzes feature interactions (pairwise).
    /// </summary>
    public FeatureInteractionResult AnalyzeInteractions(
        double[,] features,
        double[] targets,
        IReadOnlyList<string> featureNames)
    {
        var numFeatures = features.GetLength(1);
        var interactions = new Dictionary<(string, string), double>();

        for (var i = 0; i < numFeatures; i++)
        {
            for (var j = i + 1; j < numFeatures; j++)
            {
                // Calculate interaction as correlation between product of features and target
                var featureI = ExtractColumn(features, i);
                var featureJ = ExtractColumn(features, j);
                var interaction = new double[featureI.Length];

                for (var k = 0; k < featureI.Length; k++)
                {
                    interaction[k] = featureI[k] * featureJ[k];
                }

                // Partial correlation controlling for individual features
                var correlationInteraction = CalculateCorrelation(interaction, targets);
                var correlationI = CalculateCorrelation(featureI, targets);
                var correlationJ = CalculateCorrelation(featureJ, targets);

                // Interaction strength is how much the product adds beyond individual correlations
                var interactionStrength = Math.Abs(correlationInteraction) -
                    Math.Max(Math.Abs(correlationI), Math.Abs(correlationJ));

                interactions[(featureNames[i], featureNames[j])] = interactionStrength;
            }
        }

        var topInteractions = interactions
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new FeatureInteraction
            {
                Feature1 = kv.Key.Item1,
                Feature2 = kv.Key.Item2,
                InteractionStrength = kv.Value
            })
            .ToList();

        return new FeatureInteractionResult
        {
            AllInteractions = interactions,
            TopInteractions = topInteractions
        };
    }

    private static double[] ExtractColumn(double[,] matrix, int column)
    {
        var result = new double[matrix.GetLength(0)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = matrix[i, column];
        }
        return result;
    }

    private static double CalculateCorrelation(double[] x, double[] y)
    {
        var n = x.Length;
        var meanX = x.Average();
        var meanY = y.Average();

        var covariance = 0.0;
        var varX = 0.0;
        var varY = 0.0;

        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            covariance += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        var denominator = Math.Sqrt(varX * varY);
        return denominator < 1e-10 ? 0 : covariance / denominator;
    }

    private static double CalculateMutualInformation(double[] x, double[] y, int numBins)
    {
        var n = x.Length;

        // Discretize continuous variables
        var xBins = Discretize(x, numBins);
        var yBins = Discretize(y, numBins);

        // Calculate joint and marginal distributions
        var jointCounts = new int[numBins, numBins];
        var xCounts = new int[numBins];
        var yCounts = new int[numBins];

        for (var i = 0; i < n; i++)
        {
            jointCounts[xBins[i], yBins[i]]++;
            xCounts[xBins[i]]++;
            yCounts[yBins[i]]++;
        }

        // Calculate mutual information
        var mi = 0.0;
        for (var i = 0; i < numBins; i++)
        {
            for (var j = 0; j < numBins; j++)
            {
                if (jointCounts[i, j] > 0 && xCounts[i] > 0 && yCounts[j] > 0)
                {
                    var pxy = (double)jointCounts[i, j] / n;
                    var px = (double)xCounts[i] / n;
                    var py = (double)yCounts[j] / n;
                    mi += pxy * Math.Log(pxy / (px * py));
                }
            }
        }

        return mi;
    }

    private static int[] Discretize(double[] values, int numBins)
    {
        var min = values.Min();
        var max = values.Max();
        var range = max - min;

        if (range < 1e-10)
        {
            return new int[values.Length];
        }

        var binSize = range / numBins;
        var bins = new int[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var bin = (int)((values[i] - min) / binSize);
            bins[i] = Math.Min(bin, numBins - 1);
        }

        return bins;
    }
}

/// <summary>
/// Result of feature importance analysis.
/// </summary>
public sealed class FeatureImportanceResult
{
    /// <summary>Gets or sets the importance scores for each feature.</summary>
    public Dictionary<string, FeatureImportanceScore> FeatureImportances { get; set; } = new();

    /// <summary>Gets or sets the baseline model score.</summary>
    public double BaselineScore { get; set; }

    /// <summary>Gets or sets the top features by importance.</summary>
    public List<string> TopFeatures { get; set; } = new();

    /// <summary>
    /// Gets features ranked by importance.
    /// </summary>
    public IReadOnlyList<FeatureImportanceScore> GetRankedFeatures()
    {
        return FeatureImportances.Values.OrderByDescending(f => f.Importance).ToList();
    }
}

/// <summary>
/// Importance score for a single feature.
/// </summary>
public sealed class FeatureImportanceScore
{
    /// <summary>Gets or sets the feature name.</summary>
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>Gets or sets the importance score.</summary>
    public double Importance { get; set; }

    /// <summary>Gets or sets the standard deviation (for permutation importance).</summary>
    public double StandardDeviation { get; set; }

    /// <summary>Gets or sets the baseline score.</summary>
    public double BaselineScore { get; set; }

    /// <summary>Gets or sets the mean permuted score.</summary>
    public double PermutedScoreMean { get; set; }

    /// <summary>Gets or sets the correlation with target.</summary>
    public double Correlation { get; set; }

    /// <summary>Gets or sets the mutual information.</summary>
    public double MutualInformation { get; set; }

    /// <summary>Gets or sets the feature rank.</summary>
    public int Rank { get; set; }
}

/// <summary>
/// Result of recursive feature elimination.
/// </summary>
public sealed class RecursiveFeatureEliminationResult
{
    /// <summary>Gets or sets the order in which features were eliminated.</summary>
    public List<string> EliminationOrder { get; set; } = new();

    /// <summary>Gets or sets the score history during elimination.</summary>
    public List<double> ScoreHistory { get; set; } = new();

    /// <summary>Gets or sets the optimal number of features.</summary>
    public int OptimalFeatureCount { get; set; }

    /// <summary>Gets or sets the optimal feature set.</summary>
    public List<string> OptimalFeatures { get; set; } = new();

    /// <summary>Gets or sets the best score achieved.</summary>
    public double BestScore { get; set; }
}

/// <summary>
/// Result of feature interaction analysis.
/// </summary>
public sealed class FeatureInteractionResult
{
    /// <summary>Gets or sets all pairwise interactions.</summary>
    public Dictionary<(string, string), double> AllInteractions { get; set; } = new();

    /// <summary>Gets or sets the top feature interactions.</summary>
    public List<FeatureInteraction> TopInteractions { get; set; } = new();
}

/// <summary>
/// Represents an interaction between two features.
/// </summary>
public sealed class FeatureInteraction
{
    /// <summary>Gets or sets the first feature name.</summary>
    public string Feature1 { get; set; } = string.Empty;

    /// <summary>Gets or sets the second feature name.</summary>
    public string Feature2 { get; set; } = string.Empty;

    /// <summary>Gets or sets the interaction strength.</summary>
    public double InteractionStrength { get; set; }
}
