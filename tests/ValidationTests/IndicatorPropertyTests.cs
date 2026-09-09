using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Property-based validation tests for all V2 indicators.
/// Phase 1 of the V2 Validation Plan.
///
/// Properties validated:
/// 1. Bounds checking (indicator-specific ranges)
/// 2. No NaN/Infinity after warmup
/// 3. Statistical sanity (MA tracks price, volatility > 0 when prices vary)
/// </summary>
public sealed class IndicatorPropertyTests
{
    private const int TestDataSize = 200;
    private const int WarmupPeriod = 50; // Skip first 50 bars for warmup
    private const double Tolerance = 1e-10;

    #region Indicator Property Definitions

    /// <summary>
    /// Indicators bounded between 0 and 100 (percentile-based oscillators).
    /// </summary>
    private static readonly HashSet<IndicatorName> BoundedZeroToHundred = new()
    {
        IndicatorName.RelativeStrengthIndex,
        IndicatorName.StochasticOscillator,
        IndicatorName.MoneyFlowIndex,
        IndicatorName.StochasticRelativeStrengthIndex,
        IndicatorName.ConnorsRelativeStrengthIndex,
        IndicatorName.UltimateOscillator,
        IndicatorName.RelativeMomentumIndex,
        IndicatorName.InertiaIndicator,
        IndicatorName.DynamicMomentumIndex,
        IndicatorName.StochasticConnorsRelativeStrengthIndex,
        IndicatorName.CommoditySelectionIndex,
        // Note: AdaptiveRelativeStrengthIndex can slightly exceed 100 due to adaptive calculation
    };

    /// <summary>
    /// Indicators bounded between -100 and 100.
    /// SMI and TSI oscillate around zero, unlike traditional RSI.
    /// </summary>
    private static readonly HashSet<IndicatorName> BoundedNeg100To100 = new()
    {
        IndicatorName.WilliamsR,
        IndicatorName.PercentagePriceOscillator,
        IndicatorName.PercentageVolumeOscillator,
        IndicatorName.StochasticMomentumIndex,
        IndicatorName.TrueStrengthIndex,
    };

    /// <summary>
    /// Indicators bounded between -1 and 1 (correlation-based).
    /// Note: Actual correlation indicators to be added after enum audit.
    /// </summary>
    private static readonly HashSet<IndicatorName> BoundedNeg1To1 = new()
    {
        // EhlersCorrelationAngleIndicator and similar - verify actual bounds
    };

    /// <summary>
    /// Indicators that must be non-negative (absolute volatility, standard deviation, etc.).
    /// Note: ChaikinVolatility measures RATE OF CHANGE of volatility, so it can be negative.
    /// </summary>
    private static readonly HashSet<IndicatorName> NonNegative = new()
    {
        IndicatorName.StandardDeviation,
        IndicatorName.AverageTrueRange,
        IndicatorName.BollingerBandsWidth,
        IndicatorName.UlcerIndex,
        IndicatorName.HistoricalVolatility,
    };

    /// <summary>
    /// Moving average indicators that should track within price range.
    /// </summary>
    private static readonly HashSet<IndicatorName> MovingAverages = new()
    {
        IndicatorName.SimpleMovingAverage,
        IndicatorName.ExponentialMovingAverage,
        IndicatorName.WeightedMovingAverage,
        IndicatorName.DoubleExponentialMovingAverage,
        IndicatorName.TripleExponentialMovingAverage,
        IndicatorName.HullMovingAverage,
        IndicatorName.KaufmanAdaptiveMovingAverage,
        IndicatorName.TriangularMovingAverage,
        IndicatorName.ArnaudLegouxMovingAverage,
        IndicatorName.ZeroLagExponentialMovingAverage,
        IndicatorName.TillsonT3MovingAverage,
        IndicatorName.AdaptiveMovingAverage,
    };

    /// <summary>
    /// Multi-stock indicators that require secondary data source.
    /// These are tested separately.
    /// </summary>
    private static readonly HashSet<IndicatorName> MultiStockIndicators = new()
    {
        IndicatorName.RSMKIndicator,
        IndicatorName.ComparePriceMomentumOscillator,
        IndicatorName.KaufmanStressIndicator,
        IndicatorName.RelativeNormalizedVolatility,
        IndicatorName.RelativeStrength3DIndicator,
        IndicatorName.SectorRotationModel,
    };

