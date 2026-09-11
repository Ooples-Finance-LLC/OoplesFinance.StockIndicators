namespace OoplesFinance.StockIndicators.Tests.PerformanceTests;

using System.Diagnostics;
using OoplesFinance.StockIndicators.Builder.Risk;
using OoplesFinance.StockIndicators.Builder.Resilience;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Performance benchmarks for critical trading system components.
/// These tests verify that operations meet latency requirements.
/// </summary>
[Trait("Category", "Performance")]
[Collection(PerformanceCollection.Name)]
public class LatencyBenchmarks
{
    private readonly ITestOutputHelper _output;

    public LatencyBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Benchmarks VaR calculation speed.
    /// Target: &lt; 10ms for 252 days of data.
    /// </summary>
    [Fact]
    public void VaRCalculation_ShouldBeFast()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = GenerateReturns(252);
        var iterations = 1000;
        var warmup = 100;

        // Warmup
        for (var i = 0; i < warmup; i++)
        {
            calculator.CalculateHistoricalVaR(returns, 0.95m, 100000m);
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            calculator.CalculateHistoricalVaR(returns, 0.95m, 100000m);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"VaR calculation: {avgMs:F4}ms per call ({iterations} iterations)");
        Assert.True(avgMs < 10, $"VaR calculation should be < 10ms, was {avgMs:F4}ms");
    }

    /// <summary>
    /// Benchmarks correlation matrix calculation.
    /// Target: &lt; 100ms for 10 assets with 252 days.
    /// </summary>
    [Fact]
    public void CorrelationMatrix_ShouldBeFast()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var assetReturns = new Dictionary<string, IReadOnlyList<decimal>>();
        for (var i = 0; i < 10; i++)
        {
            assetReturns[$"ASSET_{i}"] = GenerateReturns(252);
        }

        var iterations = 100;
        var warmup = 10;

        // Warmup
        for (var i = 0; i < warmup; i++)
        {
            calculator.CalculateCorrelationMatrix(assetReturns);
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            calculator.CalculateCorrelationMatrix(assetReturns);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Correlation matrix: {avgMs:F4}ms per call ({iterations} iterations)");
        Assert.True(avgMs < 100, $"Correlation matrix should be < 100ms, was {avgMs:F4}ms");
    }

    /// <summary>
    /// Benchmarks exposure limit checking.
    /// Target: &lt; 1ms for 50 positions.
    /// </summary>
    [Fact]
    public void ExposureLimitCheck_ShouldBeFast()
    {
        // Arrange
        var limits = new ExposureLimits();
        var exposure = CreateLargePortfolioExposure(50);
        var iterations = 10000;
        var warmup = 1000;

        // Warmup
        for (var i = 0; i < warmup; i++)
        {
            limits.CheckViolations(exposure);
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            limits.CheckViolations(exposure);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Exposure limit check: {avgMs:F6}ms per call ({iterations} iterations)");
        Assert.True(avgMs < 1, $"Exposure limit check should be < 1ms, was {avgMs:F6}ms");
    }

    /// <summary>
    /// Benchmarks Kelly criterion calculation.
    /// Target: &lt; 0.1ms.
    /// </summary>
    [Fact]
    public void KellyCriterion_ShouldBeFast()
    {
        // Arrange
        var trades = GenerateReturns(1000);
        var iterations = 100000;
        var warmup = 10000;

        // Warmup
        for (var i = 0; i < warmup; i++)
        {
            KellyCriterion.CalculateFromTrades(trades);
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            KellyCriterion.CalculateFromTrades(trades);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Kelly criterion: {avgMs:F6}ms per call ({iterations} iterations)");
        Assert.True(avgMs < 0.5, $"Kelly criterion should be < 0.5ms, was {avgMs:F6}ms");
    }

    /// <summary>
    /// Benchmarks max drawdown calculation.
    /// Target: &lt; 5ms for 1000 points.
    /// </summary>
    [Fact]
    public void MaxDrawdown_ShouldBeFast()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var equityCurve = GenerateEquityCurve(1000);
        var iterations = 10000;
        var warmup = 1000;

        // Warmup
        for (var i = 0; i < warmup; i++)
        {
            calculator.CalculateMaxDrawdown(equityCurve);
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            calculator.CalculateMaxDrawdown(equityCurve);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Max drawdown: {avgMs:F6}ms per call ({iterations} iterations)");
        Assert.True(avgMs < 5, $"Max drawdown should be < 5ms, was {avgMs:F6}ms");
    }

    /// <summary>
    /// Benchmarks circuit breaker state check.
    /// Target: &lt; 0.01ms.
    /// </summary>
    [Fact]
    public void CircuitBreakerStateCheck_ShouldBeFast()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(
            failureThreshold: 5,
            successThreshold: 2,
            openDuration: TimeSpan.FromSeconds(30));

        var iterations = 1000000;
        var warmup = 100000;

        // Warmup
        for (var i = 0; i < warmup; i++)
        {
            _ = circuitBreaker.State;
            _ = circuitBreaker.FailureCount;
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _ = circuitBreaker.State;
            _ = circuitBreaker.FailureCount;
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Circuit breaker state check: {avgMs:F6}ms per call ({iterations} iterations)");
        Assert.True(avgMs < 0.01, $"Circuit breaker check should be < 0.01ms, was {avgMs:F6}ms");
    }

    #region Helper Methods

    private static List<decimal> GenerateReturns(int count)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count)
            .Select(_ => (decimal)(random.NextDouble() - 0.5) * 0.04m)
            .ToList();
    }

    private static List<decimal> GenerateEquityCurve(int count)
    {
        var random = new Random(42);
        var curve = new List<decimal>(count) { 100000m };

        for (var i = 1; i < count; i++)
        {
            var dailyReturn = 1 + (decimal)(random.NextDouble() - 0.48) * 0.02m;
            curve.Add(curve[i - 1] * dailyReturn);
        }

        return curve;
    }

    private static PortfolioExposure CreateLargePortfolioExposure(int positionCount)
    {
        var positions = Enumerable.Range(0, positionCount)
            .Select(i => new PositionExposure
            {
                Symbol = $"STOCK_{i}",
                PercentOfEquity = 1.0m / positionCount,
                MarketValue = 100000m / positionCount,
                Sector = i % 5 == 0 ? "Technology" : i % 3 == 0 ? "Healthcare" : "Finance",
                Beta = 1.0m + (i % 10) * 0.1m
            })
            .ToList();

        var sectorExposures = positions
            .GroupBy(p => p.Sector)
            .ToDictionary(g => g.Key!, g => g.Sum(p => p.PercentOfEquity));

        return new PortfolioExposure
        {
            Positions = positions,
            SectorExposures = sectorExposures,
            LeverageRatio = 1.0m,
            PortfolioBeta = 1.0m,
            DailyVaRPercent = 0.015m,
            TotalEquity = 100000m,
            GrossExposure = 100000m,
            NetExposure = 100000m
        };
    }

    #endregion
}

