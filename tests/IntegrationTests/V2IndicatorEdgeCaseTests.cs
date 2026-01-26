using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using System.Reflection;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// Integration tests for all v2 indicators to verify they handle edge cases correctly.
/// Tests include minimum bars, NaN/Infinity checks, and constant data handling.
/// </summary>
public sealed class V2IndicatorEdgeCaseTests
{
    // Methods to exclude from indicator testing (utility methods, not indicators)
    private static readonly HashSet<string> ExcludedMethods = new(StringComparer.Ordinal)
    {
        // Utility methods
        "Price", "High", "Low", "Volume", "Open", "Close", "Typical",
        "Formula", "Calculate", "StdDev", "GetType", "ToString", "Equals", "GetHashCode",
        // Methods with naming issues
        "LCLeastSquaresMovingAverage1", "HMA3", "MovingAverageConvergenceDivergence4",
        "PercentagePriceOscillator4",
        // Multi-stock comparison indicators that require a secondary StockData parameter
        // (catalog generation doesn't support these properly)
        "ComparePriceMomentumOscillator", "KaufmanStressIndicator", "RelativeNormalizedVolatility",
        "RelativeStrength3DIndicator", "RSMKIndicator", "SectorRotationModel"
    };

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

    /// <summary>
    /// Gets all indicator names from the IndicatorName enum (excluding None).
    /// </summary>
    private static IEnumerable<IndicatorName> GetAllIndicatorNames()
    {
        return Enum.GetValues<IndicatorName>().Where(x => x != IndicatorName.None);
    }

