using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// Integration tests for edge cases across all indicators.
/// These tests help find bugs with minimum data, NaN values, and other common issues.
/// </summary>
public sealed class EdgeCaseTests
{
    /// <summary>
    /// Creates test data with the specified number of bars.
    /// </summary>
    private static List<TickerData> CreateTestData(int count, double basePrice = 100.0, double volatility = 0.02)
    {
        var data = new List<TickerData>(count);
        var random = new Random(42); // Fixed seed for reproducibility
        var currentPrice = basePrice;
        var baseDate = new DateTime(2024, 1, 1);

        for (int i = 0; i < count; i++)
        {
            var change = (random.NextDouble() - 0.5) * 2 * volatility;
            currentPrice *= (1 + change);

            var high = currentPrice * (1 + random.NextDouble() * 0.01);
            var low = currentPrice * (1 - random.NextDouble() * 0.01);
            var open = low + (high - low) * random.NextDouble();
            var close = low + (high - low) * random.NextDouble();
            var volume = 1000000 + random.NextDouble() * 500000;

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });
        }

        return data;
    }

    /// <summary>
    /// Creates test data with constant values (no volatility).
    /// </summary>
    private static List<TickerData> CreateConstantData(int count, double price = 100.0, double volume = 1000000)
    {
        var data = new List<TickerData>(count);
        var baseDate = new DateTime(2024, 1, 1);

        for (int i = 0; i < count; i++)
        {
            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume
            });
        }

        return data;
    }

    #region Minimum Bars Tests - Core Indicators

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Sma_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateSimpleMovingAverage(Math.Min(5, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Ema_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateExponentialMovingAverage(Math.Min(5, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void Rsi_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateRelativeStrengthIndex());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void Macd_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateMovingAverageConvergenceDivergence());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(20)]
    public void BollingerBands_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateBollingerBands(length: Math.Min(20, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void Atr_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateAverageTrueRange(length: Math.Min(14, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void Stochastic_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateStochasticOscillator(length: Math.Min(14, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void Adx_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateAverageDirectionalIndex(length: Math.Min(14, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void Cci_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateCommodityChannelIndex(length: Math.Min(14, barCount)));
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void WilliamsR_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateWilliamsR(Math.Min(14, barCount)));
        exception.Should().BeNull();
    }

    #endregion

    #region NaN Value Tests

    [Fact]
    public void Sma_WithSufficientData_ShouldNotContainNaN()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var result = stockData.CalculateSimpleMovingAverage(14);
        var values = result.CustomValuesList;

        // After warmup period, values should not be NaN
        for (int i = 14; i < values.Count; i++)
        {
            double.IsNaN(values[i]).Should().BeFalse($"Value at index {i} should not be NaN");
        }
    }

    [Fact]
    public void Ema_WithSufficientData_ShouldNotContainNaN()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var result = stockData.CalculateExponentialMovingAverage(14);
        var values = result.CustomValuesList;

        // After warmup period, values should not be NaN
        for (int i = 14; i < values.Count; i++)
        {
            double.IsNaN(values[i]).Should().BeFalse($"Value at index {i} should not be NaN");
        }
    }

    [Fact]
    public void Rsi_WithSufficientData_ShouldNotContainNaN()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var result = stockData.CalculateRelativeStrengthIndex();
        var values = result.CustomValuesList;

        // After warmup period, values should not be NaN
        for (int i = 20; i < values.Count; i++)
        {
            double.IsNaN(values[i]).Should().BeFalse($"Value at index {i} should not be NaN");
        }
    }

    [Fact]
    public void Macd_WithSufficientData_ShouldNotContainNaN()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var result = stockData.CalculateMovingAverageConvergenceDivergence();
        var values = result.CustomValuesList;

        // After warmup period, values should not be NaN
        for (int i = 35; i < values.Count; i++)
        {
            double.IsNaN(values[i]).Should().BeFalse($"Value at index {i} should not be NaN");
        }
    }

    [Fact]
    public void BollingerBands_WithSufficientData_ShouldNotContainNaN()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var result = stockData.CalculateBollingerBands(length: 20);
        var upper = result.OutputValues["UpperBand"];
        var middle = result.OutputValues["MiddleBand"];
        var lower = result.OutputValues["LowerBand"];

        // After warmup period, values should not be NaN
        for (int i = 25; i < upper.Count; i++)
        {
            double.IsNaN(upper[i]).Should().BeFalse($"Upper band at index {i} should not be NaN");
            double.IsNaN(middle[i]).Should().BeFalse($"Middle band at index {i} should not be NaN");
            double.IsNaN(lower[i]).Should().BeFalse($"Lower band at index {i} should not be NaN");
        }
    }

    [Fact]
    public void Atr_WithSufficientData_ShouldNotContainNaN()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var result = stockData.CalculateAverageTrueRange(length: 14);
        var values = result.CustomValuesList;

        // After warmup period, values should not be NaN
        for (int i = 14; i < values.Count; i++)
        {
            double.IsNaN(values[i]).Should().BeFalse($"Value at index {i} should not be NaN");
        }
    }

    #endregion

    #region Constant Value Tests

    [Fact]
    public void Sma_WithConstantValues_ShouldReturnConstantOutput()
    {
        var data = CreateConstantData(100, price: 50.0);
        var stockData = new StockData(data);

        var result = stockData.CalculateSimpleMovingAverage(14);
        var values = result.CustomValuesList;

        // After warmup, SMA of constant values should equal the constant
        for (int i = 14; i < values.Count; i++)
        {
            values[i].Should().BeApproximately(50.0, 1e-10, $"SMA at index {i} should equal input price");
        }
    }

    [Fact]
    public void Ema_WithConstantValues_ShouldReturnConstantOutput()
    {
        var data = CreateConstantData(100, price: 50.0);
        var stockData = new StockData(data);

        var result = stockData.CalculateExponentialMovingAverage(14);
        var values = result.CustomValuesList;

        // After warmup, EMA of constant values should approach the constant
        for (int i = 30; i < values.Count; i++)
        {
            values[i].Should().BeApproximately(50.0, 1e-6, $"EMA at index {i} should approach input price");
        }
    }

    [Fact]
    public void Rsi_WithConstantValues_ShouldNotThrowOrReturnInfinity()
    {
        var data = CreateConstantData(100, price: 50.0);
        var stockData = new StockData(data);

        var result = stockData.CalculateRelativeStrengthIndex();
        var values = result.CustomValuesList;

        // RSI with no price changes - just check it doesn't return invalid numbers
        for (int i = 20; i < values.Count; i++)
        {
            double.IsInfinity(values[i]).Should().BeFalse($"RSI at index {i} should not be Infinity");
        }
    }

    [Fact]
    public void Atr_WithConstantValues_ShouldApproachZero()
    {
        var data = CreateConstantData(100, price: 50.0);
        var stockData = new StockData(data);

        var result = stockData.CalculateAverageTrueRange(length: 14);
        var values = result.CustomValuesList;

        // ATR with no volatility should be 0 or very close to 0
        for (int i = 20; i < values.Count; i++)
        {
            values[i].Should().BeApproximately(0.0, 1e-6, $"ATR at index {i} should be 0 for constant prices");
        }
    }

    #endregion

    #region Infinity and Edge Value Tests

    [Fact]
    public void CoreIndicators_ShouldNotReturnInfinity()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        // SMA
        var smaResult = stockData.CalculateSimpleMovingAverage(14);
        for (int i = 0; i < smaResult.CustomValuesList.Count; i++)
        {
            double.IsInfinity(smaResult.CustomValuesList[i]).Should().BeFalse($"SMA at index {i} should not be Infinity");
        }

        // EMA
        stockData = new StockData(data);
        var emaResult = stockData.CalculateExponentialMovingAverage(14);
        for (int i = 0; i < emaResult.CustomValuesList.Count; i++)
        {
            double.IsInfinity(emaResult.CustomValuesList[i]).Should().BeFalse($"EMA at index {i} should not be Infinity");
        }

        // RSI
        stockData = new StockData(data);
        var rsiResult = stockData.CalculateRelativeStrengthIndex();
        for (int i = 0; i < rsiResult.CustomValuesList.Count; i++)
        {
            double.IsInfinity(rsiResult.CustomValuesList[i]).Should().BeFalse($"RSI at index {i} should not be Infinity");
        }

        // ATR
        stockData = new StockData(data);
        var atrResult = stockData.CalculateAverageTrueRange(length: 14);
        for (int i = 0; i < atrResult.CustomValuesList.Count; i++)
        {
            double.IsInfinity(atrResult.CustomValuesList[i]).Should().BeFalse($"ATR at index {i} should not be Infinity");
        }
    }

    [Fact]
    public void MovingAverages_ShouldBeWithinPriceRange()
    {
        var data = CreateTestData(100, basePrice: 100.0, volatility: 0.02);
        var stockData = new StockData(data);

        var minPrice = data.Min(d => d.Low);
        var maxPrice = data.Max(d => d.High);

        // SMA
        var smaResult = stockData.CalculateSimpleMovingAverage(14);
        for (int i = 20; i < smaResult.CustomValuesList.Count; i++)
        {
            var val = smaResult.CustomValuesList[i];
            if (!double.IsNaN(val) && val != 0)
            {
                val.Should().BeGreaterThan(minPrice * 0.5, $"SMA at index {i} should be reasonable");
                val.Should().BeLessThan(maxPrice * 2, $"SMA at index {i} should be reasonable");
            }
        }

        // EMA
        stockData = new StockData(data);
        var emaResult = stockData.CalculateExponentialMovingAverage(14);
        for (int i = 20; i < emaResult.CustomValuesList.Count; i++)
        {
            var val = emaResult.CustomValuesList[i];
            if (!double.IsNaN(val) && val != 0)
            {
                val.Should().BeGreaterThan(minPrice * 0.5, $"EMA at index {i} should be reasonable");
                val.Should().BeLessThan(maxPrice * 2, $"EMA at index {i} should be reasonable");
            }
        }
    }

    [Fact]
    public void Oscillators_ShouldBeWithinBounds()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var rsiResult = stockData.CalculateRelativeStrengthIndex();
        stockData = new StockData(data);
        var stochResult = stockData.CalculateStochasticOscillator(length: 14);

        // RSI should be between 0 and 100
        for (int i = 20; i < rsiResult.CustomValuesList.Count; i++)
        {
            var rsi = rsiResult.CustomValuesList[i];
            if (!double.IsNaN(rsi))
            {
                rsi.Should().BeGreaterThanOrEqualTo(0, $"RSI at index {i} should be >= 0");
                rsi.Should().BeLessThanOrEqualTo(100, $"RSI at index {i} should be <= 100");
            }
        }

        // Stochastic should be between 0 and 100
        for (int i = 20; i < stochResult.CustomValuesList.Count; i++)
        {
            var stoch = stochResult.CustomValuesList[i];
            if (!double.IsNaN(stoch))
            {
                stoch.Should().BeGreaterThanOrEqualTo(0, $"Stochastic at index {i} should be >= 0");
                stoch.Should().BeLessThanOrEqualTo(100, $"Stochastic at index {i} should be <= 100");
            }
        }
    }

    #endregion

    #region Single Bar Edge Case

    [Fact]
    public void Indicators_WithSingleBar_ShouldNotThrow()
    {
        var data = CreateTestData(1);
        var stockData = new StockData(data);

        // These should not throw, even with just 1 bar
        var exceptions = new List<Exception>();

        try { new StockData(data).CalculateSimpleMovingAverage(14); } catch (Exception ex) { exceptions.Add(ex); }
        try { new StockData(data).CalculateExponentialMovingAverage(14); } catch (Exception ex) { exceptions.Add(ex); }
        try { new StockData(data).CalculateRelativeStrengthIndex(); } catch (Exception ex) { exceptions.Add(ex); }
        try { new StockData(data).CalculateMovingAverageConvergenceDivergence(); } catch (Exception ex) { exceptions.Add(ex); }

        // None should throw (they may return NaN or 0, but shouldn't crash)
        exceptions.Should().BeEmpty("No indicator should throw with single bar");
    }

    #endregion

    #region Ehlers Indicators Minimum Bars

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void EhlersFisher_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateEhlersFisherTransform());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void EhlersInstantaneousTrendline_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateEhlersInstantaneousTrendlineV1());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void EhlersSuperSmoother_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateEhlersSuperSmootherFilter());
        exception.Should().BeNull();
    }

    #endregion

    #region Volume Indicator Tests

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(14)]
    public void Obv_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateOnBalanceVolume());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(14)]
    public void Adl_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateAccumulationDistributionLine());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(20)]
    public void Mfi_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateMoneyFlowIndex(length: Math.Min(14, barCount)));
        exception.Should().BeNull();
    }

    #endregion

    #region Trend Indicator Tests

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void ParabolicSar_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateParabolicSAR());
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Supertrend_WithMinimumBars_ShouldNotThrow(int barCount)
    {
        var data = CreateTestData(barCount);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateSuperTrend());
        exception.Should().BeNull();
    }

    #endregion

    #region Composite Indicator Tests

    [Fact]
    public void TechnicalRatings_WithSufficientData_ShouldNotThrow()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateTechnicalRatings());
        exception.Should().BeNull();
    }

    [Fact]
    public void InsyncIndex_WithSufficientData_ShouldNotThrow()
    {
        var data = CreateTestData(100);
        var stockData = new StockData(data);

        var exception = Record.Exception(() => stockData.CalculateInsyncIndex());
        exception.Should().BeNull();
    }

    #endregion
}