/// <summary>
/// Throughput benchmarks for high-frequency operations.
/// </summary>
[Trait("Category", "Performance")]
[Collection(PerformanceCollection.Name)]
public class ThroughputBenchmarks
{
    private readonly ITestOutputHelper _output;

    public ThroughputBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Benchmarks concurrent VaR calculations.
    /// Target: > 1000 calculations/second.
    /// </summary>
    [Fact]
    public async Task ConcurrentVaRCalculations_ShouldScaleWell()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = GenerateReturns(252);
        var parallelism = Environment.ProcessorCount;
        var calculationsPerThread = 500;
        var totalCalculations = parallelism * calculationsPerThread;

        // Act
        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < calculationsPerThread; i++)
                {
                    calculator.CalculateHistoricalVaR(returns, 0.95m, 100000m);
                }
            }));

        await Task.WhenAll(tasks);
        sw.Stop();

        var calculationsPerSecond = totalCalculations / sw.Elapsed.TotalSeconds;

        // Assert
        _output.WriteLine($"Concurrent VaR: {calculationsPerSecond:F0} calculations/second ({parallelism} threads)");
        Assert.True(calculationsPerSecond > 1000,
            $"Should achieve > 1000 calc/sec, got {calculationsPerSecond:F0}");
    }

    /// <summary>
    /// Benchmarks rate limiter under high load.
    /// Target: Correctly limits to configured rate.
    /// </summary>
    [Fact]
    public async Task RateLimiter_ShouldEnforceRateUnderLoad()
    {
        // Arrange
        var maxPermitsPerSecond = 100;
        var rateLimiter = new SlidingWindowRateLimiter(maxPermitsPerSecond, TimeSpan.FromSeconds(1));
        var testDurationMs = 1000;
        var acquiredCount = 0;

        // Act
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < testDurationMs)
        {
            if (rateLimiter.TryAcquire())
            {
                Interlocked.Increment(ref acquiredCount);
            }
            await Task.Delay(1);
        }
        sw.Stop();

        var acquiresPerSecond = acquiredCount / sw.Elapsed.TotalSeconds;

        // Assert
        _output.WriteLine($"Rate limiter: {acquiresPerSecond:F0} permits/second (limit: {maxPermitsPerSecond})");
        Assert.True(acquiredCount <= maxPermitsPerSecond * 1.5,
            $"Should not exceed rate limit significantly, got {acquiredCount} (limit: {maxPermitsPerSecond})");
    }

    private static List<decimal> GenerateReturns(int count)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count)
            .Select(_ => (decimal)(random.NextDouble() - 0.5) * 0.04m)
            .ToList();
    }
}