    /// <summary>
    /// Gets all indicator catalog methods using reflection.
    /// </summary>
    private static IEnumerable<MethodInfo> GetIndicatorMethods()
    {
        return typeof(IndicatorCatalog)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.ReturnType == typeof(SeriesHandle) &&
                        !ExcludedMethods.Contains(m.Name));
    }

    /// <summary>
    /// Checks if an error is a "not supported" or parameter mismatch error that should be ignored.
    /// </summary>
    private static bool IsExpectedConfigurationError(string message)
    {
        return message.Contains("is not supported") ||
               message.Contains("Too many parameters") ||
               message.Contains("doesn't have a single output") ||
               message.Contains("Unknown series handle") ||
               message.Contains("Expected at most");
    }

    #region Core V2 Indicator Tests

    /// <summary>
    /// Tests a representative set of v2 indicators with minimum bars.
    /// This is more efficient than testing all 759 indicators via Theory.
    /// </summary>
    [Fact]
    public void CoreV2Indicators_WithMinimumBars_ShouldWork()
    {
        // Test representative indicators from different categories that exist in the catalog
        var coreIndicators = new[]
        {
            // Trend indicators
            "AdaptiveMovingAverage",
            "ArnaudLegouxMovingAverage",
            "TriangularMovingAverage",
            // Momentum indicators
            "RelativeMomentumIndex",
            "AbsolutePriceOscillator",
            "ChaikinOscillator",
            // Volatility indicators
            "BollingerBandsWidth",
            "ChaikinVolatility",
            // Volume indicators
            "AccumulationDistributionLine",
            "ChaikinMoneyFlow",
            // Oscillators
            "StochasticFastOscillator",
            "CommoditySelectionIndex",
            "DirectionalTrendIndex"
        };

        var data = CreateTestData(20);
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);
        var failures = new List<string>();

        foreach (var indicatorName in coreIndicators)
        {
            try
            {
                var method = typeof(IndicatorCatalog).GetMethod(indicatorName);
                if (method == null)
                {
                    failures.Add($"{indicatorName}: Method not found");
                    continue;
                }

                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var buffer = runtime.GetSeries(handle.Value);
                    // Some indicators may have warmup periods that result in fewer values
                    // Just check we got some output without crashing
                    if (buffer.Count == 0)
                    {
                        failures.Add($"{indicatorName}: No output values produced");
                    }
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                if (IsExpectedConfigurationError(innerMessage))
                {
                    continue;
                }
                failures.Add($"{indicatorName}: {innerMessage}");
            }
        }

        failures.Should().BeEmpty($"Core indicators should work:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Batch All Indicators Test

    [Fact]
    public void AllV2Indicators_WithMinimumBars_ShouldNotCrash()
    {
        // This test runs all v2 indicators with 2 bars to verify none crash
        var data = CreateTestData(2);
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);
        var methods = GetIndicatorMethods().ToList();
        var failures = new List<string>();
        var skipped = 0;

        foreach (var method in methods)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            args[i] = 2; // Minimum length
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            args[i] = 2.0;
                        }
                        else if (parameters[i].ParameterType == typeof(SeriesHandle?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(IndicatorKey?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(MovingAvgType))
                        {
                            args[i] = MovingAvgType.ExponentialMovingAverage;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    _ = runtime.GetSeries(handle.Value);
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                // Skip expected configuration errors (indicator not supported, parameter mismatch, etc.)
                if (IsExpectedConfigurationError(innerMessage))
                {
                    skipped++;
                    continue;
                }
                failures.Add($"{method.Name}: {innerMessage}");
            }
        }

        // Report skipped count for visibility
        skipped.Should().BeLessThan(methods.Count, "Not all indicators should be skipped");
        failures.Should().BeEmpty($"The following indicators crashed with 2 bars:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void AllV2Indicators_With100Bars_ShouldProduceValidValues()
    {
        // This test runs all v2 indicators with 100 bars and verifies no NaN/Infinity after warmup
        var data = CreateTestData(100);
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);
        var methods = GetIndicatorMethods().ToList();
        var failures = new List<string>();
        var skipped = 0;
        var successful = 0;

        foreach (var method in methods)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            args[i] = 14;
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            args[i] = 2.0;
                        }
                        else if (parameters[i].ParameterType == typeof(SeriesHandle?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(IndicatorKey?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(MovingAvgType))
                        {
                            args[i] = MovingAvgType.ExponentialMovingAverage;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var buffer = runtime.GetSeries(handle.Value);
                    var values = buffer.ToList();

                    // Check for NaN/Infinity after warmup (last 50 bars)
                    var hasInvalidValues = false;
                    for (int i = 50; i < values.Count; i++)
                    {
                        if (double.IsNaN(values[i]))
                        {
                            failures.Add($"{method.Name}: NaN at index {i}");
                            hasInvalidValues = true;
                            break;
                        }
                        if (double.IsInfinity(values[i]))
                        {
                            failures.Add($"{method.Name}: Infinity at index {i}");
                            hasInvalidValues = true;
                            break;
                        }
                    }
                    if (!hasInvalidValues)
                    {
                        successful++;
                    }
                }
                else
                {
                    successful++; // No handle means no values to check
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                // Skip expected configuration errors
                if (IsExpectedConfigurationError(innerMessage))
                {
                    skipped++;
                    continue;
                }
                failures.Add($"{method.Name}: Exception - {innerMessage}");
            }
        }

        // Should have tested at least some indicators successfully
        successful.Should().BeGreaterThan(0, "At least some indicators should work");
        failures.Should().BeEmpty($"The following indicators produced invalid values:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Specific Edge Cases

    [Fact]
    public void V2Indicators_WithSingleBar_ShouldNotThrow()
    {
        // Extreme edge case: single bar
        var data = CreateTestData(1);
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);
        var methods = GetIndicatorMethods().Take(50).ToList(); // Test first 50 indicators
        var failures = new List<string>();

        foreach (var method in methods)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            args[i] = 1;
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            args[i] = 2.0;
                        }
                        else if (parameters[i].ParameterType == typeof(SeriesHandle?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(IndicatorKey?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(MovingAvgType))
                        {
                            args[i] = MovingAvgType.ExponentialMovingAverage;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    _ = runtime.GetSeries(handle.Value);
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                if (IsExpectedConfigurationError(innerMessage))
                {
                    continue;
                }
                failures.Add($"{method.Name}: {innerMessage}");
            }
        }

        failures.Should().BeEmpty($"The following indicators crashed with 1 bar:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void V2Indicators_WithZeroVolume_ShouldNotCrash()
    {
        // Edge case: zero volume
        var data = CreateConstantData(100, price: 100, volume: 0);
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);

        // Test volume-based indicators that exist in the catalog
        var volumeIndicators = new[]
        {
            "AccumulationDistributionLine",
            "ChaikinMoneyFlow",
            "ChaikinOscillator",
            "AverageMoneyFlowOscillator",
            "RelativeVolumeIndicator"
        };

        var failures = new List<string>();

        foreach (var indicatorName in volumeIndicators)
        {
            try
            {
                var method = typeof(IndicatorCatalog).GetMethod(indicatorName);
                if (method == null) continue;

                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    _ = runtime.GetSeries(handle.Value);
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                if (IsExpectedConfigurationError(innerMessage))
                {
                    continue;
                }
                failures.Add($"{indicatorName}: {innerMessage}");
            }
        }

        failures.Should().BeEmpty($"The following volume indicators crashed with zero volume:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void V2Indicators_WithVeryLargeValues_ShouldNotOverflow()
    {
        // Edge case: very large price values
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var price = 1e10 + random.NextDouble() * 1e9;
            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = price,
                High = price * 1.01,
                Low = price * 0.99,
                Close = price,
                Volume = 1e9
            });
        }

        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);
        var methods = GetIndicatorMethods().Take(50).ToList();
        var failures = new List<string>();

        foreach (var method in methods)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            args[i] = 14;
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            args[i] = 2.0;
                        }
                        else if (parameters[i].ParameterType == typeof(SeriesHandle?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(IndicatorKey?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(MovingAvgType))
                        {
                            args[i] = MovingAvgType.ExponentialMovingAverage;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var buffer = runtime.GetSeries(handle.Value);
                    var values = buffer.ToList();

                    // Check for overflow (Infinity) in last 50 bars
                    for (int i = 50; i < values.Count; i++)
                    {
                        if (double.IsInfinity(values[i]))
                        {
                            failures.Add($"{method.Name}: Overflow (Infinity) at index {i}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                if (IsExpectedConfigurationError(innerMessage))
                {
                    continue;
                }
                failures.Add($"{method.Name}: {innerMessage}");
            }
        }

        failures.Should().BeEmpty($"The following indicators overflowed with large values:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void V2Indicators_WithVerySmallValues_ShouldNotUnderflow()
    {
        // Edge case: very small price values (near epsilon)
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var price = 1e-6 + random.NextDouble() * 1e-7;
            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = price,
                High = price * 1.01,
                Low = price * 0.99,
                Close = price,
                Volume = 1e6
            });
        }

        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);
        var methods = GetIndicatorMethods().Take(50).ToList();
        var failures = new List<string>();

        foreach (var method in methods)
        {
            try
            {
                var builder = new StockIndicatorBuilder(source);
                SeriesHandle? handle = null;

                builder.ConfigureIndicators(catalog =>
                {
                    var parameters = method.GetParameters();
                    var args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].HasDefaultValue)
                        {
                            args[i] = parameters[i].DefaultValue;
                        }
                        else if (parameters[i].ParameterType == typeof(int))
                        {
                            args[i] = 14;
                        }
                        else if (parameters[i].ParameterType == typeof(double))
                        {
                            args[i] = 2.0;
                        }
                        else if (parameters[i].ParameterType == typeof(SeriesHandle?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(IndicatorKey?))
                        {
                            args[i] = null;
                        }
                        else if (parameters[i].ParameterType == typeof(MovingAvgType))
                        {
                            args[i] = MovingAvgType.ExponentialMovingAverage;
                        }
                    }

                    handle = method.Invoke(catalog, args) as SeriesHandle?;
                });

                using var runtime = builder.Build();
                runtime.Start();

                if (handle.HasValue)
                {
                    runtime.Subscribe(handle.Value);
                    var buffer = runtime.GetSeries(handle.Value);
                    var values = buffer.ToList();

                    // Check for NaN (likely from division by tiny number) in last 50 bars
                    int nanCount = 0;
                    for (int i = 50; i < values.Count; i++)
                    {
                        if (double.IsNaN(values[i]))
                        {
                            nanCount++;
                        }
                    }

                    if (nanCount > 10) // Allow some NaN but not majority
                    {
                        failures.Add($"{method.Name}: {nanCount} NaN values in last {values.Count - 50} bars");
                    }
                }
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                if (IsExpectedConfigurationError(innerMessage))
                {
                    continue;
                }
                failures.Add($"{method.Name}: {innerMessage}");
            }
        }

        failures.Should().BeEmpty($"The following indicators had issues with small values:\n{string.Join("\n", failures)}");
    }

    #endregion

    #region Count Tests

    [Fact]
    public void IndicatorCatalog_ShouldHaveExpectedNumberOfMethods()
    {
        // Verify we're testing a reasonable number of indicators
        var methods = GetIndicatorMethods().ToList();

        // Should have many indicator methods (759 from IndicatorName enum)
        methods.Count.Should().BeGreaterThan(500,
            "IndicatorCatalog should have many indicator methods");
    }

    [Fact]
    public void IndicatorName_Enum_ShouldMatchCatalogMethods()
    {
        // Verify enum and catalog are in sync
        var enumCount = GetAllIndicatorNames().Count();
        var methodCount = GetIndicatorMethods().Count();

        // They should be approximately equal (some enum values may not have methods yet)
        Math.Abs(enumCount - methodCount).Should().BeLessThan(50,
            $"IndicatorName enum ({enumCount}) and catalog methods ({methodCount}) should be in sync");
    }

    #endregion
}
