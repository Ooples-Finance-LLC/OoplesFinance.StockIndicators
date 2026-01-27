namespace OoplesFinance.StockIndicators.Tests.IntegrationTests;

using OoplesFinance.StockIndicators.Builder.Risk;
using Xunit;

/// <summary>
/// Integration tests for risk management components.
/// These tests verify VaR, CVaR, Kelly Criterion, and exposure limit calculations.
/// </summary>
[Trait("Category", "Integration")]
public class RiskManagementTests
{
    /// <summary>
    /// Tests that Historical VaR calculation produces reasonable results.
    /// </summary>
    [Fact]
    public void HistoricalVaR_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = RiskTestHelpers.GenerateSampleReturns(252, 0.0005m, 0.02m);
        var confidenceLevel = 0.95m;
        var portfolioValue = 100000m;

        // Act
        var var95 = calculator.CalculateHistoricalVaR(returns, confidenceLevel, portfolioValue);

        // Assert
        Assert.True(var95 > 0, "VaR should be positive");
        Assert.True(var95 < portfolioValue, "VaR should be less than portfolio value");
    }

    /// <summary>
    /// Tests that Parametric VaR calculation produces reasonable results.
    /// </summary>
    [Fact]
    public void ParametricVaR_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = RiskTestHelpers.GenerateSampleReturns(252, 0.0004m, 0.015m);
        var confidenceLevel = 0.95m;
        var portfolioValue = 100000m;

        // Act
        var var95 = calculator.CalculateParametricVaR(returns, confidenceLevel, portfolioValue);

        // Assert
        Assert.True(var95 > 0, "Parametric VaR should be positive");
        Assert.True(var95 < portfolioValue * 0.1m, "VaR should be reasonable (< 10% of portfolio)");
    }

    /// <summary>
    /// Tests that multi-day VaR scales correctly with holding period.
    /// </summary>
    [Fact]
    public void ParametricVaR_ShouldScaleWithHoldingPeriod()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = RiskTestHelpers.GenerateSampleReturns(252, 0.0004m, 0.015m);
        var portfolioValue = 100000m;

        // Act
        var var1Day = calculator.CalculateParametricVaR(returns, 0.95m, portfolioValue, holdingPeriod: 1);
        var var10Day = calculator.CalculateParametricVaR(returns, 0.95m, portfolioValue, holdingPeriod: 10);

        // Assert - 10-day VaR should be approximately sqrt(10) times 1-day VaR
        var expectedRatio = (decimal)Math.Sqrt(10);
        var actualRatio = var10Day / var1Day;
        Assert.True(Math.Abs(actualRatio - expectedRatio) < 0.1m,
            $"10-day VaR should scale by sqrt(10). Expected ratio: {expectedRatio:F2}, Actual: {actualRatio:F2}");
    }

    /// <summary>
    /// Tests that CVaR is greater than or equal to VaR.
    /// </summary>
    [Fact]
    public void CVaR_ShouldBeGreaterThanOrEqualToVaR()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = RiskTestHelpers.GenerateSampleReturns(500, 0.0003m, 0.02m);
        var confidenceLevel = 0.95m;
        var portfolioValue = 100000m;

        // Act
        var var95 = calculator.CalculateHistoricalVaR(returns, confidenceLevel, portfolioValue);
        var cvar95 = calculator.CalculateCVaR(returns, confidenceLevel, portfolioValue);

        // Assert
        Assert.True(cvar95 >= var95,
            $"CVaR ({cvar95:F2}) should be >= VaR ({var95:F2})");
    }

    /// <summary>
    /// Tests Beta calculation against market returns.
    /// </summary>
    [Fact]
    public void Beta_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();

        // Create correlated returns (beta = 1.2)
        var benchmarkReturns = RiskTestHelpers.GenerateSampleReturns(252, 0.0004m, 0.01m);
        var portfolioReturns = benchmarkReturns.Select(r => r * 1.2m + 0.0001m).ToList();

        // Act
        var beta = calculator.CalculateBeta(portfolioReturns, benchmarkReturns);

        // Assert - Beta should be approximately 1.2
        Assert.True(Math.Abs(beta - 1.2m) < 0.1m,
            $"Beta should be approximately 1.2, got {beta:F2}");
    }

    /// <summary>
    /// Tests correlation calculation between two return series.
    /// </summary>
    [Fact]
    public void Correlation_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();

        // Create perfectly correlated returns
        var returns1 = RiskTestHelpers.GenerateSampleReturns(100, 0.001m, 0.02m);
        var returns2 = returns1.Select(r => r * 2).ToList();

        // Act
        var correlation = calculator.CalculateCorrelation(returns1, returns2);

        // Assert - Perfect positive correlation should be 1
        Assert.True(Math.Abs(correlation - 1m) < 0.001m,
            $"Correlation of perfectly correlated series should be 1, got {correlation:F4}");
    }

    /// <summary>
    /// Tests correlation matrix calculation for multiple assets.
    /// </summary>
    [Fact]
    public void CorrelationMatrix_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var baseReturns = RiskTestHelpers.GenerateSampleReturns(100, 0.001m, 0.02m);

        var assetReturns = new Dictionary<string, IReadOnlyList<decimal>>
        {
            ["AAPL"] = baseReturns,
            ["MSFT"] = baseReturns.Select(r => r * 0.8m + 0.0002m).ToList(),
            ["GOOGL"] = baseReturns.Select(r => r * 0.6m + 0.0003m).ToList()
        };

        // Act
        var matrix = calculator.CalculateCorrelationMatrix(assetReturns);

        // Assert
        Assert.Equal(3, matrix.Assets.Count);
        Assert.Equal(1m, matrix.GetCorrelation("AAPL", "AAPL"));
        Assert.True(matrix.GetCorrelation("AAPL", "MSFT") > 0.9m,
            "Correlated assets should have high correlation");
    }

    /// <summary>
    /// Tests max drawdown calculation.
    /// </summary>
    [Fact]
    public void MaxDrawdown_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var equityCurve = new List<decimal>
        {
            100m, 110m, 105m, 120m, 90m, 95m, 100m, 85m, 90m, 100m
        };

        // Act
        var maxDrawdown = calculator.CalculateMaxDrawdown(equityCurve);

        // Assert - Max drawdown is from 120 to 85 = 35/120 = 29.17%
        var expected = (120m - 85m) / 120m;
        Assert.Equal(expected, maxDrawdown, 4);
    }

    /// <summary>
    /// Tests Sharpe ratio calculation.
    /// </summary>
    [Fact]
    public void SharpeRatio_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = RiskTestHelpers.GenerateSampleReturns(252, 0.001m, 0.01m);

        // Act
        var sharpe = calculator.CalculateSharpeRatio(returns, riskFreeRate: 0.02m);

        // Assert - With positive mean return and moderate volatility, Sharpe should be reasonable
        Assert.True(sharpe > -5m && sharpe < 10m,
            $"Sharpe ratio should be reasonable, got {sharpe:F2}");
    }

    /// <summary>
    /// Tests Sortino ratio calculation.
    /// </summary>
    [Fact]
    public void SortinoRatio_ShouldCalculateCorrectly()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var returns = RiskTestHelpers.GenerateSampleReturns(252, 0.001m, 0.015m);

        // Act
        var sortino = calculator.CalculateSortinoRatio(returns, riskFreeRate: 0.02m);

        // Assert - Sortino uses downside deviation so may be different from Sharpe
        Assert.True(sortino != 0, "Sortino ratio should be non-zero");
    }
}

