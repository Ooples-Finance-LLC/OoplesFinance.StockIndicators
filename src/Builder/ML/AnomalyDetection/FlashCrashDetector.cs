namespace OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection;

/// <summary>
/// Detects flash crashes and rapid market dislocations.
/// Monitors for extreme price moves, liquidity evaporation, and circuit breaker conditions.
/// </summary>
public sealed class FlashCrashDetector
{
    /// <summary>Gets or sets the minimum price drop percentage to trigger alert.</summary>
    public double MinPriceDropPercent { get; set; } = 5.0;

    /// <summary>Gets or sets the maximum time window in minutes for flash crash detection.</summary>
    public int MaxTimeWindowMinutes { get; set; } = 5;

    /// <summary>Gets or sets the recovery threshold percentage.</summary>
    public double RecoveryThreshold { get; set; } = 50.0;

    /// <summary>Gets or sets the circuit breaker Level 1 threshold (7%).</summary>
    public double CircuitBreakerLevel1 { get; set; } = 7.0;

    /// <summary>Gets or sets the circuit breaker Level 2 threshold (13%).</summary>
    public double CircuitBreakerLevel2 { get; set; } = 13.0;

    /// <summary>Gets or sets the circuit breaker Level 3 threshold (20%).</summary>
    public double CircuitBreakerLevel3 { get; set; } = 20.0;

    /// <summary>Gets or sets the bid-ask spread spike threshold (multiplier).</summary>
    public double SpreadSpikeThreshold { get; set; } = 5.0;

    /// <summary>
    /// Detects flash crash events in intraday data.
    /// </summary>
    /// <param name="ticks">Intraday tick or minute data.</param>
    /// <param name="referencePrice">Reference price (e.g., previous close).</param>
    /// <returns>List of detected flash crash events.</returns>
    public List<FlashCrashEvent> DetectFlashCrashes(
        IReadOnlyList<IntradayTick> ticks,
        decimal referencePrice)
    {
        var events = new List<FlashCrashEvent>();

        if (ticks.Count < 2)
        {
            return events;
        }

        // Track running high/low for potential flash crashes
        var runningHigh = ticks[0].Price;
        var runningLow = ticks[0].Price;
        var highIndex = 0;
        var lowIndex = 0;

        for (var i = 1; i < ticks.Count; i++)
        {
            var currentPrice = ticks[i].Price;

            // Update running extremes
            if (currentPrice > runningHigh)
            {
                runningHigh = currentPrice;
                highIndex = i;
                // Reset low tracking after new high
                runningLow = currentPrice;
                lowIndex = i;
            }

            if (currentPrice < runningLow)
            {
                runningLow = currentPrice;
                lowIndex = i;

                // Check for flash crash from recent high
                var dropPercent = (double)((runningHigh - runningLow) / runningHigh * 100);
                var timeDiff = (ticks[lowIndex].Timestamp - ticks[highIndex].Timestamp).TotalMinutes;

                if (dropPercent >= MinPriceDropPercent && timeDiff <= MaxTimeWindowMinutes)
                {
                    // Check for recovery
                    var recovered = false;
                    var recoveryIndex = -1;
                    var recoveryPrice = 0m;

                    for (var j = lowIndex + 1; j < Math.Min(ticks.Count, lowIndex + MaxTimeWindowMinutes * 60); j++)
                    {
                        var recoveryPercent = (double)((ticks[j].Price - runningLow) / (runningHigh - runningLow) * 100);
                        if (recoveryPercent >= RecoveryThreshold)
                        {
                            recovered = true;
                            recoveryIndex = j;
                            recoveryPrice = ticks[j].Price;
                            break;
                        }
                    }

                    var flashCrash = new FlashCrashEvent
                    {
                        StartIndex = highIndex,
                        LowIndex = lowIndex,
                        RecoveryIndex = recoveryIndex,
                        StartTimestamp = ticks[highIndex].Timestamp,
                        LowTimestamp = ticks[lowIndex].Timestamp,
                        RecoveryTimestamp = recovered ? ticks[recoveryIndex].Timestamp : null,
                        HighPrice = runningHigh,
                        LowPrice = runningLow,
                        RecoveryPrice = recoveryPrice,
                        DropPercent = dropPercent,
                        DropDuration = TimeSpan.FromMinutes(timeDiff),
                        Recovered = recovered,
                        Type = ClassifyFlashCrash(dropPercent, timeDiff, recovered),
                        Severity = DetermineSeverity(dropPercent),
                        Description = BuildDescription(dropPercent, timeDiff, recovered)
                    };

                    // Avoid duplicate detections
                    if (!events.Any(e => Math.Abs((e.LowTimestamp - flashCrash.LowTimestamp).TotalMinutes) < 1))
                    {
                        events.Add(flashCrash);
                    }
                }
            }
        }

        return events;
    }

