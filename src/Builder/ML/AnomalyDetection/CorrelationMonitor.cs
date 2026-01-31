namespace OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection;

/// <summary>
/// Monitors correlations between assets and detects unusual correlation changes.
/// Useful for identifying regime changes, contagion, and hedging breakdowns.
/// </summary>
public sealed class CorrelationMonitor
{
    /// <summary>Gets or sets the lookback period for correlation calculation.</summary>
    public int LookbackPeriod { get; set; } = 60;

    /// <summary>Gets or sets the short-term period for comparison.</summary>
    public int ShortTermPeriod { get; set; } = 10;

    /// <summary>Gets or sets the correlation change threshold.</summary>
    public double CorrelationChangeThreshold { get; set; } = 0.3;

    /// <summary>Gets or sets the critical correlation threshold (near 1 or -1).</summary>
    public double CriticalCorrelationThreshold { get; set; } = 0.9;

    /// <summary>Gets or sets whether to use rolling correlation.</summary>
    public bool UseRollingCorrelation { get; set; } = true;

    /// <summary>
    /// Monitors correlation between two assets.
    /// </summary>
    /// <param name="asset1Returns">Returns for asset 1.</param>
    /// <param name="asset2Returns">Returns for asset 2.</param>
    /// <param name="asset1Name">Name of asset 1.</param>
    /// <param name="asset2Name">Name of asset 2.</param>
    /// <returns>Correlation monitoring result.</returns>
    public CorrelationMonitoringResult Monitor(
        IReadOnlyList<double> asset1Returns,
        IReadOnlyList<double> asset2Returns,
        string asset1Name = "Asset1",
        string asset2Name = "Asset2")
    {
        if (asset1Returns.Count != asset2Returns.Count || asset1Returns.Count < LookbackPeriod + 1)
        {
            return new CorrelationMonitoringResult
            {
                Asset1 = asset1Name,
                Asset2 = asset2Name,
                HasEnoughData = false
            };
        }

        var anomalies = new List<CorrelationAnomaly>();

        // Calculate rolling correlations
        var rollingCorrelations = CalculateRollingCorrelations(asset1Returns, asset2Returns);

        // Current correlation
        var currentCorrelation = rollingCorrelations[^1];

        // Long-term correlation
        var longTermCorrelation = CalculateCorrelation(
            asset1Returns.Skip(asset1Returns.Count - LookbackPeriod).ToArray(),
            asset2Returns.Skip(asset2Returns.Count - LookbackPeriod).ToArray());

        // Short-term correlation
        var shortTermCorrelation = CalculateCorrelation(
            asset1Returns.TakeLast(ShortTermPeriod).ToArray(),
            asset2Returns.TakeLast(ShortTermPeriod).ToArray());

        // Detect anomalies
        anomalies.AddRange(DetectCorrelationBreakdowns(rollingCorrelations, asset1Name, asset2Name));
        anomalies.AddRange(DetectCorrelationSpikes(rollingCorrelations, asset1Name, asset2Name));
        anomalies.AddRange(DetectRegimeChanges(rollingCorrelations, asset1Name, asset2Name));

        // Calculate correlation stability
        var correlationStability = CalculateCorrelationStability(rollingCorrelations);

        return new CorrelationMonitoringResult
        {
            Asset1 = asset1Name,
            Asset2 = asset2Name,
            HasEnoughData = true,
            CurrentCorrelation = currentCorrelation,
            LongTermCorrelation = longTermCorrelation,
            ShortTermCorrelation = shortTermCorrelation,
            CorrelationChange = shortTermCorrelation - longTermCorrelation,
            RollingCorrelations = rollingCorrelations,
            Anomalies = anomalies,
            CorrelationStability = correlationStability,
            IsBreakingDown = Math.Abs(shortTermCorrelation - longTermCorrelation) > CorrelationChangeThreshold,
            IsHighlyCorrelated = Math.Abs(currentCorrelation) > CriticalCorrelationThreshold
        };
    }