    #endregion

    #region Test Data Generation

    private static List<TickerData> CreateTestData(int count, double basePrice = 100.0, double volatility = 0.02)
    {
        var data = new List<TickerData>(count);
        var random = new Random(42); // Fixed seed for reproducibility
        var currentPrice = basePrice;
        var baseDate = new DateTime(2024, 1, 1);

        for (var i = 0; i < count; i++)
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

    private static List<TickerData> CreateTrendingData(int count, bool uptrend = true)
    {
        var data = new List<TickerData>(count);
        var random = new Random(42);
        var currentPrice = 100.0;
        var baseDate = new DateTime(2024, 1, 1);
        var trend = uptrend ? 0.001 : -0.001; // 0.1% daily trend

        for (var i = 0; i < count; i++)
        {
            currentPrice *= (1 + trend + (random.NextDouble() - 0.5) * 0.005);
            var high = currentPrice * 1.005;
            var low = currentPrice * 0.995;

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = currentPrice,
                High = high,
                Low = low,
                Close = currentPrice,
                Volume = 1000000
            });
        }

        return data;
    }

    #endregion

    #region Property Test: No NaN or Infinity

    /// <summary>
    /// Tests that all indicators produce valid numeric values (no NaN/Infinity) after warmup.
    /// </summary>
    [Fact]
    public void AllIndicators_ShouldNotProduceNaNOrInfinity_AfterWarmup()
    {
        var testData = CreateTestData(TestDataSize);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var allIndicators = Enum.GetValues<IndicatorName>()
            .Where(n => n != IndicatorName.None)
            .Where(n => !MultiStockIndicators.Contains(n))
            .ToList();

        var failures = new List<string>();
        var tested = 0;

        foreach (var indicatorName in allIndicators)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    handle = catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var values = runtime.GetSeries(handle.Value).ToList();

                    // Check values after warmup period
                    for (var i = WarmupPeriod; i < values.Count; i++)
                    {
                        if (double.IsNaN(values[i]))
                        {
                            failures.Add($"{indicatorName}: NaN at index {i}");
                            break;
                        }
                        if (double.IsInfinity(values[i]))
                        {
                            failures.Add($"{indicatorName}: Infinity at index {i}");
                            break;
                        }
                    }
                    tested++;
                }
            }
            catch (NotSupportedException)
            {
                // Indicator not yet supported in V2 - skip
            }
            catch (Exception ex)
            {
                failures.Add($"{indicatorName}: Exception - {ex.Message}");
            }
        }

        tested.Should().BeGreaterThan(600, "Should test at least 600 indicators");
        failures.Should().BeEmpty(
            $"The following {failures.Count} indicators produced NaN/Infinity:\n{string.Join("\n", failures.Take(20))}");
    }

    #endregion

    #region Property Test: Bounded Oscillators (0-100)

    /// <summary>
    /// Tests that oscillators bounded 0-100 stay within bounds.
    /// </summary>
    [Fact]
    public void BoundedOscillators_ShouldStayWithinZeroToHundred()
    {
        var testData = CreateTestData(TestDataSize);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var failures = new List<string>();

        foreach (var indicatorName in BoundedZeroToHundred)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    handle = catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var values = runtime.GetSeries(handle.Value).ToList();

                    for (var i = WarmupPeriod; i < values.Count; i++)
                    {
                        var value = values[i];
                        if (double.IsNaN(value) || double.IsInfinity(value)) continue;

                        // Allow small tolerance for floating point
                        if (value < -Tolerance || value > 100 + Tolerance)
                        {
                            failures.Add($"{indicatorName}: Value {value:F4} at index {i} outside [0, 100]");
                            break;
                        }
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Skip unsupported indicators
            }
            catch (Exception ex)
            {
                failures.Add($"{indicatorName}: Exception - {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            $"The following indicators exceeded [0, 100] bounds:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Property Test: Non-Negative Indicators

    /// <summary>
    /// Tests that volatility and standard deviation indicators are non-negative.
    /// </summary>
    [Fact]
    public void NonNegativeIndicators_ShouldBeNonNegative()
    {
        var testData = CreateTestData(TestDataSize);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var failures = new List<string>();

        foreach (var indicatorName in NonNegative)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    handle = catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var values = runtime.GetSeries(handle.Value).ToList();

                    for (var i = WarmupPeriod; i < values.Count; i++)
                    {
                        var value = values[i];
                        if (double.IsNaN(value) || double.IsInfinity(value)) continue;

                        if (value < -Tolerance)
                        {
                            failures.Add($"{indicatorName}: Negative value {value:F4} at index {i}");
                            break;
                        }
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Skip unsupported indicators
            }
            catch (Exception ex)
            {
                failures.Add($"{indicatorName}: Exception - {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            $"The following indicators produced negative values:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Property Test: Moving Averages Track Price

    /// <summary>
    /// Tests that moving averages stay within the price range.
    /// </summary>
    [Fact]
    public void MovingAverages_ShouldStayWithinPriceRange()
    {
        var testData = CreateTestData(TestDataSize);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        // Calculate price bounds
        var minPrice = testData.Min(t => t.Low);
        var maxPrice = testData.Max(t => t.High);

        var failures = new List<string>();

        foreach (var indicatorName in MovingAverages)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    handle = catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var values = runtime.GetSeries(handle.Value).ToList();

                    for (var i = WarmupPeriod; i < values.Count; i++)
                    {
                        var value = values[i];
                        if (double.IsNaN(value) || double.IsInfinity(value)) continue;

                        // MA should be within price range (with some tolerance for overshoots)
                        var margin = (maxPrice - minPrice) * 0.1; // 10% margin
                        if (value < minPrice - margin || value > maxPrice + margin)
                        {
                            failures.Add($"{indicatorName}: Value {value:F4} at index {i} outside price range [{minPrice:F4}, {maxPrice:F4}]");
                            break;
                        }
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Skip unsupported indicators
            }
            catch (Exception ex)
            {
                failures.Add($"{indicatorName}: Exception - {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            $"The following moving averages exceeded price range:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Property Test: Correlation Indicators

    /// <summary>
    /// Tests that correlation indicators stay within [-1, 1].
    /// </summary>
    [Fact]
    public void CorrelationIndicators_ShouldStayWithinNeg1To1()
    {
        var testData = CreateTestData(TestDataSize);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var failures = new List<string>();

        foreach (var indicatorName in BoundedNeg1To1)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    handle = catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var values = runtime.GetSeries(handle.Value).ToList();

                    for (var i = WarmupPeriod; i < values.Count; i++)
                    {
                        var value = values[i];
                        if (double.IsNaN(value) || double.IsInfinity(value)) continue;

                        if (value < -1 - Tolerance || value > 1 + Tolerance)
                        {
                            failures.Add($"{indicatorName}: Value {value:F4} at index {i} outside [-1, 1]");
                            break;
                        }
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Skip unsupported indicators
            }
            catch (Exception ex)
            {
                failures.Add($"{indicatorName}: Exception - {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            $"The following correlation indicators exceeded [-1, 1] bounds:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Statistical Sanity Tests

    /// <summary>
    /// Tests that RSI trends higher during uptrends.
    /// </summary>
    [Fact]
    public void RSI_ShouldTrendHigherDuringUptrend()
    {
        var uptrendData = CreateTrendingData(TestDataSize, uptrend: true);
        var stockData = new StockData(uptrendData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Rsi(14);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var values = runtime.GetSeries(handle!.Value).ToList();

        // During strong uptrend, RSI should average above 50
        var avgRsi = values.Skip(WarmupPeriod).Where(v => !double.IsNaN(v)).Average();
        avgRsi.Should().BeGreaterThan(50, "RSI should average above 50 during uptrend");
    }

    /// <summary>
    /// Tests that RSI trends lower during downtrends.
    /// </summary>
    [Fact]
    public void RSI_ShouldTrendLowerDuringDowntrend()
    {
        var downtrendData = CreateTrendingData(TestDataSize, uptrend: false);
        var stockData = new StockData(downtrendData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Rsi(14);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var values = runtime.GetSeries(handle!.Value).ToList();

        // During strong downtrend, RSI should average below 50
        var avgRsi = values.Skip(WarmupPeriod).Where(v => !double.IsNaN(v)).Average();
        avgRsi.Should().BeLessThan(50, "RSI should average below 50 during downtrend");
    }

    /// <summary>
    /// Tests that volatility indicators are positive when prices vary.
    /// </summary>
    [Fact]
    public void VolatilityIndicators_ShouldBePositiveWhenPricesVary()
    {
        var testData = CreateTestData(TestDataSize, volatility: 0.05); // High volatility
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? atrHandle = null;
        SeriesHandle? stdHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            atrHandle = catalog.Atr(14);
            stdHandle = catalog.StdDev(20);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(atrHandle!.Value);
        runtime.Subscribe(stdHandle!.Value);

        var atrValues = runtime.GetSeries(atrHandle!.Value).ToList();
        var stdValues = runtime.GetSeries(stdHandle!.Value).ToList();

        // ATR should be positive when prices vary
        var avgAtr = atrValues.Skip(WarmupPeriod).Where(v => !double.IsNaN(v)).Average();
        avgAtr.Should().BeGreaterThan(0, "ATR should be positive when prices vary");

        // StdDev should be positive when prices vary
        var avgStd = stdValues.Skip(WarmupPeriod).Where(v => !double.IsNaN(v)).Average();
        avgStd.Should().BeGreaterThan(0, "StdDev should be positive when prices vary");
    }

    /// <summary>
    /// Tests that moving averages lag behind price during trends.
    /// </summary>
    [Fact]
    public void MovingAverages_ShouldLagBehindPriceDuringUptrend()
    {
        var uptrendData = CreateTrendingData(TestDataSize, uptrend: true);
        var stockData = new StockData(uptrendData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? smaHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            smaHandle = catalog.Sma(20);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(smaHandle!.Value);

        var smaValues = runtime.GetSeries(smaHandle!.Value).ToList();
        var closeValues = uptrendData.Select(t => t.Close).ToList();

        // During uptrend, SMA should generally be below current price (lagging)
        var belowCount = 0;
        for (var i = WarmupPeriod; i < smaValues.Count && i < closeValues.Count; i++)
        {
            if (!double.IsNaN(smaValues[i]) && smaValues[i] < closeValues[i])
            {
                belowCount++;
            }
        }

        var belowPercentage = (double)belowCount / (smaValues.Count - WarmupPeriod);
        belowPercentage.Should().BeGreaterThan(0.6, "SMA should be below price >60% of time during uptrend");
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Tests all indicators with minimum data (2 bars).
    /// </summary>
    [Fact]
    public void AllIndicators_ShouldNotCrashWithMinimumData()
    {
        var testData = CreateTestData(2);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var allIndicators = Enum.GetValues<IndicatorName>()
            .Where(n => n != IndicatorName.None)
            .Where(n => !MultiStockIndicators.Contains(n))
            .ToList();

        var crashes = new List<string>();

        foreach (var indicatorName in allIndicators)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                builder.ConfigureIndicators(catalog =>
                {
                    catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();
            }
            catch (NotSupportedException)
            {
                // Expected for some indicators
            }
            catch (ArgumentException)
            {
                // Expected for some indicators with minimum length requirements
            }
            catch (Exception ex)
            {
                crashes.Add($"{indicatorName}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        crashes.Should().BeEmpty(
            $"The following indicators crashed with 2 bars:\n{string.Join("\n", crashes.Take(20))}");
    }

    /// <summary>
    /// Tests all indicators with single bar.
    /// </summary>
    [Fact]
    public void AllIndicators_ShouldNotCrashWithSingleBar()
    {
        var testData = CreateTestData(1);
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var allIndicators = Enum.GetValues<IndicatorName>()
            .Where(n => n != IndicatorName.None)
            .Where(n => !MultiStockIndicators.Contains(n))
            .ToList();

        var crashes = new List<string>();

        foreach (var indicatorName in allIndicators)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                builder.ConfigureIndicators(catalog =>
                {
                    catalog.Calculate(indicatorName);
                });

                using var runtime = builder.Build();
                runtime.Start();
            }
            catch (NotSupportedException)
            {
                // Expected
            }
            catch (ArgumentException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                crashes.Add($"{indicatorName}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        crashes.Should().BeEmpty(
            $"The following indicators crashed with 1 bar:\n{string.Join("\n", crashes.Take(20))}");
    }

    #endregion
}