/// <summary>
/// Tests for Kelly Criterion position sizing.
/// </summary>
[Trait("Category", "Integration")]
public class KellyCriterionTests
{
    /// <summary>
    /// Tests Kelly fraction with known inputs.
    /// </summary>
    [Theory]
    [InlineData(0.6, 1.5, 1.0, 0.35)] // 60% win rate, 1.5:1 reward/risk
    [InlineData(0.5, 1.0, 1.0, 0.0)]  // 50% win rate, 1:1 = break even
    [InlineData(0.4, 1.0, 1.0, 0.0)]  // 40% win rate, 1:1 = negative expectancy (clipped to 0)
    public void KellyFraction_ShouldCalculateCorrectly(
        double winRate,
        double avgWin,
        double avgLoss,
        double expectedKelly)
    {
        // Act
        var kelly = KellyCriterion.CalculateOptimalFraction(
            (decimal)winRate,
            (decimal)avgWin,
            (decimal)avgLoss);

        // Assert
        Assert.True(Math.Abs((double)kelly - expectedKelly) < 0.05,
            $"Kelly should be approximately {expectedKelly}, got {kelly}");
    }

    /// <summary>
    /// Tests Kelly calculation from historical trades.
    /// </summary>
    [Fact]
    public void KellyFromTrades_ShouldCalculateCorrectly()
    {
        // Arrange - 60% win rate with 1.5:1 reward/risk
        var trades = new List<decimal>
        {
            0.015m, -0.01m, 0.015m, 0.015m, -0.01m,
            0.015m, -0.01m, 0.015m, 0.015m, -0.01m
        };

        // Act
        var kelly = KellyCriterion.CalculateFromTrades(trades);

        // Assert - Should be positive (profitable system)
        Assert.True(kelly > 0, "Kelly should be positive for profitable system");
        Assert.True(kelly < 1, "Kelly should be less than 100%");
    }