    /// <summary>
    /// Monitors correlations across a portfolio of assets.
    /// </summary>
    /// <param name="assetReturns">Dictionary of asset name to returns.</param>
    /// <returns>Portfolio correlation analysis.</returns>
    public PortfolioCorrelationAnalysis MonitorPortfolio(
        Dictionary<string, IReadOnlyList<double>> assetReturns)
    {
        var assetNames = assetReturns.Keys.ToList();
        var pairwiseResults = new List<CorrelationMonitoringResult>();
        var allAnomalies = new List<CorrelationAnomaly>();

        // Calculate pairwise correlations
        for (var i = 0; i < assetNames.Count; i++)
        {
            for (var j = i + 1; j < assetNames.Count; j++)
            {
                var result = Monitor(
                    assetReturns[assetNames[i]],
                    assetReturns[assetNames[j]],
                    assetNames[i],
                    assetNames[j]);

                pairwiseResults.Add(result);
                allAnomalies.AddRange(result.Anomalies);
            }
        }

        // Build correlation matrix
        var correlationMatrix = BuildCorrelationMatrix(assetReturns, assetNames);

        // Calculate portfolio metrics
        var averageCorrelation = pairwiseResults.Average(r => r.CurrentCorrelation);
        var maxCorrelation = pairwiseResults.Max(r => Math.Abs(r.CurrentCorrelation));
        var correlationDispersion = Math.Sqrt(pairwiseResults.Select(r =>
            Math.Pow(r.CurrentCorrelation - averageCorrelation, 2)).Average());

        // Detect systemic risk (all correlations moving toward 1)
        var correlationTowardsOne = pairwiseResults.Count(r =>
            r.CorrelationChange > 0 && r.ShortTermCorrelation > 0.5) / (double)pairwiseResults.Count;

        return new PortfolioCorrelationAnalysis
        {
            AssetNames = assetNames,
            CorrelationMatrix = correlationMatrix,
            PairwiseResults = pairwiseResults,
            AllAnomalies = allAnomalies,
            AverageCorrelation = averageCorrelation,
            MaxAbsoluteCorrelation = maxCorrelation,
            CorrelationDispersion = correlationDispersion,
            SystemicRiskIndicator = correlationTowardsOne,
            HasSystemicRiskWarning = correlationTowardsOne > 0.7,
            HighlyCorrelatedPairs = pairwiseResults
                .Where(r => Math.Abs(r.CurrentCorrelation) > CriticalCorrelationThreshold)
                .Select(r => (r.Asset1, r.Asset2, r.CurrentCorrelation))
                .ToList()
        };
    }

    /// <summary>
    /// Real-time correlation monitoring with new data point.
    /// </summary>
    public CorrelationAnomaly? MonitorRealTime(
        IReadOnlyList<double> asset1RecentReturns,
        IReadOnlyList<double> asset2RecentReturns,
        double asset1NewReturn,
        double asset2NewReturn,
        string asset1Name = "Asset1",
        string asset2Name = "Asset2")
    {
        if (asset1RecentReturns.Count < LookbackPeriod)
        {
            return null;
        }

        // Calculate baseline correlation
        var baselineCorrelation = CalculateCorrelation(
            asset1RecentReturns.TakeLast(LookbackPeriod).ToArray(),
            asset2RecentReturns.TakeLast(LookbackPeriod).ToArray());

        // Calculate correlation with new data
        var extendedAsset1 = asset1RecentReturns.TakeLast(LookbackPeriod - 1).Append(asset1NewReturn).ToArray();
        var extendedAsset2 = asset2RecentReturns.TakeLast(LookbackPeriod - 1).Append(asset2NewReturn).ToArray();
        var newCorrelation = CalculateCorrelation(extendedAsset1, extendedAsset2);

        var correlationChange = newCorrelation - baselineCorrelation;

        // Check for significant change
        if (Math.Abs(correlationChange) > CorrelationChangeThreshold / 2)
        {
            return new CorrelationAnomaly
            {
                Index = asset1RecentReturns.Count,
                Timestamp = DateTime.UtcNow,
                Type = correlationChange > 0
                    ? CorrelationAnomalyType.CorrelationSpike
                    : CorrelationAnomalyType.CorrelationBreakdown,
                Severity = Math.Abs(correlationChange) > CorrelationChangeThreshold
                    ? AnomalySeverity.High
                    : AnomalySeverity.Medium,
                Asset1 = asset1Name,
                Asset2 = asset2Name,
                CorrelationBefore = baselineCorrelation,
                CorrelationAfter = newCorrelation,
                CorrelationChange = correlationChange,
                Description = $"Correlation {(correlationChange > 0 ? "increased" : "decreased")} " +
                              $"from {baselineCorrelation:F3} to {newCorrelation:F3}"
            };
        }

        // Check for extreme correlation
        if (Math.Abs(newCorrelation) > CriticalCorrelationThreshold && Math.Abs(baselineCorrelation) < CriticalCorrelationThreshold)
        {
            return new CorrelationAnomaly
            {
                Index = asset1RecentReturns.Count,
                Timestamp = DateTime.UtcNow,
                Type = CorrelationAnomalyType.ExtremeCorrelation,
                Severity = AnomalySeverity.Critical,
                Asset1 = asset1Name,
                Asset2 = asset2Name,
                CorrelationBefore = baselineCorrelation,
                CorrelationAfter = newCorrelation,
                CorrelationChange = correlationChange,
                Description = $"Correlation reached extreme level: {newCorrelation:F3}"
            };
        }

        return null;
    }