    /// <summary>
    /// Checks circuit breaker conditions against reference price.
    /// </summary>
    /// <param name="currentPrice">Current price.</param>
    /// <param name="referencePrice">Reference price (previous close).</param>
    /// <returns>Circuit breaker status.</returns>
    public CircuitBreakerStatus CheckCircuitBreaker(decimal currentPrice, decimal referencePrice)
    {
        var changePercent = (double)((currentPrice - referencePrice) / referencePrice * 100);
        var absChange = Math.Abs(changePercent);

        var level = CircuitBreakerLevel.None;
        var triggered = false;
        var haltDuration = TimeSpan.Zero;

        if (absChange >= CircuitBreakerLevel3)
        {
            level = CircuitBreakerLevel.Level3;
            triggered = true;
            haltDuration = TimeSpan.FromHours(24); // Trading halted for day
        }
        else if (absChange >= CircuitBreakerLevel2)
        {
            level = CircuitBreakerLevel.Level2;
            triggered = true;
            haltDuration = TimeSpan.FromMinutes(15);
        }
        else if (absChange >= CircuitBreakerLevel1)
        {
            level = CircuitBreakerLevel.Level1;
            triggered = true;
            haltDuration = TimeSpan.FromMinutes(15);
        }

        return new CircuitBreakerStatus
        {
            Level = level,
            Triggered = triggered,
            ChangePercent = changePercent,
            ReferencePrice = referencePrice,
            CurrentPrice = currentPrice,
            ExpectedHaltDuration = haltDuration,
            DistanceToNextLevel = CalculateDistanceToNextLevel(absChange)
        };
    }

