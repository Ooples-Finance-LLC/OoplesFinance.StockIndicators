namespace OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection;

/// <summary>
/// Detects anomalies in trading volume patterns.
/// Identifies unusual volume spikes, dry-ups, and pattern breaks.
/// </summary>
public sealed class VolumeAnomalyDetector
{
    /// <summary>Gets or sets the lookback period for baseline calculation.</summary>
    public int LookbackPeriod { get; set; } = 20;

    /// <summary>Gets or sets the volume spike threshold (multiplier of average).</summary>
    public double VolumeSpikeThreshold { get; set; } = 3.0;

    /// <summary>Gets or sets the volume dry-up threshold (fraction of average).</summary>
    public double VolumeDryUpThreshold { get; set; } = 0.3;

    /// <summary>Gets or sets the Z-score threshold for anomaly detection.</summary>
    public double ZScoreThreshold { get; set; } = 2.5;

    /// <summary>Gets or sets the price-volume divergence threshold.</summary>
    public double DivergenceThreshold { get; set; } = 2.0;

    /// <summary>
    /// Detects volume anomalies in a time series.
    /// </summary>
    /// <param name="prices">Historical price bars with volume.</param>
    /// <returns>List of detected volume anomalies.</returns>
    public List<VolumeAnomaly> DetectAnomalies(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<VolumeAnomaly>();

        if (prices.Count < LookbackPeriod + 1)
        {
            return anomalies;
        }

        anomalies.AddRange(DetectVolumeSpikes(prices));
        anomalies.AddRange(DetectVolumeDryUps(prices));
        anomalies.AddRange(DetectPriceVolumeDivergence(prices));
        anomalies.AddRange(DetectVolumePatternBreaks(prices));
        anomalies.AddRange(DetectUnusualVolumeDistribution(prices));

        return anomalies.OrderBy(a => a.Index).ToList();
    }