    private double[] CalculateRollingCorrelations(
        IReadOnlyList<double> asset1Returns,
        IReadOnlyList<double> asset2Returns)
    {
        var correlations = new List<double>();

        for (var i = LookbackPeriod; i <= asset1Returns.Count; i++)
        {
            var window1 = asset1Returns.Skip(i - LookbackPeriod).Take(LookbackPeriod).ToArray();
            var window2 = asset2Returns.Skip(i - LookbackPeriod).Take(LookbackPeriod).ToArray();
            correlations.Add(CalculateCorrelation(window1, window2));
        }

        return correlations.ToArray();
    }

    private List<CorrelationAnomaly> DetectCorrelationBreakdowns(
        double[] rollingCorrelations,
        string asset1Name,
        string asset2Name)
    {
        var anomalies = new List<CorrelationAnomaly>();

        for (var i = ShortTermPeriod; i < rollingCorrelations.Length; i++)
        {
            var recentCorrelation = rollingCorrelations.Skip(i - ShortTermPeriod).Take(ShortTermPeriod).Average();
            var previousCorrelation = rollingCorrelations[i - ShortTermPeriod];

            var change = recentCorrelation - previousCorrelation;

            // Breakdown: significant drop in correlation
            if (change < -CorrelationChangeThreshold && previousCorrelation > 0.3)
            {
                anomalies.Add(new CorrelationAnomaly
                {
                    Index = i + LookbackPeriod,
                    Timestamp = DateTime.MinValue, // Would need actual timestamps
                    Type = CorrelationAnomalyType.CorrelationBreakdown,
                    Severity = change < -CorrelationChangeThreshold * 1.5
                        ? AnomalySeverity.Critical
                        : AnomalySeverity.High,
                    Asset1 = asset1Name,
                    Asset2 = asset2Name,
                    CorrelationBefore = previousCorrelation,
                    CorrelationAfter = recentCorrelation,
                    CorrelationChange = change,
                    Description = $"Correlation breakdown: {previousCorrelation:F3} -> {recentCorrelation:F3}"
                });
            }
        }

        return anomalies;
    }

