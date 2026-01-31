namespace OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection;

/// <summary>
/// Detects anomalies in price movements using statistical methods.
/// Identifies unusual price changes, gaps, and pattern breaks.
/// </summary>
public sealed class PriceAnomalyDetector
{
    /// <summary>Gets or sets the lookback period for baseline calculation.</summary>
    public int LookbackPeriod { get; set; } = 60;

    /// <summary>Gets or sets the Z-score threshold for anomaly detection.</summary>
    public double ZScoreThreshold { get; set; } = 3.0;

    /// <summary>Gets or sets the minimum gap percentage to flag.</summary>
    public double MinGapPercent { get; set; } = 2.0;

    /// <summary>Gets or sets the IQR multiplier for outlier detection.</summary>
    public double IqrMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Detects price anomalies in a time series.
    /// </summary>
    /// <param name="prices">Historical prices (OHLCV).</param>
    /// <returns>List of detected anomalies.</returns>
    public List<PriceAnomaly> DetectAnomalies(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<PriceAnomaly>();

        if (prices.Count < LookbackPeriod + 1)
        {
            return anomalies;
        }

        // Calculate returns
        var returns = new double[prices.Count - 1];
        for (var i = 1; i < prices.Count; i++)
        {
            returns[i - 1] = (double)((prices[i].Close - prices[i - 1].Close) / prices[i - 1].Close);
        }

        // Detect various anomaly types
        anomalies.AddRange(DetectReturnAnomalies(prices, returns));
        anomalies.AddRange(DetectGapAnomalies(prices));
        anomalies.AddRange(DetectIntradayAnomalies(prices));
        anomalies.AddRange(DetectVolatilityAnomalies(prices, returns));

        return anomalies.OrderBy(a => a.Index).ToList();
    }

    /// <summary>
    /// Detects anomalies using isolation forest approach.
    /// </summary>
    public List<PriceAnomaly> DetectWithIsolationForest(
        IReadOnlyList<PriceBar> prices,
        int numTrees = 100,
        int sampleSize = 256)
    {
        var anomalies = new List<PriceAnomaly>();
        if (prices.Count < LookbackPeriod) return anomalies;

        // Feature extraction
        var features = ExtractFeatures(prices);

        // Build isolation forest and score each point
        var scores = ComputeIsolationScores(features, numTrees, sampleSize);

        // Flag anomalies (score > threshold)
        var threshold = 0.6; // Typical threshold for isolation forest
        for (var i = 0; i < scores.Length; i++)
        {
            if (scores[i] > threshold)
            {
                anomalies.Add(new PriceAnomaly
                {
                    Index = i + LookbackPeriod,
                    Timestamp = prices[i + LookbackPeriod].Timestamp,
                    Type = AnomalyType.Statistical,
                    Severity = MapScoreToSeverity(scores[i]),
                    Score = scores[i],
                    Description = $"Isolation forest anomaly score: {scores[i]:F3}",
                    Price = prices[i + LookbackPeriod].Close
                });
            }
        }

        return anomalies;
    }

    /// <summary>
    /// Real-time anomaly detection for streaming data.
    /// </summary>
    public PriceAnomaly? DetectRealTime(
        IReadOnlyList<PriceBar> recentPrices,
        PriceBar newBar)
    {
        if (recentPrices.Count < LookbackPeriod)
        {
            return null;
        }

        // Calculate recent statistics
        var recentReturns = new double[recentPrices.Count - 1];
        for (var i = 1; i < recentPrices.Count; i++)
        {
            recentReturns[i - 1] = (double)((recentPrices[i].Close - recentPrices[i - 1].Close) / recentPrices[i - 1].Close);
        }

        var mean = recentReturns.Average();
        var stdDev = Math.Sqrt(recentReturns.Select(r => Math.Pow(r - mean, 2)).Average());

        // Calculate new return
        var newReturn = (double)((newBar.Close - recentPrices[^1].Close) / recentPrices[^1].Close);
        var zScore = stdDev > 0 ? Math.Abs((newReturn - mean) / stdDev) : 0;

        // Check for gap
        var gapPercent = Math.Abs((double)((newBar.Open - recentPrices[^1].Close) / recentPrices[^1].Close) * 100);

        // Determine if anomaly
        if (zScore > ZScoreThreshold)
        {
            return new PriceAnomaly
            {
                Index = recentPrices.Count,
                Timestamp = newBar.Timestamp,
                Type = AnomalyType.ExtremeReturn,
                Severity = zScore > ZScoreThreshold * 2 ? AnomalySeverity.Critical : AnomalySeverity.High,
                Score = zScore,
                Description = $"Extreme return: {newReturn:P2} (Z-score: {zScore:F2})",
                Price = newBar.Close
            };
        }

        if (gapPercent > MinGapPercent)
        {
            return new PriceAnomaly
            {
                Index = recentPrices.Count,
                Timestamp = newBar.Timestamp,
                Type = AnomalyType.Gap,
                Severity = gapPercent > MinGapPercent * 2 ? AnomalySeverity.High : AnomalySeverity.Medium,
                Score = gapPercent / MinGapPercent,
                Description = $"Price gap: {gapPercent:F2}%",
                Price = newBar.Close
            };
        }

        return null;
    }