    /// <summary>
    /// Detects unusual volume relative to daily average volume.
    /// </summary>
    public List<VolumeAnomaly> DetectRelativeVolumeAnomalies(
        IReadOnlyList<PriceBar> prices,
        double averageDailyVolume)
    {
        var anomalies = new List<VolumeAnomaly>();

        for (var index = 0; index < prices.Count; index++)
        {
            var bar = prices[index];
            var relativeVolume = bar.Volume / averageDailyVolume;

            if (relativeVolume > VolumeSpikeThreshold * 2)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = index,
                    Timestamp = bar.Timestamp,
                    Type = VolumeAnomalyType.ExtremeSpike,
                    Severity = AnomalySeverity.Critical,
                    Volume = bar.Volume,
                    AverageVolume = (long)averageDailyVolume,
                    VolumeRatio = relativeVolume,
                    Description = $"Extreme volume: {relativeVolume:F1}x ADV ({bar.Volume:N0} vs {averageDailyVolume:N0})"
                });
            }
            else if (relativeVolume > VolumeSpikeThreshold)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = index,
                    Timestamp = bar.Timestamp,
                    Type = VolumeAnomalyType.Spike,
                    Severity = AnomalySeverity.High,
                    Volume = bar.Volume,
                    AverageVolume = (long)averageDailyVolume,
                    VolumeRatio = relativeVolume,
                    Description = $"Volume spike: {relativeVolume:F1}x ADV"
                });
            }
        }

        return anomalies;
    }

    /// <summary>
    /// Real-time volume anomaly detection for streaming data.
    /// </summary>
    public VolumeAnomaly? DetectRealTime(
        IReadOnlyList<PriceBar> recentPrices,
        PriceBar newBar)
    {
        if (recentPrices.Count < LookbackPeriod)
        {
            return null;
        }

        // Calculate recent volume statistics
        var recentVolumes = recentPrices.TakeLast(LookbackPeriod).Select(p => (double)p.Volume).ToArray();
        var avgVolume = recentVolumes.Average();
        var stdDev = Math.Sqrt(recentVolumes.Select(v => Math.Pow(v - avgVolume, 2)).Average());

        var volumeRatio = newBar.Volume / avgVolume;
        var zScore = stdDev > 0 ? (newBar.Volume - avgVolume) / stdDev : 0;

        // Check for spike
        if (volumeRatio > VolumeSpikeThreshold)
        {
            return new VolumeAnomaly
            {
                Index = recentPrices.Count,
                Timestamp = newBar.Timestamp,
                Type = volumeRatio > VolumeSpikeThreshold * 2 ? VolumeAnomalyType.ExtremeSpike : VolumeAnomalyType.Spike,
                Severity = volumeRatio > VolumeSpikeThreshold * 2 ? AnomalySeverity.Critical : AnomalySeverity.High,
                Volume = newBar.Volume,
                AverageVolume = (long)avgVolume,
                VolumeRatio = volumeRatio,
                Description = $"Real-time volume spike: {volumeRatio:F1}x average"
            };
        }

        // Check for dry-up
        if (volumeRatio < VolumeDryUpThreshold)
        {
            return new VolumeAnomaly
            {
                Index = recentPrices.Count,
                Timestamp = newBar.Timestamp,
                Type = VolumeAnomalyType.DryUp,
                Severity = volumeRatio < VolumeDryUpThreshold / 2 ? AnomalySeverity.High : AnomalySeverity.Medium,
                Volume = newBar.Volume,
                AverageVolume = (long)avgVolume,
                VolumeRatio = volumeRatio,
                Description = $"Real-time volume dry-up: {volumeRatio:P0} of average"
            };
        }

        // Check for price-volume divergence
        var priceChange = Math.Abs((double)((newBar.Close - recentPrices[^1].Close) / recentPrices[^1].Close));
        var avgPriceChange = recentPrices.TakeLast(LookbackPeriod).Skip(1)
            .Zip(recentPrices.TakeLast(LookbackPeriod), (curr, prev) =>
                Math.Abs((double)((curr.Close - prev.Close) / prev.Close)))
            .Average();

        if (priceChange > avgPriceChange * 2 && volumeRatio < 0.5)
        {
            return new VolumeAnomaly
            {
                Index = recentPrices.Count,
                Timestamp = newBar.Timestamp,
                Type = VolumeAnomalyType.PriceVolumeDivergence,
                Severity = AnomalySeverity.High,
                Volume = newBar.Volume,
                AverageVolume = (long)avgVolume,
                VolumeRatio = volumeRatio,
                Description = $"Price move ({priceChange:P2}) on low volume ({volumeRatio:P0} avg)"
            };
        }

        return null;
    }

    /// <summary>
    /// Analyzes volume profile for unusual activity.
    /// </summary>
    public VolumeProfileAnalysis AnalyzeVolumeProfile(IReadOnlyList<PriceBar> prices)
    {
        if (prices.Count < LookbackPeriod)
        {
            return new VolumeProfileAnalysis();
        }

        var volumes = prices.Select(p => (double)p.Volume).ToArray();
        var avgVolume = volumes.Average();
        var stdDev = Math.Sqrt(volumes.Select(v => Math.Pow(v - avgVolume, 2)).Average());

        // Calculate percentiles
        var sortedVolumes = volumes.OrderBy(v => v).ToArray();
        var p10 = sortedVolumes[(int)(sortedVolumes.Length * 0.1)];
        var p50 = sortedVolumes[(int)(sortedVolumes.Length * 0.5)];
        var p90 = sortedVolumes[(int)(sortedVolumes.Length * 0.9)];

        // Calculate volume trend
        var firstHalf = volumes.Take(volumes.Length / 2).Average();
        var secondHalf = volumes.Skip(volumes.Length / 2).Average();
        var volumeTrend = (secondHalf - firstHalf) / firstHalf;

        // Calculate volume concentration (how much volume in high-volume days)
        var topQuartileVolume = volumes.OrderByDescending(v => v).Take(volumes.Length / 4).Sum();
        var totalVolume = volumes.Sum();
        var volumeConcentration = topQuartileVolume / totalVolume;

        return new VolumeProfileAnalysis
        {
            AverageVolume = (long)avgVolume,
            VolumeStdDev = stdDev,
            Volume10thPercentile = (long)p10,
            Volume50thPercentile = (long)p50,
            Volume90thPercentile = (long)p90,
            VolumeTrend = volumeTrend,
            VolumeConcentration = volumeConcentration,
            IsIncreasing = volumeTrend > 0.1,
            IsDecreasing = volumeTrend < -0.1,
            HighConcentration = volumeConcentration > 0.5
        };
    }

    private List<VolumeAnomaly> DetectVolumeSpikes(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<VolumeAnomaly>();

        for (var i = LookbackPeriod; i < prices.Count; i++)
        {
            var lookbackVolumes = prices.Skip(i - LookbackPeriod).Take(LookbackPeriod)
                .Select(p => (double)p.Volume).ToArray();

            var avgVolume = lookbackVolumes.Average();
            var volumeRatio = prices[i].Volume / avgVolume;

            if (volumeRatio > VolumeSpikeThreshold * 2)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.ExtremeSpike,
                    Severity = AnomalySeverity.Critical,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)avgVolume,
                    VolumeRatio = volumeRatio,
                    Description = $"Extreme volume spike: {volumeRatio:F1}x average"
                });
            }
            else if (volumeRatio > VolumeSpikeThreshold)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.Spike,
                    Severity = AnomalySeverity.High,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)avgVolume,
                    VolumeRatio = volumeRatio,
                    Description = $"Volume spike: {volumeRatio:F1}x average"
                });
            }
        }

        return anomalies;
    }

    private List<VolumeAnomaly> DetectVolumeDryUps(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<VolumeAnomaly>();

        for (var i = LookbackPeriod; i < prices.Count; i++)
        {
            var lookbackVolumes = prices.Skip(i - LookbackPeriod).Take(LookbackPeriod)
                .Select(p => (double)p.Volume).ToArray();

            var avgVolume = lookbackVolumes.Average();
            var volumeRatio = prices[i].Volume / avgVolume;

            if (volumeRatio < VolumeDryUpThreshold)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.DryUp,
                    Severity = volumeRatio < VolumeDryUpThreshold / 2 ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)avgVolume,
                    VolumeRatio = volumeRatio,
                    Description = $"Volume dry-up: {volumeRatio:P0} of average"
                });
            }
        }

        return anomalies;
    }

    private List<VolumeAnomaly> DetectPriceVolumeDivergence(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<VolumeAnomaly>();

        for (var i = LookbackPeriod; i < prices.Count; i++)
        {
            var lookback = prices.Skip(i - LookbackPeriod).Take(LookbackPeriod + 1).ToList();

            // Calculate average price change and volume
            var priceChanges = lookback.Skip(1).Zip(lookback, (curr, prev) =>
                Math.Abs((double)((curr.Close - prev.Close) / prev.Close))).ToList();

            var avgPriceChange = priceChanges.Take(LookbackPeriod).Average();
            var avgVolume = lookback.Take(LookbackPeriod).Average(p => (double)p.Volume);

            var currentPriceChange = (double)Math.Abs((prices[i].Close - prices[i - 1].Close) / prices[i - 1].Close);
            var currentVolumeRatio = prices[i].Volume / avgVolume;

            // Big price move on low volume
            if (currentPriceChange > avgPriceChange * DivergenceThreshold && currentVolumeRatio < 0.5)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.PriceVolumeDivergence,
                    Severity = AnomalySeverity.High,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)avgVolume,
                    VolumeRatio = currentVolumeRatio,
                    Description = $"Large price move ({currentPriceChange:P2}) on low volume ({currentVolumeRatio:P0})"
                });
            }

            // Small price move on high volume (distribution/accumulation)
            if (currentPriceChange < avgPriceChange * 0.5 && currentVolumeRatio > VolumeSpikeThreshold)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.AccumulationDistribution,
                    Severity = AnomalySeverity.Medium,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)avgVolume,
                    VolumeRatio = currentVolumeRatio,
                    Description = $"High volume ({currentVolumeRatio:F1}x) with small price change ({currentPriceChange:P2})"
                });
            }
        }

        return anomalies;
    }

    private List<VolumeAnomaly> DetectVolumePatternBreaks(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<VolumeAnomaly>();
        var shortPeriod = 5;

        for (var i = LookbackPeriod + shortPeriod; i < prices.Count; i++)
        {
            // Calculate long-term volume trend
            var longTermVolumes = prices.Skip(i - LookbackPeriod - shortPeriod).Take(LookbackPeriod)
                .Select(p => (double)p.Volume).ToArray();

            // Calculate short-term volume trend
            var shortTermVolumes = prices.Skip(i - shortPeriod).Take(shortPeriod)
                .Select(p => (double)p.Volume).ToArray();

            var longTermAvg = longTermVolumes.Average();
            var shortTermAvg = shortTermVolumes.Average();

            // Calculate trend direction
            var longTermTrend = CalculateTrend(longTermVolumes);
            var shortTermTrend = CalculateTrend(shortTermVolumes);

            // Detect trend reversal
            if (Math.Sign(longTermTrend) != Math.Sign(shortTermTrend) &&
                Math.Abs(shortTermTrend) > 0.5 &&
                Math.Abs(shortTermAvg / longTermAvg - 1) > 0.3)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.TrendReversal,
                    Severity = AnomalySeverity.Medium,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)longTermAvg,
                    VolumeRatio = shortTermAvg / longTermAvg,
                    Description = $"Volume trend reversal: long-term {(longTermTrend > 0 ? "up" : "down")}, short-term {(shortTermTrend > 0 ? "up" : "down")}"
                });
            }
        }

        return anomalies;
    }

    private List<VolumeAnomaly> DetectUnusualVolumeDistribution(IReadOnlyList<PriceBar> prices)
    {
        var anomalies = new List<VolumeAnomaly>();

        for (var i = LookbackPeriod; i < prices.Count; i++)
        {
            var lookbackVolumes = prices.Skip(i - LookbackPeriod).Take(LookbackPeriod)
                .Select(p => (double)p.Volume).ToArray();

            var currentVolume = (double)prices[i].Volume;

            // Calculate percentile of current volume
            var percentile = lookbackVolumes.Count(v => v <= currentVolume) / (double)lookbackVolumes.Length * 100;

            // Detect outliers using IQR method
            var sorted = lookbackVolumes.OrderBy(v => v).ToArray();
            var q1 = sorted[(int)(sorted.Length * 0.25)];
            var q3 = sorted[(int)(sorted.Length * 0.75)];
            var iqr = q3 - q1;

            var upperBound = q3 + 3 * iqr; // Extreme outlier
            var lowerBound = q1 - 3 * iqr;

            if (currentVolume > upperBound)
            {
                anomalies.Add(new VolumeAnomaly
                {
                    Index = i,
                    Timestamp = prices[i].Timestamp,
                    Type = VolumeAnomalyType.StatisticalOutlier,
                    Severity = percentile > 99 ? AnomalySeverity.Critical : AnomalySeverity.High,
                    Volume = prices[i].Volume,
                    AverageVolume = (long)lookbackVolumes.Average(),
                    VolumeRatio = currentVolume / lookbackVolumes.Average(),
                    Description = $"Volume statistical outlier: {percentile:F0}th percentile"
                });
            }
        }

        return anomalies;
    }

    private static double CalculateTrend(double[] values)
    {
        if (values.Length < 2) return 0;

        var n = values.Length;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (var i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumX2 += i * i;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var avgY = sumY / n;

        return avgY > 0 ? slope / avgY : 0;
    }
}