    /// <summary>
    /// Tests fractional Kelly for conservative sizing.
    /// </summary>
    [Fact]
    public void FractionalKelly_ShouldReduceRisk()
    {
        // Arrange
        var fullKelly = 0.35m;

        // Act
        var halfKelly = KellyCriterion.FractionalKelly(fullKelly, 0.5m);
        var quarterKelly = KellyCriterion.FractionalKelly(fullKelly, 0.25m);

        // Assert
        Assert.Equal(0.175m, halfKelly);
        Assert.Equal(0.0875m, quarterKelly);
    }

    /// <summary>
    /// Tests position size calculation.
    /// </summary>
    [Fact]
    public void PositionSize_ShouldCalculateCorrectly()
    {
        // Arrange
        var accountValue = 100000m;
        var winRate = 0.6m;
        var avgWin = 0.015m;
        var avgLoss = 0.01m;

        // Act
        var positionSize = KellyCriterion.CalculatePositionSize(
            accountValue,
            winRate,
            avgWin,
            avgLoss,
            kellyFraction: 0.5m);

        // Assert - Should be a reasonable fraction of account
        Assert.True(positionSize > 0, "Position size should be positive");
        Assert.True(positionSize < accountValue * 0.5m,
            "Half Kelly position should be less than 50% of account");
    }
}

/// <summary>
/// Tests for exposure limits and risk controls.
/// </summary>
[Trait("Category", "Integration")]
public class ExposureLimitsTests
{
    /// <summary>
    /// Tests that single position violations are detected.
    /// </summary>
    [Fact]
    public void ExposureLimits_ShouldDetectSinglePositionViolation()
    {
        // Arrange
        var limits = new ExposureLimits
        {
            MaxSinglePositionPercent = 0.10m  // 10% max
        };

        var exposure = new PortfolioExposure
        {
            Positions = new List<PositionExposure>
            {
                new() { Symbol = "AAPL", PercentOfEquity = 0.08m },
                new() { Symbol = "TSLA", PercentOfEquity = 0.15m }  // Violation!
            }
        };

        // Act
        var violations = limits.CheckViolations(exposure);

        // Assert
        Assert.Single(violations);
        Assert.Equal(ViolationType.SinglePositionExceeded, violations[0].Type);
        Assert.Equal("TSLA", violations[0].Symbol);
    }

    /// <summary>
    /// Tests that sector exposure violations are detected.
    /// </summary>
    [Fact]
    public void ExposureLimits_ShouldDetectSectorViolation()
    {
        // Arrange
        var limits = new ExposureLimits
        {
            MaxSectorExposurePercent = 0.25m  // 25% max
        };

        var exposure = new PortfolioExposure
        {
            SectorExposures = new Dictionary<string, decimal>
            {
                ["Technology"] = 0.30m,  // Violation!
                ["Healthcare"] = 0.15m
            }
        };

        // Act
        var violations = limits.CheckViolations(exposure);

        // Assert
        Assert.Single(violations);
        Assert.Equal(ViolationType.SectorExposureExceeded, violations[0].Type);
        Assert.Contains("Technology", violations[0].Symbol!);
    }

    /// <summary>
    /// Tests that leverage violations are detected.
    /// </summary>
    [Fact]
    public void ExposureLimits_ShouldDetectLeverageViolation()
    {
        // Arrange
        var limits = new ExposureLimits
        {
            MaxLeverageRatio = 2.0m
        };

        var exposure = new PortfolioExposure
        {
            LeverageRatio = 2.5m  // Violation!
        };

        // Act
        var violations = limits.CheckViolations(exposure);

        // Assert
        Assert.Single(violations);
        Assert.Equal(ViolationType.LeverageExceeded, violations[0].Type);
    }