    private List<PriceAnomaly> DetectReturnAnomalies(IReadOnlyList<PriceBar> prices, double[] returns)
    {
        var anomalies = new List<PriceAnomaly>();

        for (var i = LookbackPeriod; i < returns.Length; i++)
        {
            var lookbackReturns = returns.Skip(i - LookbackPeriod).Take(LookbackPeriod).ToArray();
            var mean = lookbackReturns.Average();
            var stdDev = Math.Sqrt(lookbackReturns.Select(r => Math.Pow(r - mean, 2)).Average());

            if (stdDev < 1e-10) continue;

            var zScore = Math.Abs((returns[i] - mean) / stdDev);

            if (zScore > ZScoreThreshold)
            {
                anomalies.Add(new PriceAnomaly
                {
                    Index = i + 1,
                    Timestamp = prices[i + 1].Timestamp,
                    Type = AnomalyType.ExtremeReturn,
                    Severity = MapZScoreToSeverity(zScore),
                    Score = zScore,
                    Description = $"Extreme return: {returns[i]:P2} (Z-score: {zScore:F2})",
                    Price = prices[i + 1].Close
                });
            }
        }

        return anomalies;
    }

    private List<PriceAnomaly> DetectGapAnomalies(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<PriceAnomaly>();

        for (var i = 1; i < prices.Count; i++)
        {
            var gapPercent = Math.Abs((double)((prices[i].Open - prices[i - 1].Close) / prices[i - 1].Close) * 100);

            if (gapPercent > MinGapPercent)
            {
                anomalies.Add(new PriceAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = AnomalyType.Gap,
                    Severity = gapPercent > MinGapPercent * 3 ? AnomalySeverity.Critical :
                              gapPercent > MinGapPercent * 2 ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Score = gapPercent / MinGapPercent,
                    Description = $"Price gap: {gapPercent:F2}%",
                    Price = prices[i].Open
                });
            }
        }