/// <summary>
/// Memory allocation benchmarks.
/// </summary>
[Trait("Category", "Performance")]
[Collection(PerformanceCollection.Name)]
public class MemoryBenchmarks
{
    private readonly ITestOutputHelper _output;

    public MemoryBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Verifies that VaR calculation has minimal allocation.
    /// </summary>
    [Fact]
    public void VaRCalculation_ShouldHaveMinimalAllocation()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = GenerateReturns(252);

        // Warm up so first-call JIT and one-off setup are not counted as per-call allocation.
        for (var i = 0; i < 50; i++)
        {
            calculator.CalculateHistoricalVaR(returns, 0.95m, 100000m);
        }

        // GC.GetTotalMemory measures the WHOLE PROCESS, and xUnit runs test collections in
        // parallel, so another test class allocating concurrently lands in the reading. It also
        // reports bytes currently held rather than bytes allocated, so whether a collection happens
        // to run inside the loop changes the answer - and can make it negative.
        // GetAllocatedBytesForCurrentThread is cumulative and thread-local: unaffected by GC timing
        // and by every other test running at the same time.
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        // Act - Run many iterations
        for (var i = 0; i < 1000; i++)
        {
            calculator.CalculateHistoricalVaR(returns, 0.95m, 100000m);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        var bytesPerCall = allocatedBytes / 1000;

        // Assert
        _output.WriteLine($"Memory per VaR call: {bytesPerCall:N0} bytes");

        // Allow some allocation but should be bounded
        Assert.True(bytesPerCall < 50000,
            $"VaR should allocate < 50KB per call, allocated {bytesPerCall:N0} bytes");
    }

    /// <summary>
    /// Verifies that exposure checks don't allocate when no violations.
    /// </summary>
    [Fact]
    public void ExposureCheck_ShouldHaveMinimalAllocation()
    {
        // Arrange
        var limits = new ExposureLimits();
        var exposure = new PortfolioExposure
        {
            Positions = new List<PositionExposure>
            {
                new() { Symbol = "AAPL", PercentOfEquity = 0.05m }
            },
            SectorExposures = new Dictionary<string, decimal> { ["Tech"] = 0.05m },
            LeverageRatio = 1.0m,
            PortfolioBeta = 1.0m,
            DailyVaRPercent = 0.01m
        };

        // Warmup
        for (var i = 0; i < 100; i++)
        {
            limits.CheckViolations(exposure);
        }

        // Thread-local and cumulative, for the reasons given on the VaR benchmark above: a
        // process-wide GC.GetTotalMemory reading is polluted by the other test collections xUnit
        // runs in parallel, and depends on whether a collection happens to fire inside the loop.
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        // Act
        for (var i = 0; i < 10000; i++)
        {
            limits.CheckViolations(exposure);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        var bytesPerCall = allocatedBytes / 10000;

        // Assert
        _output.WriteLine($"Memory per exposure check: {bytesPerCall:N0} bytes");

        // Should be very low since no violations creates empty list
        Assert.True(bytesPerCall < 1000,
            $"Exposure check should allocate < 1KB per call, allocated {bytesPerCall:N0} bytes");
    }

    private static List<decimal> GenerateReturns(int count)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count)
            .Select(_ => (decimal)(random.NextDouble() - 0.5) * 0.04m)
            .ToList();
    }
}
