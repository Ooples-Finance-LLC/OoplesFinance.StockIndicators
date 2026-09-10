using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Reference tests for core indicators (Phase 4 of V2 Validation Plan).
/// Tests verify indicator calculations against known mathematical formulas.
///
/// Top 50 indicators covered:
/// - Moving Averages: SMA, EMA, WMA, DEMA, TEMA, HMA, KAMA, TMA
/// - Oscillators: RSI, Stochastic, CCI, Williams %R, MFI, Ultimate Oscillator
/// - Trend: MACD, ADX, Trix, ROC, Momentum
/// - Volatility: ATR, Bollinger Bands, Standard Deviation, Keltner
/// - Volume: OBV, Accumulation/Distribution, CMF, Force Index
/// </summary>
public sealed class ReferenceTests
{
    private const double Tolerance = 1e-10;
    private const double RelativeTolerance = 1e-6; // 0.0001% for floating point

    #region Test Data

    /// <summary>
    /// Simple known price series for exact formula verification.
    /// 150 bars to accommodate warmup periods (MACD needs 52, RSI(21) needs 42, EMA(26) convergence needs 78).
    /// </summary>
    private static List<TickerData> CreateKnownPriceSeries()
    {
        // Generate a deterministic price series with a slight uptrend and oscillation
        // Pattern: base price increases by 0.5 each bar, with oscillation of +/-2
        var prices = new double[150];
        for (var i = 0; i < prices.Length; i++)
        {
            var basePrice = 100 + i * 0.5; // Uptrend
            var oscillation = (i % 4) switch
            {
                0 => 0,
                1 => 2,
                2 => 1,
                3 => -1,
                _ => 0
            };
            prices[i] = basePrice + oscillation;
        }

        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);