    private List<CorrelationAnomaly> DetectCorrelationSpikes(
        double[] rollingCorrelations,
        string asset1Name,
        string asset2Name)
    {
        var anomalies = new List<CorrelationAnomaly>();

        for (var i = ShortTermPeriod; i < rollingCorrelations.Length; i++)
        {
            var recentCorrelation = rollingCorrelations.Skip(i - ShortTermPeriod).Take(ShortTermPeriod).Average();
            var previousCorrelation = rollingCorrelations[i - ShortTermPeriod];

            var change = recentCorrelation - previousCorrelation;

            // Spike: significant increase toward 1
            if (change > CorrelationChangeThreshold && recentCorrelation > 0.7)
            {
                anomalies.Add(new CorrelationAnomaly
                {
                    Index = i + LookbackPeriod,
                    Timestamp = DateTime.MinValue,
                    Type = CorrelationAnomalyType.CorrelationSpike,
                    Severity = recentCorrelation > CriticalCorrelationThreshold
                        ? AnomalySeverity.Critical
                        : AnomalySeverity.High,
                    Asset1 = asset1Name,
                    Asset2 = asset2Name,
                    CorrelationBefore = previousCorrelation,
                    CorrelationAfter = recentCorrelation,
                    CorrelationChange = change,
                    Description = $"Correlation spike (contagion risk): {previousCorrelation:F3} -> {recentCorrelation:F3}"
                });
            }
        }

        return anomalies;
    }

    private List<CorrelationAnomaly> DetectRegimeChanges(
        double[] rollingCorrelations,
        string asset1Name,
        string asset2Name)
    {
        var anomalies = new List<CorrelationAnomaly>();

        // Detect sign changes (positive to negative correlation or vice versa)
        for (var i = ShortTermPeriod; i < rollingCorrelations.Length; i++)
        {
            var previousCorrelations = rollingCorrelations.Skip(i - ShortTermPeriod).Take(ShortTermPeriod / 2);
            var recentCorrelations = rollingCorrelations.Skip(i - ShortTermPeriod / 2).Take(ShortTermPeriod / 2);

            var previousAvg = previousCorrelations.Average();
            var recentAvg = recentCorrelations.Average();

            // Sign change with significant magnitude
            if (Math.Sign(previousAvg) != Math.Sign(recentAvg) &&
                Math.Abs(previousAvg) > 0.2 &&
                Math.Abs(recentAvg) > 0.2)
            {
                anomalies.Add(new CorrelationAnomaly
                {
                    Index = i + LookbackPeriod,
                    Timestamp = DateTime.MinValue,
                    Type = CorrelationAnomalyType.RegimeChange,
                    Severity = AnomalySeverity.Critical,
                    Asset1 = asset1Name,
                    Asset2 = asset2Name,
                    CorrelationBefore = previousAvg,
                    CorrelationAfter = recentAvg,
                    CorrelationChange = recentAvg - previousAvg,
                    Description = $"Correlation regime change: {(previousAvg > 0 ? "positive" : "negative")} " +
                                  $"to {(recentAvg > 0 ? "positive" : "negative")}"
                });
            }
        }

        return anomalies;
    }

    private double CalculateCorrelationStability(double[] rollingCorrelations)
    {
        if (rollingCorrelations.Length < 2) return 1.0;

        var mean = rollingCorrelations.Average();
        var variance = rollingCorrelations.Select(c => Math.Pow(c - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        // Stability is inverse of volatility, normalized to 0-1
        return Math.Max(0, 1 - stdDev);
    }

    private double[,] BuildCorrelationMatrix(
        Dictionary<string, IReadOnlyList<double>> assetReturns,
        List<string> assetNames)
    {
        var n = assetNames.Count;
        var matrix = new double[n, n];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                {
                    matrix[i, j] = 1.0;
                }
                else if (j > i)
                {
                    var correlation = CalculateCorrelation(
                        assetReturns[assetNames[i]].TakeLast(LookbackPeriod).ToArray(),
                        assetReturns[assetNames[j]].TakeLast(LookbackPeriod).ToArray());
                    matrix[i, j] = correlation;
                    matrix[j, i] = correlation;
                }
            }
        }

        return matrix;
    }

    private static double CalculateCorrelation(double[] x, double[] y)
    {
        if (x.Length != y.Length || x.Length < 2) return 0;

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
}

/// <summary>
/// Result of correlation monitoring between two assets.
/// </summary>
public sealed class CorrelationMonitoringResult
{
    /// <summary>Gets or sets the first asset name.</summary>
    public string Asset1 { get; set; } = string.Empty;

    /// <summary>Gets or sets the second asset name.</summary>
    public string Asset2 { get; set; } = string.Empty;