    /// <summary>
    /// Tests that multiple violations are detected simultaneously.
    /// </summary>
    [Fact]
    public void ExposureLimits_ShouldDetectMultipleViolations()
    {
        // Arrange
        var limits = new ExposureLimits
        {
            MaxSinglePositionPercent = 0.10m,
            MaxLeverageRatio = 2.0m,
            MaxPortfolioBeta = 1.5m
        };

        var exposure = new PortfolioExposure
        {
            Positions = new List<PositionExposure>
            {
                new() { Symbol = "AAPL", PercentOfEquity = 0.15m }  // Violation
            },
            LeverageRatio = 2.5m,  // Violation
            PortfolioBeta = 1.8m   // Violation
        };

        // Act
        var violations = limits.CheckViolations(exposure);

        // Assert
        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, v => v.Type == ViolationType.SinglePositionExceeded);
        Assert.Contains(violations, v => v.Type == ViolationType.LeverageExceeded);
        Assert.Contains(violations, v => v.Type == ViolationType.BetaExceeded);
    }

    /// <summary>
    /// Tests that no violations are detected when within limits.
    /// </summary>
    [Fact]
    public void ExposureLimits_ShouldPassWhenWithinLimits()
    {
        // Arrange
        var limits = new ExposureLimits();

        var exposure = new PortfolioExposure
        {
            Positions = new List<PositionExposure>
            {
                new() { Symbol = "AAPL", PercentOfEquity = 0.05m },
                new() { Symbol = "MSFT", PercentOfEquity = 0.05m }
            },
            SectorExposures = new Dictionary<string, decimal>
            {
                ["Technology"] = 0.10m
            },
            LeverageRatio = 1.0m,
            PortfolioBeta = 1.0m,
            DailyVaRPercent = 0.01m
        };

        // Act
        var violations = limits.CheckViolations(exposure);

        // Assert
        Assert.Empty(violations);
    }
}

/// <summary>
/// Tests for correlation matrix functionality.
/// </summary>
[Trait("Category", "Integration")]
public class CorrelationMatrixTests
{
    /// <summary>
    /// Tests finding highly correlated assets.
    /// </summary>
    [Fact]
    public void CorrelationMatrix_ShouldFindHighlyCorrelatedAssets()
    {
        // Arrange
        var calculator = new PortfolioRiskCalculator();
        var baseReturns = RiskTestHelpers.GenerateSampleReturns(100);

        var assetReturns = new Dictionary<string, IReadOnlyList<decimal>>
        {
            ["AAPL"] = baseReturns,
            ["MSFT"] = baseReturns.Select(r => r * 0.95m).ToList(),  // High correlation
            ["GOOGL"] = baseReturns.Select(r => r * 0.85m).ToList(), // High correlation
            ["JPM"] = RiskTestHelpers.GenerateSampleReturns(100)  // Independent - low correlation
        };

        var matrix = calculator.CalculateCorrelationMatrix(assetReturns);

        // Act
        var highlyCorrelated = matrix.FindHighlyCorrelated("AAPL", threshold: 0.7m);

        // Assert
        Assert.True(highlyCorrelated.Count >= 2,
            "Should find at least 2 highly correlated assets");
        Assert.Contains(highlyCorrelated, x => x.Asset == "MSFT");
        Assert.Contains(highlyCorrelated, x => x.Asset == "GOOGL");
    }

    /// <summary>
    /// Tests getting all correlations for an asset.
    /// </summary>
    [Fact]
    public void CorrelationMatrix_ShouldGetAllCorrelations()
    {
        // Arrange
        var assets = new List<string> { "A", "B", "C" };
        var matrix = new decimal[3, 3]
        {
            { 1.0m, 0.8m, 0.5m },
            { 0.8m, 1.0m, 0.6m },
            { 0.5m, 0.6m, 1.0m }
        };
        var correlationMatrix = new CorrelationMatrix(assets, matrix);

        // Act
        var correlations = correlationMatrix.GetCorrelationsFor("A");

        // Assert
        Assert.Equal(3, correlations.Count);
        Assert.Equal(1.0m, correlations["A"]);
        Assert.Equal(0.8m, correlations["B"]);
        Assert.Equal(0.5m, correlations["C"]);
    }

    private static List<decimal> GenerateSampleReturns(int count)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count)
            .Select(_ => (decimal)(random.NextDouble() - 0.5) * 0.04m)
            .ToList();
    }
}

/// <summary>
/// Helper methods for generating test data.
/// </summary>
internal static class RiskTestHelpers
{
    public static List<decimal> GenerateSampleReturns(int count, decimal mean = 0, decimal stdDev = 0.01m)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count)
            .Select(_ =>
            {
                // Box-Muller transform for normal distribution
                var u1 = random.NextDouble();
                var u2 = random.NextDouble();
                var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                return mean + (decimal)z * stdDev;
            })
            .ToList();
    }
}