/// <summary>
/// Detected volume anomaly.
/// </summary>
public sealed class VolumeAnomaly
{
    /// <summary>Gets or sets the index in the price series.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the anomaly type.</summary>
    public VolumeAnomalyType Type { get; set; }

    /// <summary>Gets or sets the severity.</summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>Gets or sets the volume at anomaly.</summary>
    public long Volume { get; set; }

    /// <summary>Gets or sets the average volume.</summary>
    public long AverageVolume { get; set; }

    /// <summary>Gets or sets the volume ratio.</summary>
    public double VolumeRatio { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Type of volume anomaly.
/// </summary>
public enum VolumeAnomalyType
{
    /// <summary>Volume spike.</summary>
    Spike,

    /// <summary>Extreme volume spike.</summary>
    ExtremeSpike,

    /// <summary>Volume dry-up.</summary>
    DryUp,

    /// <summary>Price-volume divergence.</summary>
    PriceVolumeDivergence,

    /// <summary>Accumulation or distribution pattern.</summary>
    AccumulationDistribution,

    /// <summary>Volume trend reversal.</summary>
    TrendReversal,

    /// <summary>Statistical outlier.</summary>
    StatisticalOutlier
}

/// <summary>
/// Volume profile analysis result.
/// </summary>
public sealed class VolumeProfileAnalysis
{
    /// <summary>Gets or sets the average volume.</summary>
    public long AverageVolume { get; set; }

    /// <summary>Gets or sets the volume standard deviation.</summary>
    public double VolumeStdDev { get; set; }

    /// <summary>Gets or sets the 10th percentile volume.</summary>
    public long Volume10thPercentile { get; set; }

    /// <summary>Gets or sets the 50th percentile volume (median).</summary>
    public long Volume50thPercentile { get; set; }

    /// <summary>Gets or sets the 90th percentile volume.</summary>
    public long Volume90thPercentile { get; set; }

    /// <summary>Gets or sets the volume trend.</summary>
    public double VolumeTrend { get; set; }

    /// <summary>Gets or sets the volume concentration.</summary>
    public double VolumeConcentration { get; set; }

    /// <summary>Gets or sets whether volume is increasing.</summary>
    public bool IsIncreasing { get; set; }

    /// <summary>Gets or sets whether volume is decreasing.</summary>
    public bool IsDecreasing { get; set; }

    /// <summary>Gets or sets whether volume is highly concentrated.</summary>
    public bool HighConcentration { get; set; }
}