    /// <summary>Gets or sets whether there is enough data.</summary>
    public bool HasEnoughData { get; set; }

    /// <summary>Gets or sets the current correlation.</summary>
    public double CurrentCorrelation { get; set; }

    /// <summary>Gets or sets the long-term correlation.</summary>
    public double LongTermCorrelation { get; set; }

    /// <summary>Gets or sets the short-term correlation.</summary>
    public double ShortTermCorrelation { get; set; }

    /// <summary>Gets or sets the correlation change.</summary>
    public double CorrelationChange { get; set; }

    /// <summary>Gets or sets the rolling correlations.</summary>
    public double[] RollingCorrelations { get; set; } = Array.Empty<double>();

    /// <summary>Gets or sets detected anomalies.</summary>
    public List<CorrelationAnomaly> Anomalies { get; set; } = new();

    /// <summary>Gets or sets the correlation stability (0-1).</summary>
    public double CorrelationStability { get; set; }

    /// <summary>Gets or sets whether correlation is breaking down.</summary>
    public bool IsBreakingDown { get; set; }

    /// <summary>Gets or sets whether assets are highly correlated.</summary>
    public bool IsHighlyCorrelated { get; set; }
}

/// <summary>
/// Correlation anomaly detected.
/// </summary>
public sealed class CorrelationAnomaly
{
    /// <summary>Gets or sets the index.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the anomaly type.</summary>
    public CorrelationAnomalyType Type { get; set; }

    /// <summary>Gets or sets the severity.</summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>Gets or sets the first asset.</summary>
    public string Asset1 { get; set; } = string.Empty;

    /// <summary>Gets or sets the second asset.</summary>
    public string Asset2 { get; set; } = string.Empty;

    /// <summary>Gets or sets the correlation before.</summary>
    public double CorrelationBefore { get; set; }

    /// <summary>Gets or sets the correlation after.</summary>
    public double CorrelationAfter { get; set; }

    /// <summary>Gets or sets the correlation change.</summary>
    public double CorrelationChange { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Type of correlation anomaly.
/// </summary>
public enum CorrelationAnomalyType
{
    /// <summary>Correlation breakdown (decreasing).</summary>
    CorrelationBreakdown,

    /// <summary>Correlation spike (increasing toward 1).</summary>
    CorrelationSpike,

    /// <summary>Extreme correlation level.</summary>
    ExtremeCorrelation,

    /// <summary>Regime change (sign flip).</summary>
    RegimeChange,

    /// <summary>Unstable correlation.</summary>
    UnstableCorrelation
}

/// <summary>
/// Portfolio-wide correlation analysis.
/// </summary>
public sealed class PortfolioCorrelationAnalysis
{
    /// <summary>Gets or sets the asset names.</summary>
    public List<string> AssetNames { get; set; } = new();

    /// <summary>Gets or sets the correlation matrix.</summary>
    public double[,] CorrelationMatrix { get; set; } = new double[0, 0];

    /// <summary>Gets or sets the pairwise results.</summary>
    public List<CorrelationMonitoringResult> PairwiseResults { get; set; } = new();

    /// <summary>Gets or sets all detected anomalies.</summary>
    public List<CorrelationAnomaly> AllAnomalies { get; set; } = new();

    /// <summary>Gets or sets the average correlation.</summary>
    public double AverageCorrelation { get; set; }

    /// <summary>Gets or sets the maximum absolute correlation.</summary>
    public double MaxAbsoluteCorrelation { get; set; }

    /// <summary>Gets or sets the correlation dispersion.</summary>
    public double CorrelationDispersion { get; set; }

    /// <summary>Gets or sets the systemic risk indicator.</summary>
    public double SystemicRiskIndicator { get; set; }

    /// <summary>Gets or sets whether there is a systemic risk warning.</summary>
    public bool HasSystemicRiskWarning { get; set; }

    /// <summary>Gets or sets highly correlated pairs.</summary>
    public List<(string Asset1, string Asset2, double Correlation)> HighlyCorrelatedPairs { get; set; } = new();
}