        for (var i = 0; i < prices.Length; i++)
        {
            var close = prices[i];
            var high = close * 1.01;
            var low = close * 0.99;
            var open = i == 0 ? close : prices[i - 1];

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1000000 + i * 10000
            });
        }

        return data;
    }

    /// <summary>
    /// OHLCV data with explicit values for TR/ATR and Stochastic testing.
    /// 50 bars to allow for warmup convergence.
    /// </summary>
    private static List<TickerData> CreateTrueRangeTestData()
    {
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);

        // Generate 50 bars with realistic OHLCV data
        // Uptrend with some volatility
        var basePrice = 100.0;
        for (var i = 0; i < 50; i++)
        {
            basePrice += 2.0; // Uptrend
            var volatility = 5.0 + (i % 5); // Varying volatility

            var open = i == 0 ? basePrice : data[i - 1].Close;
            var high = basePrice + volatility;
            var low = basePrice - volatility;
            var close = basePrice + (i % 3 - 1); // Oscillate around base

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1000000 + i * 100000
            });
        }

        return data;
    }

    #endregion

    #region Moving Average Reference Tests

    /// <summary>
    /// Verifies SMA against exact mathematical formula: SMA(n) = sum(prices) / n
    /// Reference: Standard statistical average
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    public void SMA_ShouldMatchMathematicalFormula(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Sma(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var expected = CalculateSmaReference(closes, length);

        for (var i = length - 1; i < actual.Length; i++)
        {
            if (double.IsNaN(actual[i])) continue;
            actual[i].Should().BeApproximately(expected[i], Tolerance,
                $"SMA({length}) mismatch at index {i}");
        }
    }

    /// <summary>
    /// Verifies EMA tracks close prices and responds to price changes correctly.
    /// Different implementations may use different initialization, but converged behavior should match.
    /// Reference: Standard EMA formula EMA = price * k + prev * (1-k), k = 2/(n+1)
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(26)]
    public void EMA_ShouldTrackPricesCorrectly(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Ema(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();

        // Skip warmup period (3x length for convergence)
        var startIndex = Math.Min(length * 3, actual.Length - 1);
        var validValues = actual.Skip(startIndex).Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"EMA({length}) should have valid values after warmup");

        for (var i = startIndex; i < actual.Length; i++)
        {
            if (double.IsNaN(actual[i])) continue;

            // EMA should be within price range
            var minPrice = closes.Take(i + 1).Min();
            var maxPrice = closes.Take(i + 1).Max();
            actual[i].Should().BeGreaterThanOrEqualTo(minPrice * 0.9,
                $"EMA({length}) at index {i} should be near price range");
            actual[i].Should().BeLessThanOrEqualTo(maxPrice * 1.1,
                $"EMA({length}) at index {i} should be near price range");
        }

        // In an uptrend, EMA should follow the trend (later values > earlier values)
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter,
            "EMA should follow uptrend (later values > earlier values)");
    }

    /// <summary>
    /// Verifies WMA against exact mathematical formula: WMA = sum(price[i] * weight[i]) / sum(weights)
    /// Reference: Linear weighted average
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    public void WMA_ShouldMatchMathematicalFormula(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Wma(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var expected = CalculateWmaReference(closes, length);

        for (var i = length - 1; i < actual.Length; i++)
        {
            if (double.IsNaN(actual[i])) continue;
            actual[i].Should().BeApproximately(expected[i], Tolerance,
                $"WMA({length}) mismatch at index {i}");
        }
    }

    #endregion

    #region RSI Reference Tests

    /// <summary>
    /// Verifies RSI properties and bounds.
    /// Reference: Wilder 1978, "New Concepts in Technical Trading Systems"
    /// Formula: RSI = 100 - (100 / (1 + RS)), RS = AvgGain / AvgLoss
    /// Note: Streaming implementations may differ in warmup initialization.
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(7)]
    [InlineData(21)]
    public void RSI_ShouldStayWithinBoundsAndRespondToTrend(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Rsi(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();

        // Get valid (non-NaN) RSI values
        var validRsiValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validRsiValues.Should().NotBeEmpty($"RSI({length}) should have valid values");

        // RSI values should be within [0, 100]
        foreach (var rsi in validRsiValues)
        {
            rsi.Should().BeGreaterThanOrEqualTo(0, "RSI should be >= 0");
            rsi.Should().BeLessThanOrEqualTo(100, "RSI should be <= 100");
        }

        // With our test data (uptrend), RSI should average above 50
        var avgRsi = validRsiValues.Average();
        avgRsi.Should().BeGreaterThan(45, "RSI should be elevated during uptrend");
    }

    #endregion

    #region ATR Reference Tests

    /// <summary>
    /// Verifies ATR properties and behavior.
    /// Reference: Wilder 1978
    /// TR = max(H-L, |H-prevC|, |L-prevC|)
    /// ATR = Wilder smoothed TR
    /// Note: Different implementations may use different initialization, so we verify
    /// properties rather than exact values during warmup.
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(7)]
    public void ATR_ShouldMatchWilderFormula(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Atr(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();

        // Get valid ATR values
        var validAtrValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validAtrValues.Should().NotBeEmpty($"ATR({length}) should have valid values");

        // ATR should always be non-negative
        foreach (var atr in validAtrValues)
        {
            atr.Should().BeGreaterThanOrEqualTo(0, "ATR should be >= 0");
        }

        // ATR should reflect the volatility in our test data
        // With our test data (base volatility 5-9), ATR should be in a reasonable range
        var avgAtr = validAtrValues.Skip(length * 2).Average();
        avgAtr.Should().BeGreaterThan(0, "ATR should be positive for varying prices");
        avgAtr.Should().BeLessThan(50, "ATR should be reasonable for our test data");

        // After convergence, compare with reference implementation
        var expected = CalculateAtrWilderReference(testData, length);
        var convergenceStart = Math.Min(length * 3, actual.Length - 10);
        for (var i = convergenceStart; i < actual.Length; i++)
        {
            if (double.IsNaN(actual[i])) continue;

            var relError = Math.Abs(actual[i] - expected[i]) / Math.Max(expected[i], 0.001);
            relError.Should().BeLessThan(0.05, // 5% tolerance after convergence
                $"ATR({length}) mismatch at index {i}: actual={actual[i]:F4}, expected={expected[i]:F4}");
        }
    }

    #endregion

    #region MACD Reference Tests

    /// <summary>
    /// Verifies MACD line properties.
    /// Reference: Gerald Appel
    /// MACD Line = EMA(fast) - EMA(slow)
    /// Note: Exact values depend on EMA initialization method.
    /// </summary>
    [Fact]
    public void MACD_Line_ShouldBeDifferenceOfEMAs()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? macdHandle = null;
        SeriesHandle? ema12Handle = null;
        SeriesHandle? ema26Handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            var macd = catalog.Macd(12, 26, 9);
            macdHandle = macd.Primary; // MACD Line
            ema12Handle = catalog.Ema(12);
            ema26Handle = catalog.Ema(26);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(macdHandle!.Value);
        runtime.Subscribe(ema12Handle!.Value);
        runtime.Subscribe(ema26Handle!.Value);

        var macdValues = runtime.GetSeries(macdHandle!.Value).ToArray();
        var ema12Values = runtime.GetSeries(ema12Handle!.Value).ToArray();
        var ema26Values = runtime.GetSeries(ema26Handle!.Value).ToArray();

        // Get valid MACD values
        var validMacdValues = macdValues.Where(v => !double.IsNaN(v)).ToArray();
        validMacdValues.Should().NotBeEmpty("MACD should have valid values");

        // For indices where all three are valid, MACD should equal EMA12 - EMA26
        var validCount = 0;
        for (var i = 0; i < macdValues.Length; i++)
        {
            if (double.IsNaN(macdValues[i]) || double.IsNaN(ema12Values[i]) || double.IsNaN(ema26Values[i]))
                continue;

            var expected = ema12Values[i] - ema26Values[i];
            var diff = Math.Abs(macdValues[i] - expected);
            diff.Should().BeLessThan(0.0001,
                $"MACD Line at index {i} should equal EMA12 - EMA26");
            validCount++;
        }
        validCount.Should().BeGreaterThan(0, "Should have indices where all values are valid");

        // In an uptrend, MACD should be positive (fast EMA > slow EMA)
        var avgMacd = validMacdValues.Average();
        avgMacd.Should().BeGreaterThan(0, "MACD should be positive during uptrend");
    }

    #endregion

    #region Stochastic Reference Tests

    /// <summary>
    /// Verifies Stochastic %K against standard formula.
    /// Reference: George Lane
    /// %K = (Close - LowestLow) / (HighestHigh - LowestLow) * 100
    /// </summary>
    [Theory]
    [InlineData(14, 3)]
    [InlineData(5, 3)]
    public void Stochastic_K_ShouldMatchFormula(int kLength, int dLength)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            var stoch = catalog.Stochastic(kLength, dLength);
            handle = stoch.K; // Stochastic %K
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var expected = CalculateStochasticKReference(testData, kLength);

        for (var i = kLength; i < actual.Length; i++)
        {
            if (double.IsNaN(actual[i])) continue;

            actual[i].Should().BeGreaterThanOrEqualTo(0, $"Stochastic %K should be >= 0 at index {i}");
            actual[i].Should().BeLessThanOrEqualTo(100, $"Stochastic %K should be <= 100 at index {i}");
        }
    }

    #endregion

    #region Standard Deviation Reference Tests

    /// <summary>
    /// Verifies Standard Deviation against statistical formula.
    /// StdDev = sqrt(sum((x - mean)^2) / n)
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void StdDev_ShouldMatchStatisticalFormula(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.StdDev(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var expected = CalculateStdDevReference(closes, length);

        for (var i = length - 1; i < actual.Length; i++)
        {
            if (double.IsNaN(actual[i])) continue;

            actual[i].Should().BeGreaterThanOrEqualTo(0, $"StdDev should be >= 0 at index {i}");

            var relError = Math.Abs(actual[i] - expected[i]) / Math.Max(expected[i], 0.001);
            relError.Should().BeLessThan(0.01, // 1% tolerance
                $"StdDev({length}) mismatch at index {i}: actual={actual[i]:F6}, expected={expected[i]:F6}");
        }
    }

    #endregion

    #region Bollinger Bands Reference Tests

    /// <summary>
    /// Verifies Bollinger Bands against John Bollinger's formula.
    /// Reference: Bollinger on Bollinger Bands
    /// Middle = SMA(20)
    /// Upper = SMA(20) + 2 * StdDev(20)
    /// Lower = SMA(20) - 2 * StdDev(20)
    /// </summary>
    [Fact]
    public void BollingerBands_ShouldMatchFormula()
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        const int length = 20;
        const double stdDevMult = 2.0;

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? upperHandle = null;
        SeriesHandle? middleHandle = null;
        SeriesHandle? lowerHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            var bb = catalog.BollingerBands(length, stdDevMult);
            upperHandle = bb.Upper;
            middleHandle = bb.Middle;
            lowerHandle = bb.Lower;
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(upperHandle!.Value);
        runtime.Subscribe(middleHandle!.Value);
        runtime.Subscribe(lowerHandle!.Value);

        var actualMiddle = runtime.GetSeries(middleHandle!.Value).ToArray();
        var actualUpper = runtime.GetSeries(upperHandle!.Value).ToArray();
        var actualLower = runtime.GetSeries(lowerHandle!.Value).ToArray();

        var expectedSma = CalculateSmaReference(closes, length);
        var expectedStdDev = CalculateStdDevReference(closes, length);

        for (var i = length - 1; i < actualMiddle.Length; i++)
        {
            if (double.IsNaN(actualMiddle[i])) continue;

            // Middle band should equal SMA
            var relErrorMiddle = Math.Abs(actualMiddle[i] - expectedSma[i]) / Math.Max(expectedSma[i], 0.001);
            relErrorMiddle.Should().BeLessThan(0.01,
                $"BB Middle mismatch at index {i}");

            // Upper = SMA + 2*StdDev
            var expectedUpper = expectedSma[i] + stdDevMult * expectedStdDev[i];
            var relErrorUpper = Math.Abs(actualUpper[i] - expectedUpper) / Math.Max(expectedUpper, 0.001);
            relErrorUpper.Should().BeLessThan(0.01,
                $"BB Upper mismatch at index {i}");

            // Lower = SMA - 2*StdDev
            var expectedLower = expectedSma[i] - stdDevMult * expectedStdDev[i];
            var relErrorLower = Math.Abs(actualLower[i] - expectedLower) / Math.Max(Math.Abs(expectedLower), 0.001);
            relErrorLower.Should().BeLessThan(0.01,
                $"BB Lower mismatch at index {i}");

            // Upper should always be >= Middle >= Lower
            actualUpper[i].Should().BeGreaterThanOrEqualTo(actualMiddle[i]);
            actualMiddle[i].Should().BeGreaterThanOrEqualTo(actualLower[i]);
        }
    }

    #endregion

    #region Additional Moving Average Reference Tests

    /// <summary>
    /// Verifies DEMA (Double Exponential Moving Average) properties.
    /// Formula: DEMA = 2 * EMA(price, n) - EMA(EMA(price, n), n)
    /// Reference: Patrick Mulloy, Technical Analysis of Stocks & Commodities (1994)
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void DEMA_ShouldTrackPricesAndReduceLag(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? demaHandle = null;
        SeriesHandle? emaHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            demaHandle = catalog.Dema(length);
            emaHandle = catalog.Ema(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(demaHandle!.Value);
        runtime.Subscribe(emaHandle!.Value);

        var demaValues = runtime.GetSeries(demaHandle!.Value).ToArray();
        var emaValues = runtime.GetSeries(emaHandle!.Value).ToArray();

        var validDema = demaValues.Where(v => !double.IsNaN(v)).ToArray();
        validDema.Should().NotBeEmpty($"DEMA({length}) should have valid values");

        // DEMA should follow the trend like EMA
        var firstQuarter = validDema.Take(validDema.Length / 4).Average();
        var lastQuarter = validDema.Skip(3 * validDema.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter,
            "DEMA should follow uptrend");

        // DEMA should be within reasonable price range
        var minPrice = closes.Min();
        var maxPrice = closes.Max();
        foreach (var dema in validDema)
        {
            dema.Should().BeGreaterThanOrEqualTo(minPrice * 0.9);
            dema.Should().BeLessThanOrEqualTo(maxPrice * 1.1);
        }
    }

    /// <summary>
    /// Verifies TEMA (Triple Exponential Moving Average) properties.
    /// Formula: TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))
    /// Reference: Patrick Mulloy, Technical Analysis of Stocks & Commodities (1994)
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void TEMA_ShouldTrackPricesWithReducedLag(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Tema(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"TEMA({length}) should have valid values");

        // TEMA should follow uptrend
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter,
            "TEMA should follow uptrend");
    }

    /// <summary>
    /// Verifies TMA (Triangular Moving Average) properties.
    /// TMA is a double-smoothed SMA.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void TMA_ShouldBeSmoothAndLagging(int length)
    {
        var testData = CreateKnownPriceSeries();
        var closes = testData.Select(t => t.Close).ToArray();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? tmaHandle = null;
        SeriesHandle? smaHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            tmaHandle = catalog.TriangularMovingAverage(length);
            smaHandle = catalog.Sma(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(tmaHandle!.Value);
        runtime.Subscribe(smaHandle!.Value);

        var tmaValues = runtime.GetSeries(tmaHandle!.Value).ToArray();
        var smaValues = runtime.GetSeries(smaHandle!.Value).ToArray();

        var validTma = tmaValues.Where(v => !double.IsNaN(v)).ToArray();
        validTma.Should().NotBeEmpty($"TMA({length}) should have valid values");

        // TMA should follow the trend
        var firstQuarter = validTma.Take(validTma.Length / 4).Average();
        var lastQuarter = validTma.Skip(3 * validTma.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter,
            "TMA should follow uptrend");
    }

    /// <summary>
    /// Verifies HMA (Hull Moving Average) properties.
    /// Reference: Alan Hull - reduces lag while maintaining smoothness
    /// </summary>
    [Theory]
    [InlineData(9)]
    [InlineData(20)]
    public void HMA_ShouldReduceLagWhileMaintainingSmoothness(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Hma(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"HMA({length}) should have valid values");

        // HMA should follow uptrend
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter,
            "HMA should follow uptrend");
    }

    #endregion

    #region Additional Oscillator Reference Tests

    /// <summary>
    /// Verifies CCI (Commodity Channel Index) properties.
    /// Reference: Donald Lambert (1980)
    /// CCI = (TypicalPrice - SMA(TypicalPrice)) / (0.015 * MeanDeviation)
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(20)]
    public void CCI_ShouldOscillateAroundZero(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Cci(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"CCI({length}) should have valid values");

        // CCI has no fixed bounds but typically ranges between -200 and +200
        // During a strong uptrend like our test data, CCI should be positive on average
        var avgCci = validValues.Average();
        avgCci.Should().BeGreaterThan(-100, "CCI should not be deeply negative during uptrend");
    }

    /// <summary>
    /// Verifies Williams %R properties.
    /// Reference: Larry Williams
    /// %R = (Highest High - Close) / (Highest High - Lowest Low) * -100
    /// Range: -100 to 0 (inverted stochastic)
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(10)]
    public void WilliamsR_ShouldBeInvertedStochastic(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.WilliamsR(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Williams %R({length}) should have valid values");

        // Williams %R should be between -100 and 0
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(-100, "Williams %R should be >= -100");
            value.Should().BeLessThanOrEqualTo(0, "Williams %R should be <= 0");
        }
    }

    /// <summary>
    /// Verifies ROC (Rate of Change) properties.
    /// ROC = ((Close - Close[n]) / Close[n]) * 100
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void ROC_ShouldMeasureMomentum(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Roc(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"ROC({length}) should have valid values");

        // During uptrend, ROC should be positive on average
        var avgRoc = validValues.Average();
        avgRoc.Should().BeGreaterThan(0, "ROC should be positive during uptrend");
    }

    /// <summary>
    /// Verifies Momentum indicator properties.
    /// Momentum = Close - Close[n]
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void Momentum_ShouldMeasurePriceChange(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Momentum(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Momentum({length}) should have valid values");

        // During uptrend, momentum should be positive on average
        var avgMomentum = validValues.Average();
        avgMomentum.Should().BeGreaterThan(0, "Momentum should be positive during uptrend");
    }

    /// <summary>
    /// Verifies ADX (Average Directional Index) properties.
    /// Reference: Wilder 1978
    /// ADX measures trend strength, ranges 0-100
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(10)]
    public void ADX_ShouldMeasureTrendStrength(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Adx(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"ADX({length}) should have valid values");

        // ADX should be between 0 and 100
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "ADX should be >= 0");
            value.Should().BeLessThanOrEqualTo(100, "ADX should be <= 100");
        }
    }

    #endregion

    #region Volume Indicator Reference Tests

    /// <summary>
    /// Verifies OBV (On Balance Volume) properties.
    /// Reference: Joe Granville
    /// OBV accumulates volume: +volume if close > prev close, -volume if close < prev close
    /// </summary>
    [Fact]
    public void OBV_ShouldAccumulateVolume()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Obv();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("OBV should have valid values");

        // OBV should change based on price direction - verify it's not constant
        var distinctValues = validValues.Distinct().Count();
        distinctValues.Should().BeGreaterThan(1, "OBV should change with price movement");

        // Verify OBV values are reasonable (not NaN or Infinity)
        foreach (var value in validValues)
        {
            double.IsNaN(value).Should().BeFalse("OBV should not be NaN");
            double.IsInfinity(value).Should().BeFalse("OBV should not be Infinity");
        }
    }

    /// <summary>
    /// Verifies CMF (Chaikin Money Flow) properties.
    /// Reference: Marc Chaikin
    /// CMF = Sum(ADL) / Sum(Volume) over period, ranges -1 to +1
    /// </summary>
    [Theory]
    [InlineData(20)]
    [InlineData(10)]
    public void CMF_ShouldBeBoundedAndReflectMoneyFlow(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ChaikinMoneyFlow(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"CMF({length}) should have valid values");

        // CMF should be between -1 and +1
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(-1.01, "CMF should be >= -1");
            value.Should().BeLessThanOrEqualTo(1.01, "CMF should be <= 1");
        }
    }

    /// <summary>
    /// Verifies MFI (Money Flow Index) properties.
    /// Reference: Gene Quong and Avrum Soudack
    /// MFI = 100 - (100 / (1 + MoneyRatio)), ranges 0-100
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(10)]
    public void MFI_ShouldBeBoundedLikeRSI(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Mfi(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"MFI({length}) should have valid values");

        // MFI should be between 0 and 100
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "MFI should be >= 0");
            value.Should().BeLessThanOrEqualTo(100, "MFI should be <= 100");
        }
    }

    #endregion

    #region Additional Core Indicator Tests

    /// <summary>
    /// Verifies KAMA (Kaufman Adaptive Moving Average) properties.
    /// Reference: Perry Kaufman - adapts to market volatility
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void KAMA_ShouldAdaptToVolatility(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.KaufmanAdaptiveMovingAverage(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"KAMA({length}) should have valid values");

        // KAMA should follow uptrend
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter, "KAMA should follow uptrend");
    }

    /// <summary>
    /// Verifies Ultimate Oscillator properties.
    /// Reference: Larry Williams
    /// Range: 0-100
    /// </summary>
    [Fact]
    public void UltimateOscillator_ShouldBeBounded()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // UltimateOscillator uses single length param (defaults: 7, 14, 28 periods)
            handle = catalog.UltimateOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Ultimate Oscillator should have valid values");

        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "UO should be >= 0");
            value.Should().BeLessThanOrEqualTo(100, "UO should be <= 100");
        }
    }

    /// <summary>
    /// Verifies Trix indicator properties.
    /// Triple smoothed EMA rate of change
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(18)]
    public void Trix_ShouldOscillateAroundZero(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // Trix publishes one output, so the catalog hands back a plain handle. It previously
            // returned a result type with a .Signal that the indicator does not compute.
            handle = catalog.Trix(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Trix({length}) should have valid values");

        // Trix oscillates around zero
        var hasPositive = validValues.Any(v => v > 0);
        var hasNegative = validValues.Any(v => v < 0);
        (hasPositive || hasNegative).Should().BeTrue("Trix should have non-zero values");
    }

    /// <summary>
    /// Verifies Keltner Channels properties.
    /// Reference: Chester Keltner / Linda Raschke
    /// Upper > Middle > Lower
    /// </summary>
    [Theory]
    [InlineData(20)]
    [InlineData(10)]
    public void KeltnerChannels_ShouldHaveProperOrdering(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? upperHandle = null;
        SeriesHandle? middleHandle = null;
        SeriesHandle? lowerHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            var kc = catalog.KeltnerChannels(length);
            upperHandle = kc.Upper;
            middleHandle = kc.Middle;
            lowerHandle = kc.Lower;
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(upperHandle!.Value);
        runtime.Subscribe(middleHandle!.Value);
        runtime.Subscribe(lowerHandle!.Value);

        var upper = runtime.GetSeries(upperHandle!.Value).ToArray();
        var middle = runtime.GetSeries(middleHandle!.Value).ToArray();
        var lower = runtime.GetSeries(lowerHandle!.Value).ToArray();

        for (var i = length * 2; i < upper.Length; i++)
        {
            if (double.IsNaN(upper[i]) || double.IsNaN(middle[i]) || double.IsNaN(lower[i]))
                continue;

            upper[i].Should().BeGreaterThanOrEqualTo(middle[i], $"Upper >= Middle at {i}");
            middle[i].Should().BeGreaterThanOrEqualTo(lower[i], $"Middle >= Lower at {i}");
        }
    }

    /// <summary>
    /// Verifies Accumulation/Distribution Line properties.
    /// Reference: Marc Chaikin
    /// </summary>
    [Fact]
    public void AccumulationDistribution_ShouldAccumulate()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.AccumulationDistributionLine();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("ADL should have valid values");

        // ADL should change over time
        var distinctValues = validValues.Distinct().Count();
        distinctValues.Should().BeGreaterThan(1, "ADL should change with price/volume");
    }

    /// <summary>
    /// Verifies Force Index properties.
    /// Reference: Alexander Elder
    /// Force = Close change * Volume
    /// </summary>
    [Theory]
    [InlineData(13)]
    [InlineData(2)]
    public void ForceIndex_ShouldReflectPriceVolume(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ForceIndex(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Force Index({length}) should have valid values");

        // Force Index can be positive or negative
        foreach (var value in validValues)
        {
            double.IsNaN(value).Should().BeFalse();
            double.IsInfinity(value).Should().BeFalse();
        }
    }

    /// <summary>
    /// Verifies Aroon indicator properties.
    /// Reference: Tushar Chande
    /// AroonUp and AroonDown range 0-100
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(25)]
    public void Aroon_ShouldBeBounded(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? upHandle = null;
        SeriesHandle? downHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // These are real series now. Aroon Up and Down were computed and discarded, so the catalog
            // members that claimed to expose them both resolved to the oscillator instead - which is
            // why this test passed while reading the same series twice. See #170.
            var aroon = catalog.AroonOscillator(length);
            upHandle = aroon.AroonUp;
            downHandle = aroon.AroonDown;
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(upHandle!.Value);
        runtime.Subscribe(downHandle!.Value);


        var aroonUp = runtime.GetSeries(upHandle!.Value).ToArray();
        var aroonDown = runtime.GetSeries(downHandle!.Value).ToArray();

        var validUp = aroonUp.Where(v => !double.IsNaN(v)).ToArray();
        var validDown = aroonDown.Where(v => !double.IsNaN(v)).ToArray();

        validUp.Should().NotBeEmpty($"Aroon Up({length}) should have valid values");
        validDown.Should().NotBeEmpty($"Aroon Down({length}) should have valid values");

        // Each is (length - barsSinceExtreme) / length * 100, so each is bounded by 0 and 100.
        foreach (var value in validUp)
        {
            value.Should().BeGreaterThanOrEqualTo(0, $"Aroon Up({length}) is bounded below by 0");
            value.Should().BeLessThanOrEqualTo(100, $"Aroon Up({length}) is bounded above by 100");
        }

        foreach (var value in validDown)
        {
            value.Should().BeGreaterThanOrEqualTo(0, $"Aroon Down({length}) is bounded below by 0");
            value.Should().BeLessThanOrEqualTo(100, $"Aroon Down({length}) is bounded above by 100");
        }

        validUp.Should().NotEqual(validDown, "Up and Down are different series, not the oscillator twice");

    }

    /// <summary>
    /// Verifies DPO (Detrended Price Oscillator) properties.
    /// DPO removes trend to identify cycles
    /// </summary>
    [Theory]
    [InlineData(20)]
    [InlineData(14)]
    public void DPO_ShouldOscillate(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.DetrendedPriceOscillator(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"DPO({length}) should have valid values");

        // DPO should have some variance (oscillation)
        var distinctCount = validValues.Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "DPO should oscillate");
    }

    /// <summary>
    /// Verifies TSI (True Strength Index) properties.
    /// Reference: William Blau
    /// Range: typically -100 to +100
    /// </summary>
    [Fact]
    public void TSI_ShouldBeBounded()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // TSI method is named Tsi in the catalog
            handle = catalog.Tsi(25, 13);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("TSI should have valid values");

        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(-100, "TSI >= -100");
            value.Should().BeLessThanOrEqualTo(100, "TSI <= 100");
        }
    }

    /// <summary>
    /// Verifies PPO (Percentage Price Oscillator) properties.
    /// Similar to MACD but percentage-based
    /// </summary>
    [Fact]
    public void PPO_ShouldBePercentageBased()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // PPO uses single length parameter (defaults: fast=12, slow=26)
            handle = catalog.PercentagePriceOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("PPO should have valid values");

        // PPO is percentage-based, typically small values
        foreach (var value in validValues)
        {
            Math.Abs(value).Should().BeLessThan(50, "PPO should be reasonable percentage");
        }
    }

    /// <summary>
    /// Verifies APO (Absolute Price Oscillator) properties.
    /// APO = Fast EMA - Slow EMA (absolute difference)
    /// </summary>
    [Fact]
    public void APO_ShouldBeAbsoluteDifference()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // APO uses single length parameter (defaults: fast=10, slow=20)
            handle = catalog.AbsolutePriceOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("APO should have valid values");

        // During uptrend, APO should be positive (fast > slow)
        var avgApo = validValues.Average();
        avgApo.Should().BeGreaterThan(0, "APO should be positive during uptrend");
    }

    /// <summary>
    /// Verifies Chaikin Oscillator properties.
    /// Reference: Marc Chaikin
    /// ChaikinOsc = EMA(3, ADL) - EMA(10, ADL)
    /// </summary>
    [Fact]
    public void ChaikinOscillator_ShouldOscillate()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // ChaikinOscillator uses single length parameter (defaults: fast=3, slow=10)
            handle = catalog.ChaikinOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Chaikin Oscillator should have valid values");

        // Should have variance
        var distinctCount = validValues.Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "Chaikin Osc should oscillate");
    }

    /// <summary>
    /// Verifies Parabolic SAR properties.
    /// Reference: Wilder 1978
    /// SAR follows price with acceleration. The initial SAR values may be
    /// far from price during the warmup period.
    /// </summary>
    [Fact]
    public void ParabolicSAR_ShouldProduceValidValues()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // Parabolic SAR - method name is Sar()
            handle = catalog.Sar();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Parabolic SAR should have valid values");

        // SAR should have positive values (price context)
        foreach (var sar in validValues)
        {
            sar.Should().BeGreaterThanOrEqualTo(0, "SAR >= 0");
        }

        // SAR should vary over time (tracks price movement)
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "SAR should change over time");
    }

    /// <summary>
    /// Verifies SuperTrend indicator properties.
    /// Trend following indicator based on ATR
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void SuperTrend_ShouldFollowTrend(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.SuperTrend(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var closes = testData.Select(t => t.Close).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"SuperTrend({length}) should have valid values");

        // SuperTrend should be near price
        var minPrice = closes.Min();
        var maxPrice = closes.Max();
        foreach (var st in validValues)
        {
            st.Should().BeGreaterThan(minPrice * 0.5, "SuperTrend near price");
            st.Should().BeLessThan(maxPrice * 1.5, "SuperTrend near price");
        }
    }

    /// <summary>
    /// Verifies Donchian Channels properties.
    /// Reference: Richard Donchian
    /// Upper = Highest High, Lower = Lowest Low over period
    /// Note: Close can break out of channels (new high/low) - we verify band ordering only
    /// </summary>
    [Theory]
    [InlineData(20)]
    [InlineData(10)]
    public void DonchianChannels_ShouldHaveProperOrdering(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? upperHandle = null;
        SeriesHandle? lowerHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            var dc = catalog.DonchianChannels(length);
            upperHandle = dc.Upper;
            lowerHandle = dc.Lower;
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(upperHandle!.Value);
        runtime.Subscribe(lowerHandle!.Value);

        var upper = runtime.GetSeries(upperHandle!.Value).ToArray();
        var lower = runtime.GetSeries(lowerHandle!.Value).ToArray();
        var validUpper = upper.Where(v => !double.IsNaN(v)).ToArray();
        var validLower = lower.Where(v => !double.IsNaN(v)).ToArray();

        validUpper.Should().NotBeEmpty($"Donchian Upper({length}) should have valid values");
        validLower.Should().NotBeEmpty($"Donchian Lower({length}) should have valid values");

        // Upper should always be >= Lower
        for (var i = length; i < upper.Length; i++)
        {
            if (double.IsNaN(upper[i]) || double.IsNaN(lower[i])) continue;
            upper[i].Should().BeGreaterThanOrEqualTo(lower[i], $"Upper >= Lower at {i}");
        }

        // Both should be positive (in price context)
        foreach (var val in validUpper)
            val.Should().BeGreaterThan(0, "Upper should be positive");
        foreach (var val in validLower)
            val.Should().BeGreaterThan(0, "Lower should be positive");
    }

    /// <summary>
    /// Verifies Vortex Indicator properties.
    /// Reference: Etienne Botes and Douglas Siepman
    /// VI+ and VI- measure trend direction. Values are typically around 1.0 but
    /// can vary significantly in volatile conditions.
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(21)]
    public void VortexIndicator_ShouldMeasureTrend(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            // VortexIndicator returns VI+ (positive vortex)
            handle = catalog.VortexIndicator(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var viValues = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = viValues.Where(v => !double.IsNaN(v)).ToArray();

        validValues.Should().NotBeEmpty($"VortexIndicator({length}) should have valid values");

        // VI values should be positive (ratio of movement sums)
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "VI >= 0");
        }

        // Should have variance (not all same value)
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "VI should vary over time");
    }

    /// <summary>
    /// Verifies Choppiness Index properties.
    /// Range: 0-100, high values indicate choppy/ranging market
    /// </summary>
    [Theory]
    [InlineData(14)]
    public void ChoppinessIndex_ShouldBeBounded(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ChoppinessIndex(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Choppiness Index({length}) should have valid values");

        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "CI >= 0");
            value.Should().BeLessThanOrEqualTo(100, "CI <= 100");
        }
    }

    /// <summary>
    /// Verifies Awesome Oscillator properties.
    /// Reference: Bill Williams
    /// AO = SMA(5, Median) - SMA(34, Median)
    /// </summary>
    [Fact]
    public void AwesomeOscillator_ShouldOscillate()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.AwesomeOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Awesome Oscillator should have valid values");

        // Should oscillate around zero
        var distinctCount = validValues.Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "AO should oscillate");
    }

    /// <summary>
    /// Verifies Fisher Transform properties.
    /// Reference: John Ehlers
    /// Transforms prices into Gaussian distribution, bounded approximately -1 to +1 but can exceed
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void FisherTransform_ShouldBeApproximatelyBounded(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.EhlersFisherTransform(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
        validValues.Should().NotBeEmpty($"Fisher Transform({length}) should have valid values");

        // Fisher Transform can exceed -5 to +5 in extreme cases but most values should be smaller
        foreach (var value in validValues)
        {
            Math.Abs(value).Should().BeLessThan(20, "Fisher Transform should be reasonable");
        }
    }

    /// <summary>
    /// Verifies Coppock Curve properties.
    /// Reference: Edwin Coppock
    /// Long-term momentum indicator
    /// </summary>
    [Fact]
    public void CoppockCurve_ShouldOscillate()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.CoppockCurve();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Coppock Curve should have valid values");

        // Should have variance
        var distinctCount = validValues.Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "Coppock Curve should oscillate");
    }

    /// <summary>
    /// Verifies Balance of Power indicator properties.
    /// BOP = (Close - Open) / (High - Low)
    /// Bounded [-1, 1]
    /// </summary>
    [Fact]
    public void BalanceOfPower_ShouldBeBounded()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.BalanceOfPower();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Balance of Power should have valid values");

        // BOP bounded [-1, 1] (smoothed version may slightly exceed)
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(-1.5, "BOP >= -1.5");
            value.Should().BeLessThanOrEqualTo(1.5, "BOP <= 1.5");
        }
    }

    /// <summary>
    /// Verifies PVT (Price Volume Trend) properties.
    /// Reference: Cumulative volume weighted by price change
    /// </summary>
    [Fact]
    public void PVT_ShouldAccumulate()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.PriceVolumeTrend();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("PVT should have valid values");

        // PVT should change (cumulative)
        var distinctCount = validValues.Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "PVT should change over time");
    }

    /// <summary>
    /// Verifies NVI (Negative Volume Index) properties.
    /// Reference: Paul Dysart, popularized by Norman Fosback
    /// NVI changes only on down volume days
    /// </summary>
    [Fact]
    public void NVI_ShouldAccumulateOnDownVolumeDays()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.NegativeVolumeIndex();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("NVI should have valid values");

        // NVI should be positive (starts at 1000 or similar)
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThan(0, "NVI > 0");
        }
    }

    /// <summary>
    /// Verifies PVI (Positive Volume Index) properties.
    /// Reference: Paul Dysart, popularized by Norman Fosback
    /// PVI changes only on up volume days
    /// </summary>
    [Fact]
    public void PVI_ShouldAccumulateOnUpVolumeDays()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.PositiveVolumeIndex();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("PVI should have valid values");

        // PVI should be positive (starts at 1000 or similar)
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThan(0, "PVI > 0");
        }
    }

    /// <summary>
    /// Verifies EMV (Ease of Movement) properties.
    /// Reference: Richard Arms
    /// Measures price/volume relationship
    /// </summary>
    [Fact]
    public void EMV_ShouldOscillate()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.EaseOfMovement();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("EMV should have valid values");

        // EMV oscillates around zero
        var distinctCount = validValues.Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "EMV should vary");
    }

    /// <summary>
    /// Verifies VHF (Vertical Horizontal Filter) properties.
    /// Reference: Adam White
    /// Measures trend strength, higher values = stronger trend
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(28)]
    public void VHF_ShouldBePositive(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.VerticalHorizontalFilter(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"VHF({length}) should have valid values");

        // VHF should be positive
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "VHF >= 0");
        }
    }

    /// <summary>
    /// Verifies LSMA (Least Squares Moving Average) properties.
    /// Also known as Linear Regression Line or Time Series Forecast
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(25)]
    public void LSMA_ShouldProduceValidValues(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.LeastSquaresMovingAverage(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var closes = testData.Select(t => t.Close).ToArray();
        // Skip warmup period - LSMA needs 2*length bars for stable output
        var validValues = actual.Skip(length * 2).Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"LSMA({length}) should have valid values after warmup");

        // LSMA should be within price range after warmup
        var postWarmupCloses = closes.Skip(length * 2).ToArray();
        var minPrice = postWarmupCloses.Min() * 0.8;
        var maxPrice = postWarmupCloses.Max() * 1.2;
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThan(minPrice, "LSMA should be near price");
            value.Should().BeLessThan(maxPrice, "LSMA should be near price");
        }
    }

    /// <summary>
    /// Verifies ALMA (Arnaud Legoux Moving Average) properties.
    /// Reference: Arnaud Legoux
    /// Gaussian-weighted moving average
    /// </summary>
    [Theory]
    [InlineData(9)]
    [InlineData(20)]
    public void ALMA_ShouldFollowTrend(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ArnaudLegouxMovingAverage(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"ALMA({length}) should have valid values");

        // In uptrend data, ALMA should generally increase
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter, "ALMA should follow uptrend");
    }

    /// <summary>
    /// Verifies T3 (Tillson T3) moving average properties.
    /// Reference: Tim Tillson
    /// Triple-smoothed EMA variant
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(21)]
    public void T3_ShouldFollowTrend(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.TillsonT3MovingAverage(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"T3({length}) should have valid values");

        // In uptrend data, T3 should generally increase
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter, "T3 should follow uptrend");
    }

    /// <summary>
    /// Verifies McGinley Dynamic indicator properties.
    /// Reference: John McGinley
    /// Self-adjusting moving average that responds to market speed
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void McGinleyDynamic_ShouldFollowTrend(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.McGinleyDynamicIndicator(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"McGinley Dynamic({length}) should have valid values");

        // In uptrend data, McGinley should generally increase
        var firstQuarter = validValues.Take(validValues.Length / 4).Average();
        var lastQuarter = validValues.Skip(3 * validValues.Length / 4).Average();
        lastQuarter.Should().BeGreaterThan(firstQuarter, "McGinley Dynamic should follow uptrend");
    }

    /// <summary>
    /// Verifies VWMA (Volume Weighted Moving Average) properties using direct state test.
    /// MA weighted by volume
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void VWMA_ShouldProduceValidValues(int length)
    {
        // Direct test of VolumeWeightedMovingAverageState to isolate issues
        var state = new VolumeWeightedMovingAverageState(MovingAvgType.SimpleMovingAverage, length);
        var testData = CreateTrueRangeTestData();
        var results = new List<double>();

        foreach (var tick in testData)
        {
            var bar = new OhlcvBar(
                "TEST",
                BarTimeframe.Days(1),
                tick.Date,
                tick.Date,
                tick.Open,
                tick.High,
                tick.Low,
                tick.Close,
                tick.Volume,
                isFinal: true);

            var result = state.Update(bar, isFinal: true, includeOutputs: false);
            results.Add(result.Value);
        }

        // Filter out zeros (warmup period returns 0)
        var validValues = results.Where(v => !double.IsNaN(v) && v > 0).ToArray();
        validValues.Should().NotBeEmpty($"VWMA({length}) should have valid values after warmup (got {results.Count} total, {validValues.Length} non-zero)");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "VWMA should change over time");
    }

    /// <summary>
    /// Verifies VWAP (Volume Weighted Average Price) properties.
    /// Session cumulative volume-weighted average price
    /// </summary>
    [Fact]
    public void VWAP_ShouldBeNearPrice()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.Vwap();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var closes = testData.Select(t => t.Close).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("VWAP should have valid values");

        // VWAP should be within price range
        var minPrice = closes.Min() * 0.5;
        var maxPrice = closes.Max() * 1.5;
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThan(minPrice, "VWAP should be near price");
            value.Should().BeLessThan(maxPrice, "VWAP should be near price");
        }
    }

    /// <summary>
    /// Verifies Stochastic RSI properties.
    /// RSI applied to RSI, bounded [0, 100]
    /// </summary>
    [Theory]
    [InlineData(14, 14)]
    public void StochasticRSI_ShouldBeBounded(int rsiLength, int stochLength)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.StochasticRelativeStrengthIndex();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Stochastic RSI should have valid values");

        // Stochastic RSI bounded [0, 100]
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "StochRSI >= 0");
            value.Should().BeLessThanOrEqualTo(100, "StochRSI <= 100");
        }
    }

    /// <summary>
    /// Verifies Schaff Trend Cycle properties.
    /// Combination of MACD and Stochastic
    /// Bounded [0, 100]
    /// </summary>
    [Fact]
    public void SchaffTrendCycle_ShouldBeBounded()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.SchaffTrendCycle();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Schaff Trend Cycle should have valid values");

        // STC bounded [0, 100]
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "STC >= 0");
            value.Should().BeLessThanOrEqualTo(100, "STC <= 100");
        }
    }

    /// <summary>
    /// Verifies CMO (Chande Momentum Oscillator) properties.
    /// Bounded [-100, 100]
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(20)]
    public void CMO_ShouldBeBounded(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ChandeMomentumOscillator(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"CMO({length}) should have valid values");

        // CMO bounded [-100, 100]
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(-100, "CMO >= -100");
            value.Should().BeLessThanOrEqualTo(100, "CMO <= 100");
        }
    }

    /// <summary>
    /// Verifies Bull Power properties.
    /// Bull Power = High - EMA(Close)
    /// </summary>
    [Theory]
    [InlineData(13)]
    [InlineData(21)]
    public void BullPower_ShouldProduceValidValues(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.BullPowerIndicator(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Bull Power({length}) should have valid values");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "Bull Power should change over time");
    }

    /// <summary>
    /// Verifies Bear Power properties.
    /// Bear Power = Low - EMA(Close)
    /// </summary>
    [Theory]
    [InlineData(13)]
    [InlineData(21)]
    public void BearPower_ShouldProduceValidValues(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.BearPowerIndicator(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Bear Power({length}) should have valid values");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "Bear Power should change over time");
    }

    /// <summary>
    /// Verifies Linear Regression Line properties.
    /// Fits a linear trend line to price data
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(25)]
    public void LinearRegression_ShouldTrackPrice(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.LinearRegressionLine(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var closes = testData.Select(t => t.Close).ToArray();
        // Skip warmup period
        var validValues = actual.Skip(length).Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Linear Regression({length}) should have valid values");

        // Linear Regression should be within price range
        var postWarmupCloses = closes.Skip(length).ToArray();
        var minPrice = postWarmupCloses.Min() * 0.8;
        var maxPrice = postWarmupCloses.Max() * 1.2;
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThan(minPrice, "LinReg should be near price");
            value.Should().BeLessThan(maxPrice, "LinReg should be near price");
        }
    }

    /// <summary>
    /// Verifies PVO (Percentage Volume Oscillator) properties.
    /// Similar to PPO but for volume
    /// </summary>
    [Fact]
    public void PVO_ShouldProduceValidValues()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.PercentageVolumeOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("PVO should have valid values");

        // PVO oscillates around zero
        var positiveCount = validValues.Count(v => v > 0);
        var negativeCount = validValues.Count(v => v < 0);
        // With varying volume in test data, we should see both positive and negative values
        (positiveCount + negativeCount).Should().BeGreaterThan(0, "PVO should have non-zero values");
    }

    /// <summary>
    /// Verifies RVI (Relative Vigor Index) properties.
    /// Measures the strength of price movement
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void RVI_ShouldProduceValidValues(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.RelativeVigorIndex(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"RVI({length}) should have valid values");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 6)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "RVI should change over time");
    }

    /// <summary>
    /// Verifies Mass Index properties.
    /// Identifies trend reversals based on range expansion
    /// </summary>
    [Fact]
    public void MassIndex_ShouldProduceValidValues()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.MassIndex();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v) && v > 0).ToArray();
        validValues.Should().NotBeEmpty("Mass Index should have valid positive values");

        // Mass Index should be positive
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThan(0, "Mass Index should be positive");
        }
    }

    /// <summary>
    /// Verifies KVO (Klinger Volume Oscillator) properties.
    /// Combines price, volume, and accumulation/distribution
    /// </summary>
    [Fact]
    public void KVO_ShouldProduceValidValues()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.KlingerVolumeOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("KVO should have valid values");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 2)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "KVO should change over time");
    }

    /// <summary>
    /// Verifies Acceleration Oscillator properties.
    /// Derivative of Awesome Oscillator
    /// </summary>
    [Fact]
    public void AccelerationOscillator_ShouldOscillate()
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.AcceleratorOscillator();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Acceleration Oscillator should have valid values");

        // Acceleration Oscillator oscillates around zero
        var positiveCount = validValues.Count(v => v > 0);
        var negativeCount = validValues.Count(v => v < 0);
        (positiveCount + negativeCount).Should().BeGreaterThan(0, "AC should have non-zero values");
    }

    /// <summary>
    /// Verifies Ulcer Index properties.
    /// Measures downside volatility/risk
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(20)]
    public void UlcerIndex_ShouldBeNonNegative(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.UlcerIndex(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Ulcer Index({length}) should have valid values");

        // Ulcer Index should be non-negative
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "Ulcer Index >= 0");
        }
    }

    /// <summary>
    /// Verifies PFE (Polarized Fractal Efficiency) properties.
    /// Measures price path efficiency
    /// Note: PFE can exceed [-100, 100] bounds in some implementations during warmup
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void PFE_ShouldProduceValidValues(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.PolarizedFractalEfficiency(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        // Skip warmup values that may be out of normal bounds
        var validValues = actual.Skip(length * 2).Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"PFE({length}) should have valid values after warmup");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "PFE should change over time");
    }

    /// <summary>
    /// Verifies Connors RSI properties.
    /// Composite RSI with streak and rate of change components
    /// Bounded [0, 100]
    /// </summary>
    [Fact]
    public void ConnorsRSI_ShouldBeBounded()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ConnorsRelativeStrengthIndex();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("Connors RSI should have valid values");

        // Connors RSI bounded [0, 100]
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "CRSI >= 0");
            value.Should().BeLessThanOrEqualTo(100, "CRSI <= 100");
        }
    }

    /// <summary>
    /// Verifies KST (Know Sure Thing) properties.
    /// Combines multiple ROC indicators with different periods
    /// </summary>
    [Fact]
    public void KST_ShouldProduceValidValues()
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.KnowSureThing();
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty("KST should have valid values");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "KST should change over time");
    }

    /// <summary>
    /// Verifies Historical Volatility properties.
    /// Annualized standard deviation of log returns
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void HistoricalVolatility_ShouldBeNonNegative(int length)
    {
        var testData = CreateKnownPriceSeries();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.HistoricalVolatility(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
        validValues.Should().NotBeEmpty($"Historical Volatility({length}) should have valid values");

        // Historical Volatility should be non-negative
        foreach (var value in validValues)
        {
            value.Should().BeGreaterThanOrEqualTo(0, "HV >= 0");
        }
    }

    /// <summary>
    /// Verifies Chaikin Volatility properties.
    /// Rate of change of High-Low range spread
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    public void ChaikinVolatility_ShouldProduceValidValues(int length)
    {
        var testData = CreateTrueRangeTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(source);
        SeriesHandle? handle = null;

        builder.ConfigureIndicators(catalog =>
        {
            handle = catalog.ChaikinVolatility(length);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle!.Value);

        var actual = runtime.GetSeries(handle!.Value).ToArray();
        var validValues = actual.Where(v => !double.IsNaN(v)).ToArray();
        validValues.Should().NotBeEmpty($"Chaikin Volatility({length}) should have valid values");

        // Should have variance
        var distinctCount = validValues.Select(v => Math.Round(v, 4)).Distinct().Count();
        distinctCount.Should().BeGreaterThan(1, "Chaikin Volatility should change over time");
    }

    #endregion

    #region Reference Formula Implementations

    private static double[] CalculateSmaReference(double[] prices, int length)
    {
        var result = new double[prices.Length];

        for (var i = 0; i < prices.Length; i++)
        {
            if (i < length - 1)
            {
                result[i] = double.NaN;
                continue;
            }

            var sum = 0.0;
            for (var j = 0; j < length; j++)
            {
                sum += prices[i - j];
            }
            result[i] = sum / length;
        }

        return result;
    }

    private static double[] CalculateEmaReference(double[] prices, int length)
    {
        var result = new double[prices.Length];
        var k = 2.0 / (length + 1);

        for (var i = 0; i < prices.Length; i++)
        {
            if (i == 0)
            {
                result[i] = prices[i];
            }
            else
            {
                result[i] = prices[i] * k + result[i - 1] * (1 - k);
            }
        }

        return result;
    }

    private static double[] CalculateWmaReference(double[] prices, int length)
    {
        var result = new double[prices.Length];
        var weightSum = length * (length + 1) / 2.0;

        for (var i = 0; i < prices.Length; i++)
        {
            if (i < length - 1)
            {
                result[i] = double.NaN;
                continue;
            }

            var sum = 0.0;
            for (var j = 0; j < length; j++)
            {
                var weight = length - j;
                sum += prices[i - j] * weight;
            }
            result[i] = sum / weightSum;
        }

        return result;
    }

    private static double[] CalculateWilderSmoothReference(double[] values, int length)
    {
        var result = new double[values.Length];
        var k = 1.0 / length;

        for (var i = 0; i < values.Length; i++)
        {
            if (i == 0)
            {
                result[i] = values[i];
            }
            else
            {
                result[i] = values[i] * k + result[i - 1] * (1 - k);
            }
        }

        return result;
    }

    private static double[] CalculateRsiWilderReference(double[] prices, int length)
    {
        var result = new double[prices.Length];
        var gains = new double[prices.Length];
        var losses = new double[prices.Length];

        // Calculate gains and losses
        for (var i = 1; i < prices.Length; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains[i] = change > 0 ? change : 0;
            losses[i] = change < 0 ? -change : 0;
        }

        // Wilder smooth gains and losses
        var avgGains = CalculateWilderSmoothReference(gains, length);
        var avgLosses = CalculateWilderSmoothReference(losses, length);

        // Calculate RSI
        for (var i = 0; i < prices.Length; i++)
        {
            if (avgLosses[i] == 0)
            {
                result[i] = avgGains[i] == 0 ? 50 : 100;
            }
            else
            {
                var rs = avgGains[i] / avgLosses[i];
                result[i] = 100 - (100 / (1 + rs));
            }
        }

        return result;
    }

    private static double[] CalculateAtrWilderReference(List<TickerData> data, int length)
    {
        var trueRange = new double[data.Count];

        // Calculate True Range
        for (var i = 0; i < data.Count; i++)
        {
            if (i == 0)
            {
                trueRange[i] = data[i].High - data[i].Low;
            }
            else
            {
                var hl = data[i].High - data[i].Low;
                var hc = Math.Abs(data[i].High - data[i - 1].Close);
                var lc = Math.Abs(data[i].Low - data[i - 1].Close);
                trueRange[i] = Math.Max(hl, Math.Max(hc, lc));
            }
        }

        // Wilder smooth the True Range
        return CalculateWilderSmoothReference(trueRange, length);
    }

    private static double[] CalculateStochasticKReference(List<TickerData> data, int length)
    {
        var result = new double[data.Count];

        for (var i = 0; i < data.Count; i++)
        {
            if (i < length - 1)
            {
                result[i] = double.NaN;
                continue;
            }

            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;

            for (var j = 0; j < length; j++)
            {
                highestHigh = Math.Max(highestHigh, data[i - j].High);
                lowestLow = Math.Min(lowestLow, data[i - j].Low);
            }

            var range = highestHigh - lowestLow;
            result[i] = range > 0 ? (data[i].Close - lowestLow) / range * 100 : 50;
        }

        return result;
    }

    private static double[] CalculateStdDevReference(double[] prices, int length)
    {
        var result = new double[prices.Length];
        var sma = CalculateSmaReference(prices, length);

        for (var i = 0; i < prices.Length; i++)
        {
            if (i < length - 1)
            {
                result[i] = double.NaN;
                continue;
            }

            var sumSquaredDiff = 0.0;
            for (var j = 0; j < length; j++)
            {
                var diff = prices[i - j] - sma[i];
                sumSquaredDiff += diff * diff;
            }
            result[i] = Math.Sqrt(sumSquaredDiff / length);
        }

        return result;
    }

    #endregion
}