        return anomalies;
    }

    private List<PriceAnomaly> DetectIntradayAnomalies(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<PriceAnomaly>();

        for (var i = LookbackPeriod; i < prices.Count; i++)
        {
            // Calculate typical intraday range
            var recentRanges = prices.Skip(i - LookbackPeriod).Take(LookbackPeriod)
                .Select(p => (double)((p.High - p.Low) / p.Close * 100))
                .ToArray();

            var q1 = Percentile(recentRanges, 25);
            var q3 = Percentile(recentRanges, 75);
            var iqr = q3 - q1;
            var upperBound = q3 + IqrMultiplier * iqr;

            var currentRange = (double)((prices[i].High - prices[i].Low) / prices[i].Close * 100);

            if (currentRange > upperBound && currentRange > 2) // At least 2% range
            {
                anomalies.Add(new PriceAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = AnomalyType.IntradayRange,
                    Severity = currentRange > upperBound * 2 ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Score = currentRange / upperBound,
                    Description = $"Unusual intraday range: {currentRange:F2}% (threshold: {upperBound:F2}%)",
                    Price = prices[i].Close
                });
            }
        }

        return anomalies;
    }

    private List<PriceAnomaly> DetectVolatilityAnomalies(IReadOnlyList<PriceBar> prices, double[] returns)
    {
        var anomalies = new List<PriceAnomaly>();
        var shortPeriod = 5;

        for (var i = LookbackPeriod; i < returns.Length - shortPeriod; i++)
        {
            // Calculate long-term volatility
            var longTermReturns = returns.Skip(i - LookbackPeriod).Take(LookbackPeriod).ToArray();
            var longTermVol = Math.Sqrt(longTermReturns.Select(r => r * r).Average()) * Math.Sqrt(252);

            // Calculate short-term volatility
            var shortTermReturns = returns.Skip(i).Take(shortPeriod).ToArray();
            var shortTermVol = Math.Sqrt(shortTermReturns.Select(r => r * r).Average()) * Math.Sqrt(252);

            if (longTermVol < 0.01) continue;

            var volRatio = shortTermVol / longTermVol;

            if (volRatio > 3.0) // Volatility spike
            {
                anomalies.Add(new PriceAnomaly
                {
                    Index = i + 1,
                    Timestamp = prices[i + 1].Timestamp,
                    Type = AnomalyType.VolatilitySpike,
                    Severity = volRatio > 5 ? AnomalySeverity.Critical : AnomalySeverity.High,
                    Score = volRatio,
                    Description = $"Volatility spike: {shortTermVol:P0} vs {longTermVol:P0} ({volRatio:F1}x)",
                    Price = prices[i + 1].Close
                });
            }
        }

        return anomalies;
    }

    private double[][] ExtractFeatures(IReadOnlyList<PriceBar> prices)
    {
        var features = new List<double[]>();

        for (var i = LookbackPeriod; i < prices.Count; i++)
        {
            var windowPrices = prices.Skip(i - LookbackPeriod).Take(LookbackPeriod + 1).ToList();

            // Feature 1: Return
            var returns = windowPrices.Skip(1).Zip(windowPrices, (curr, prev) =>
                (double)((curr.Close - prev.Close) / prev.Close)).ToArray();
            var currentReturn = returns[^1];

            // Feature 2: Volatility ratio
            var recentVol = Math.Sqrt(returns.TakeLast(5).Select(r => r * r).Average());
            var longVol = Math.Sqrt(returns.Select(r => r * r).Average());
            var volRatio = longVol > 0 ? recentVol / longVol : 1;

            // Feature 3: Gap
            var gap = (double)((prices[i].Open - prices[i - 1].Close) / prices[i - 1].Close);

            // Feature 4: Range percentile
            var ranges = windowPrices.Select(p => (double)((p.High - p.Low) / p.Close)).ToArray();
            var currentRange = ranges[^1];
            var rangePercentile = ranges.Count(r => r <= currentRange) / (double)ranges.Length;

            // Feature 5: Distance from moving average
            var ma = windowPrices.Average(p => (double)p.Close);
            var distFromMa = ((double)prices[i].Close - ma) / ma;

            features.Add(new[] { currentReturn, volRatio, gap, rangePercentile, distFromMa });
        }

        return features.ToArray();
    }

    private double[] ComputeIsolationScores(double[][] features, int numTrees, int sampleSize)
    {
        var scores = new double[features.Length];
        var random = new Random(42);
        var avgPathLength = AveragePathLength(sampleSize);

        for (var t = 0; t < numTrees; t++)
        {
            // Sample subset
            var sampleIndices = Enumerable.Range(0, features.Length)
                .OrderBy(_ => random.Next())
                .Take(Math.Min(sampleSize, features.Length))
                .ToList();

            var sample = sampleIndices.Select(i => features[i]).ToArray();
            var tree = BuildIsolationTree(sample, 0, (int)Math.Ceiling(Math.Log2(sampleSize)), random);

            // Score all points
            for (var i = 0; i < features.Length; i++)
            {
                var pathLength = GetPathLength(features[i], tree, 0);
                scores[i] += pathLength;
            }
        }

        // Average and normalize
        for (var i = 0; i < scores.Length; i++)
        {
            scores[i] = Math.Pow(2, -scores[i] / numTrees / avgPathLength);
        }

        return scores;
    }

    private IsolationNode BuildIsolationTree(double[][] data, int depth, int maxDepth, Random random)
    {
        if (depth >= maxDepth || data.Length <= 1)
        {
            return new IsolationNode { Size = data.Length };
        }

        var numFeatures = data[0].Length;
        var splitFeature = random.Next(numFeatures);

        var values = data.Select(d => d[splitFeature]).ToArray();
        var min = values.Min();
        var max = values.Max();

        if (Math.Abs(max - min) < 1e-10)
        {
            return new IsolationNode { Size = data.Length };
        }

        var splitValue = min + random.NextDouble() * (max - min);

        var leftData = data.Where(d => d[splitFeature] < splitValue).ToArray();
        var rightData = data.Where(d => d[splitFeature] >= splitValue).ToArray();

        return new IsolationNode
        {
            SplitFeature = splitFeature,
            SplitValue = splitValue,
            Left = BuildIsolationTree(leftData, depth + 1, maxDepth, random),
            Right = BuildIsolationTree(rightData, depth + 1, maxDepth, random)
        };
    }

    private double GetPathLength(double[] point, IsolationNode node, int depth)
    {
        if (node.Left is null || node.Right is null)
        {
            return depth + AveragePathLength(node.Size);
        }

        if (point[node.SplitFeature] < node.SplitValue)
        {
            return GetPathLength(point, node.Left, depth + 1);
        }
        return GetPathLength(point, node.Right, depth + 1);
    }

    private static double AveragePathLength(int n)
    {
        if (n <= 1) return 0;
        if (n == 2) return 1;
        return 2 * (Math.Log(n - 1) + 0.5772156649) - 2.0 * (n - 1) / n;
    }

    private static double Percentile(double[] data, double percentile)
    {
        var sorted = data.OrderBy(x => x).ToArray();
        var index = (percentile / 100.0) * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper) return sorted[lower];

        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    private static AnomalySeverity MapZScoreToSeverity(double zScore)
    {
        return zScore switch
        {
            > 5 => AnomalySeverity.Critical,
            > 4 => AnomalySeverity.High,
            > 3 => AnomalySeverity.Medium,
            _ => AnomalySeverity.Low
        };
    }

    private static AnomalySeverity MapScoreToSeverity(double score)
    {
        return score switch
        {
            > 0.8 => AnomalySeverity.Critical,
            > 0.7 => AnomalySeverity.High,
            > 0.6 => AnomalySeverity.Medium,
            _ => AnomalySeverity.Low
        };
    }

    private sealed class IsolationNode
    {
        public int SplitFeature { get; set; }
        public double SplitValue { get; set; }
        public IsolationNode? Left { get; set; }
        public IsolationNode? Right { get; set; }
        public int Size { get; set; }
    }
}