    /// <summary>
    /// Real-time flash crash detection for streaming data.
    /// </summary>
    public FlashCrashAlert? MonitorRealTime(
        IReadOnlyList<IntradayTick> recentTicks,
        IntradayTick newTick,
        decimal referencePrice)
    {
        if (recentTicks.Count < 10)
        {
            return null;
        }

        // Find recent high
        var recentPrices = recentTicks.TakeLast(MaxTimeWindowMinutes * 60).ToList();
        var recentHigh = recentPrices.Max(t => t.Price);
        var highTimestamp = recentPrices.First(t => t.Price == recentHigh).Timestamp;

        // Check drop from recent high
        var dropFromHigh = (double)((recentHigh - newTick.Price) / recentHigh * 100);
        var timeSinceHigh = (newTick.Timestamp - highTimestamp).TotalMinutes;

        if (dropFromHigh >= MinPriceDropPercent && timeSinceHigh <= MaxTimeWindowMinutes)
        {
            return new FlashCrashAlert
            {
                Timestamp = newTick.Timestamp,
                CurrentPrice = newTick.Price,
                RecentHighPrice = recentHigh,
                ReferencePrice = referencePrice,
                DropFromHigh = dropFromHigh,
                DropFromReference = (double)((referencePrice - newTick.Price) / referencePrice * 100),
                TimeSinceHigh = TimeSpan.FromMinutes(timeSinceHigh),
                Severity = DetermineSeverity(dropFromHigh),
                IsActive = true,
                Message = $"FLASH CRASH ALERT: Price dropped {dropFromHigh:F2}% from {recentHigh:F2} to {newTick.Price:F2} in {timeSinceHigh:F1} minutes"
            };
        }

        // Check circuit breaker from reference
        var changeFromReference = (double)((newTick.Price - referencePrice) / referencePrice * 100);
        if (Math.Abs(changeFromReference) >= CircuitBreakerLevel1)
        {
            var cbStatus = CheckCircuitBreaker(newTick.Price, referencePrice);
            if (cbStatus.Triggered)
            {
                return new FlashCrashAlert
                {
                    Timestamp = newTick.Timestamp,
                    CurrentPrice = newTick.Price,
                    RecentHighPrice = recentHigh,
                    ReferencePrice = referencePrice,
                    DropFromHigh = dropFromHigh,
                    DropFromReference = changeFromReference,
                    TimeSinceHigh = TimeSpan.FromMinutes(timeSinceHigh),
                    Severity = AnomalySeverity.Critical,
                    IsActive = true,
                    CircuitBreakerTriggered = true,
                    CircuitBreakerLevel = cbStatus.Level,
                    Message = $"CIRCUIT BREAKER {cbStatus.Level}: {changeFromReference:F2}% from reference"
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Monitors liquidity conditions that often precede flash crashes.
    /// </summary>
    public LiquidityAlert? MonitorLiquidity(
        decimal bidPrice,
        decimal askPrice,
        long bidSize,
        long askSize,
        decimal normalSpread,
        long normalDepth)
    {
        var currentSpread = askPrice - bidPrice;
        var spreadMultipleDecimal = currentSpread / normalSpread;
        var spreadMultiple = (double)spreadMultipleDecimal;
        var depthRatio = (bidSize + askSize) / (double)normalDepth;

        // Detect liquidity evaporation
        if (spreadMultiple > SpreadSpikeThreshold || depthRatio < 0.2)
        {
            return new LiquidityAlert
            {
                Timestamp = DateTime.UtcNow,
                BidPrice = bidPrice,
                AskPrice = askPrice,
                BidSize = bidSize,
                AskSize = askSize,
                Spread = currentSpread,
                SpreadMultiple = spreadMultiple,
                DepthRatio = depthRatio,
                Type = spreadMultiple > SpreadSpikeThreshold
                    ? LiquidityAlertType.SpreadSpike
                    : LiquidityAlertType.DepthEvaporation,
                Severity = spreadMultiple > SpreadSpikeThreshold * 2 || depthRatio < 0.1
                    ? AnomalySeverity.Critical
                    : AnomalySeverity.High,
                Message = $"Liquidity alert: Spread {spreadMultiple:F1}x normal, Depth {depthRatio:P0} of normal"
            };
        }

        return null;
    }

    /// <summary>
    /// Analyzes historical flash crashes for patterns.
    /// </summary>
    public FlashCrashAnalysis AnalyzeHistoricalCrashes(IReadOnlyList<FlashCrashEvent> events)
    {
        if (events.Count == 0)
        {
            return new FlashCrashAnalysis();
        }

        return new FlashCrashAnalysis
        {
            TotalEvents = events.Count,
            RecoveredCount = events.Count(e => e.Recovered),
            AverageDropPercent = events.Average(e => e.DropPercent),
            MaxDropPercent = events.Max(e => e.DropPercent),
            AverageDropDuration = TimeSpan.FromTicks((long)events.Average(e => e.DropDuration.Ticks)),
            RecoveryRate = events.Count(e => e.Recovered) / (double)events.Count,
            ByType = events.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()),
            BySeverity = events.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count()),
            MostCommonTimeOfDay = FindMostCommonTimeOfDay(events),
            WorstEvent = events.OrderByDescending(e => e.DropPercent).First()
        };
    }

    private FlashCrashType ClassifyFlashCrash(double dropPercent, double durationMinutes, bool recovered)
    {
        if (dropPercent >= 20 && durationMinutes <= 2)
        {
            return FlashCrashType.ExtremeCrash;
        }
        if (recovered && durationMinutes <= 5)
        {
            return FlashCrashType.ClassicFlashCrash;
        }
        if (!recovered && dropPercent >= 10)
        {
            return FlashCrashType.SustainedCrash;
        }
        if (durationMinutes <= 1)
        {
            return FlashCrashType.MicroCrash;
        }
        return FlashCrashType.MiniCrash;
    }

    private AnomalySeverity DetermineSeverity(double dropPercent)
    {
        return dropPercent switch
        {
            >= 20 => AnomalySeverity.Critical,
            >= 10 => AnomalySeverity.High,
            >= 5 => AnomalySeverity.Medium,
            _ => AnomalySeverity.Low
        };
    }

    private string BuildDescription(double dropPercent, double durationMinutes, bool recovered)
    {
        var recovery = recovered ? "with recovery" : "no recovery detected";
        return $"Flash crash: {dropPercent:F2}% drop in {durationMinutes:F1} minutes, {recovery}";
    }

    private double CalculateDistanceToNextLevel(double currentChange)
    {
        if (currentChange < CircuitBreakerLevel1)
            return CircuitBreakerLevel1 - currentChange;
        if (currentChange < CircuitBreakerLevel2)
            return CircuitBreakerLevel2 - currentChange;
        if (currentChange < CircuitBreakerLevel3)
            return CircuitBreakerLevel3 - currentChange;
        return 0;
    }

    private TimeSpan FindMostCommonTimeOfDay(IReadOnlyList<FlashCrashEvent> events)
    {
        if (events.Count == 0) return TimeSpan.Zero;

        var hourCounts = events
            .GroupBy(e => e.LowTimestamp.Hour)
            .OrderByDescending(g => g.Count())
            .First();

        return TimeSpan.FromHours(hourCounts.Key);
    }
}

/// <summary>
/// Intraday tick data.
/// </summary>
public sealed class IntradayTick
{
    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the volume.</summary>
    public long Volume { get; set; }

    /// <summary>Gets or sets the bid price.</summary>
    public decimal BidPrice { get; set; }

    /// <summary>Gets or sets the ask price.</summary>
    public decimal AskPrice { get; set; }

    /// <summary>Gets or sets the bid size.</summary>
    public long BidSize { get; set; }

    /// <summary>Gets or sets the ask size.</summary>
    public long AskSize { get; set; }
}

/// <summary>
/// Flash crash event.
/// </summary>
public sealed class FlashCrashEvent
{
    /// <summary>Gets or sets the start index.</summary>
    public int StartIndex { get; set; }

    /// <summary>Gets or sets the low index.</summary>
    public int LowIndex { get; set; }

    /// <summary>Gets or sets the recovery index.</summary>
    public int RecoveryIndex { get; set; }

    /// <summary>Gets or sets the start timestamp.</summary>
    public DateTime StartTimestamp { get; set; }

    /// <summary>Gets or sets the low timestamp.</summary>
    public DateTime LowTimestamp { get; set; }

    /// <summary>Gets or sets the recovery timestamp.</summary>
    public DateTime? RecoveryTimestamp { get; set; }

    /// <summary>Gets or sets the high price.</summary>
    public decimal HighPrice { get; set; }

    /// <summary>Gets or sets the low price.</summary>
    public decimal LowPrice { get; set; }

    /// <summary>Gets or sets the recovery price.</summary>
    public decimal RecoveryPrice { get; set; }

    /// <summary>Gets or sets the drop percentage.</summary>
    public double DropPercent { get; set; }

    /// <summary>Gets or sets the drop duration.</summary>
    public TimeSpan DropDuration { get; set; }

    /// <summary>Gets or sets whether the crash recovered.</summary>
    public bool Recovered { get; set; }

    /// <summary>Gets or sets the crash type.</summary>
    public FlashCrashType Type { get; set; }

    /// <summary>Gets or sets the severity.</summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Type of flash crash.
/// </summary>
public enum FlashCrashType
{
    /// <summary>Micro crash (very short duration).</summary>
    MicroCrash,

    /// <summary>Mini crash (5-10% drop).</summary>
    MiniCrash,

    /// <summary>Classic flash crash with recovery.</summary>
    ClassicFlashCrash,

    /// <summary>Extreme crash (20%+).</summary>
    ExtremeCrash,

    /// <summary>Sustained crash without recovery.</summary>
    SustainedCrash
}

/// <summary>
/// Circuit breaker level.
/// </summary>
public enum CircuitBreakerLevel
{
    /// <summary>No circuit breaker triggered.</summary>
    None,

    /// <summary>Level 1 (7% drop).</summary>
    Level1,

    /// <summary>Level 2 (13% drop).</summary>
    Level2,

    /// <summary>Level 3 (20% drop).</summary>
    Level3
}

/// <summary>
/// Circuit breaker status.
/// </summary>
public sealed class CircuitBreakerStatus
{
    /// <summary>Gets or sets the circuit breaker level.</summary>
    public CircuitBreakerLevel Level { get; set; }

    /// <summary>Gets or sets whether circuit breaker is triggered.</summary>
    public bool Triggered { get; set; }

    /// <summary>Gets or sets the change percentage.</summary>
    public double ChangePercent { get; set; }

    /// <summary>Gets or sets the reference price.</summary>
    public decimal ReferencePrice { get; set; }

    /// <summary>Gets or sets the current price.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>Gets or sets the expected halt duration.</summary>
    public TimeSpan ExpectedHaltDuration { get; set; }

    /// <summary>Gets or sets the distance to next level.</summary>
    public double DistanceToNextLevel { get; set; }
}

/// <summary>
/// Real-time flash crash alert.
/// </summary>
public sealed class FlashCrashAlert
{
    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the current price.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>Gets or sets the recent high price.</summary>
    public decimal RecentHighPrice { get; set; }

    /// <summary>Gets or sets the reference price.</summary>
    public decimal ReferencePrice { get; set; }

    /// <summary>Gets or sets the drop from high.</summary>
    public double DropFromHigh { get; set; }

    /// <summary>Gets or sets the drop from reference.</summary>
    public double DropFromReference { get; set; }

    /// <summary>Gets or sets the time since high.</summary>
    public TimeSpan TimeSinceHigh { get; set; }

    /// <summary>Gets or sets the severity.</summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>Gets or sets whether the alert is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets whether circuit breaker is triggered.</summary>
    public bool CircuitBreakerTriggered { get; set; }

    /// <summary>Gets or sets the circuit breaker level.</summary>
    public CircuitBreakerLevel CircuitBreakerLevel { get; set; }

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Liquidity alert.
/// </summary>
public sealed class LiquidityAlert
{
    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the bid price.</summary>
    public decimal BidPrice { get; set; }

    /// <summary>Gets or sets the ask price.</summary>
    public decimal AskPrice { get; set; }

    /// <summary>Gets or sets the bid size.</summary>
    public long BidSize { get; set; }

    /// <summary>Gets or sets the ask size.</summary>
    public long AskSize { get; set; }

    /// <summary>Gets or sets the spread.</summary>
    public decimal Spread { get; set; }

    /// <summary>Gets or sets the spread multiple.</summary>
    public double SpreadMultiple { get; set; }

    /// <summary>Gets or sets the depth ratio.</summary>
    public double DepthRatio { get; set; }

    /// <summary>Gets or sets the alert type.</summary>
    public LiquidityAlertType Type { get; set; }

    /// <summary>Gets or sets the severity.</summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Type of liquidity alert.
/// </summary>
public enum LiquidityAlertType
{
    /// <summary>Spread spike.</summary>
    SpreadSpike,

    /// <summary>Depth evaporation.</summary>
    DepthEvaporation,

    /// <summary>Both spread spike and depth evaporation.</summary>
    Combined
}

/// <summary>
/// Flash crash analysis summary.
/// </summary>
public sealed class FlashCrashAnalysis
{
    /// <summary>Gets or sets the total events.</summary>
    public int TotalEvents { get; set; }

    /// <summary>Gets or sets the recovered count.</summary>
    public int RecoveredCount { get; set; }

    /// <summary>Gets or sets the average drop percent.</summary>
    public double AverageDropPercent { get; set; }

    /// <summary>Gets or sets the max drop percent.</summary>
    public double MaxDropPercent { get; set; }

    /// <summary>Gets or sets the average drop duration.</summary>
    public TimeSpan AverageDropDuration { get; set; }

    /// <summary>Gets or sets the recovery rate.</summary>
    public double RecoveryRate { get; set; }

    /// <summary>Gets or sets counts by type.</summary>
    public Dictionary<FlashCrashType, int> ByType { get; set; } = new();

    /// <summary>Gets or sets counts by severity.</summary>
    public Dictionary<AnomalySeverity, int> BySeverity { get; set; } = new();

    /// <summary>Gets or sets the most common time of day.</summary>
    public TimeSpan MostCommonTimeOfDay { get; set; }

    /// <summary>Gets or sets the worst event.</summary>
    public FlashCrashEvent? WorstEvent { get; set; }
}