/// <summary>
/// Price bar data.
/// </summary>
public sealed class PriceBar
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
/// Detected price anomaly.
/// </summary>
public sealed class PriceAnomaly
{
    /// <summary>Gets or sets the index in the price series.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the anomaly type.</summary>
    public AnomalyType Type { get; set; }

    /// <summary>Gets or sets the severity.</summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>Gets or sets the anomaly score.</summary>
    public double Score { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the price at anomaly.</summary>
    public decimal Price { get; set; }
}

/// <summary>
/// Type of anomaly detected.
/// </summary>
public enum AnomalyType
{
    /// <summary>Extreme return anomaly.</summary>
    ExtremeReturn,

    /// <summary>Price gap.</summary>
    Gap,

    /// <summary>Unusual intraday range.</summary>
    IntradayRange,

    /// <summary>Volatility spike.</summary>
    VolatilitySpike,

    /// <summary>Statistical anomaly (isolation forest, etc.).</summary>
    Statistical,

    /// <summary>Pattern break.</summary>
    PatternBreak
}

/// <summary>
/// Severity of detected anomaly.
/// </summary>
public enum AnomalySeverity
{
    /// <summary>Low severity - informational.</summary>
    Low,

    /// <summary>Medium severity - notable.</summary>
    Medium,

    /// <summary>High severity - significant.</summary>
    High,

    /// <summary>Critical severity - requires immediate attention.</summary>
    Critical
}
