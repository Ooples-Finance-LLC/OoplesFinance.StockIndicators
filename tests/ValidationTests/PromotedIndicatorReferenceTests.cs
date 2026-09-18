using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Values held against the published definition, for indicators the parity sweeps cannot judge.
/// </summary>
/// <remarks>
/// <para>
/// Every other sweep in this suite compares one engine against another: batch against streaming, and the
/// Builder's arm against the batch indicator it names. That proves the three agree. It cannot prove any of
/// them is right, and when a formula is written out three times the three copies agree on the mistake -
/// which is how a Yang-Zhang weight, a rolling-sum removal, a regression residual and an overflowing
/// product all sat green at once.
/// </para>
/// <para>
/// These tests compare against the definition instead, and each is written so that the defect it guards
/// actually moves it. Where a property pins the answer exactly - a window lying on a straight line has no
/// scatter, a geometric mean of a constant is that constant - the test asserts the property rather than a
/// recorded number, so it stays true if the fixture changes.
/// </para>
/// </remarks>
public sealed class PromotedIndicatorReferenceTests : GlobalTestData
{
    /// <summary>Tight enough to catch a wrong constant, loose enough for double rounding.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    /// A band at k sigma is k standard deviations wide either side, and a straight line says exactly what
    /// that is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ultimate moving average bands are the Bollinger construction: upper = average + k sigma, lower =
    /// average - k sigma. So the width is 2k sigma and the average cancels out of it entirely, which makes the
    /// width a reading of sigma alone and leaves the ultimate moving average unable to hide a wrong one.
    /// </para>
    /// <para>
    /// A window of L values in arithmetic progression with common difference m has population variance exactly
    /// m^2 (L^2 - 1) / 12, so the width is 2k m sqrt((L^2 - 1) / 12) - here 4 * 2.5 * sqrt(399/12), about
    /// 57.66.
    /// </para>
    /// <para>
    /// This is the discriminating fixture rather than a convenient one, and the control arm was run to say so
    /// in numbers: with the conversion reverted, all three cases fail, at 95.0, 114.0 and 355.67 against the
    /// 57.66, 69.20 and 86.55 asserted here. A constant series could not tell the two quantities apart - both
    /// are zero - and an alternating series could not either, because its moving average is flat and the two
    /// coincide exactly. Only a trend separates them.
    /// </para>
    /// <para>
    /// The first two of those numbers are the closed form of the old quantity and the third deliberately is
    /// not, which is worth recording because the tidy story is wrong. CalculateStandardDeviationVolatility
    /// measures each bar from the moving average at that bar and then averages the squares, so it warms in two
    /// stages and is only itself from about bar 2L - 2. Where it is warm, the lagging average sits a constant
    /// m (L - 1) / 2 below a straight line, every residual is that same constant, and the width is 2k m (L -
    /// 1) / 2: 95.0 at L = 20, k = 2, m = 2.5, and 114.0 at L = 20, k = 1.5, m = 4. At L = 30 bar 50 is still
    /// short of bar 58, so the outer average still holds bars whose own average window had not filled, whose
    /// residual is of the order of the price itself rather than of the slope - which is how it reaches 355.67
    /// instead of the 145 that formula would give. See issue #190.
    /// </para>
    /// <para>
    /// Read from the batch calculation's own published bands rather than through the Builder, because the
    /// width needs two named outputs at once and this is the calculation the conversion changed.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(20, 2.0, 2.5)]
    [InlineData(20, 1.5, 4.0)]
    [InlineData(30, 2.0, 2.5)]
    public void UltimateMovingAverageBands_AreTwoKSigmaWideOnALine(int minLength, double stdDevMult, double slope)
    {
        const int MaxLength = 50;
        var bars = LinearSeries(160, 100.0, slope);

        var result = new StockData(bars)
            .CalculateUltimateMovingAverageBands(MovingAvgType.SimpleMovingAverage, minLength, MaxLength, stdDevMult);
        var upper = result.OutputValues["UpperBand"];
        var lower = result.OutputValues["LowerBand"];

        var sigma = slope * Math.Sqrt(((double)(minLength * minLength) - 1) / 12);
        var expected = 2 * stdDevMult * sigma;

        // From past the longest window, so the ultimate moving average is warm too - it cancels out of the
        // width, but only once it is a number.
        for (var i = MaxLength; i < bars.Count; i++)
        {
            (upper[i] - lower[i]).Should().BeApproximately(expected, 1e-6,
                $"a band at {stdDevMult} sigma over {minLength} bars of slope {slope} is {expected} wide (bar {i})");
        }
    }

    /// <summary>
    /// The variable length moving average holds its length while there is no deviation to judge it by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The length moves on where the price sits against four levels drawn at 0.25 and 1.75 sigma either side
    /// of the average: inside the inner pair it lengthens, outside the outer pair it shortens, and between
    /// them it holds. Until the window fills there is no sigma, and the windowed deviation says so by
    /// publishing 0 - which collapses all four levels onto the average itself. A price that is not exactly on
    /// the average is then outside the outer pair by construction, so the length shortens on every warm-up
    /// bar. Measured rather than inferred: without the hold, bar 0 alone already reads 49 of 50 and 39 of 40,
    /// and this test passing across the whole warm-up range is what shows the length no longer drifts at all.
    /// </para>
    /// <para>
    /// That is shortening on the absence of a measurement rather than on a measurement, and a zero-width band
    /// is not evidence of low volatility. It is also a regression introduced by taking the windowed deviation
    /// here: the quantity this replaced published a value from the first bar, so the levels were never
    /// degenerate and the question never arose. The parity sweeps cannot see it, because both engines shorten
    /// in step - which is the blind spot this file exists for.
    /// </para>
    /// <para>
    /// A constant series cannot test this. There the price sits exactly on the average, the inner branch wins,
    /// and the length is pinned at its maximum whichever way the question is decided. It takes a trend.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(5, 50)]
    [InlineData(10, 40)]
    public void VariableLengthMovingAverage_HoldsItsLengthWhileThereIsNoDeviationYet(int minLength, int maxLength)
    {
        var bars = LinearSeries(maxLength + 40, 100.0, 2.5);

        var result = new StockData(bars)
            .CalculateVariableLengthMovingAverage(MovingAvgType.SimpleMovingAverage, minLength, maxLength);
        var lengths = result.OutputValues["Length"];

        for (var i = 0; i < maxLength - 1; i++)
        {
            lengths[i].Should().Be(maxLength,
                $"there is no {maxLength}-bar deviation at bar {i}, so nothing yet says the length should move");
        }
    }

    /// <summary>
    /// The scatter about the fitted line is zero when the window lies exactly on a line.
    /// </summary>
    /// <remarks>
    /// The discriminating case for the residual: taken against the line's endpoint rather than against the
    /// line at each position, a straight series of slope 2.5 reads about 19.12 here instead of 0.
    /// </remarks>
    [Theory]
    [InlineData(14)]
    [InlineData(20)]
    public void StandardError_IsZeroWhereTheWindowLiesOnALine(int length)
    {
        var bars = LinearSeries(80, 100.0, 2.5);

        var actual = Arm(IndicatorName.StandardError, new StandardErrorSpecOptions(length), bars);

        for (var i = length - 1; i < actual.Length; i++)
        {
            actual[i].Should().BeApproximately(0, Tolerance,
                $"a window of {length} bars lying exactly on a line has no scatter about that line (bar {i})");
        }
    }

    /// <summary>
    /// The geometric mean of a constant series is that constant, however long the window.
    /// </summary>
    /// <remarks>
    /// The discriminating case for the overflow: multiplied rather than summed as logarithms, a window of
    /// 110 bars at 1000 drives the product past 10^308 and publishes infinity. Both lengths are reachable,
    /// since the spec option clamps only to a minimum of one.
    /// </remarks>
    [Theory]
    [InlineData(110, 1000.0)]
    [InlineData(160, 100.0)]
    public void GeometricMeanMovingAverage_StaysFiniteOnALongWindow(int length, double price)
    {
        var bars = ConstantSeries(length + 40, price);

        var actual = Arm(IndicatorName.GeometricMeanMovingAverage, new GeometricMeanMovingAverageSpecOptions(length), bars);

        for (var i = length - 1; i < actual.Length; i++)
        {
            double.IsFinite(actual[i]).Should().BeTrue(
                $"a geometric mean over {length} bars at {price} must not overflow to infinity (bar {i})");
            actual[i].Should().BeApproximately(price, 1e-6,
                $"the geometric mean of a constant series is that constant (bar {i})");
        }
    }

    /// <summary>
    /// The price zone is a share of a window's movement, so it cannot leave minus one hundred to one hundred.
    /// </summary>
    /// <remarks>
    /// The discriminating case for the rolling-sum removal: subtracting a change one bar too early takes a
    /// raw price out of a sum that never held it, and the series climbs to about 120 from bar 14 on.
    /// </remarks>
    [Theory]
    [InlineData(14)]
    [InlineData(21)]
    public void SimplePriceZone_StaysWithinItsRange(int length)
    {
        var bars = StockTestData.ToList();

        var actual = Arm(IndicatorName.SimplePriceZone, new SimplePriceZoneSpecOptions(length), bars);

        for (var i = 0; i < actual.Length; i++)
        {
            actual[i].Should().BeInRange(-100, 100,
                $"the zone is the balance of a window's rises against its falls (bar {i})");
        }
    }

    /// <summary>
    /// The price zone is the relative strength index's arithmetic over a plain window.
    /// </summary>
    [Theory]
    [InlineData(14)]
    [InlineData(21)]
    public void SimplePriceZone_MatchesTheBalanceOfItsWindow(int length)
    {
        var bars = StockTestData.ToList();
        var closes = bars.Select(b => b.Close).ToArray();
        var expected = SimplePriceZoneReference(closes, length);

        var actual = Arm(IndicatorName.SimplePriceZone, new SimplePriceZoneSpecOptions(length), bars);

        actual.Should().HaveCount(expected.Length);
        for (var i = 0; i < actual.Length; i++)
        {
            actual[i].Should().BeApproximately(expected[i], Tolerance,
                $"the zone is 100 * (rises - falls) / (rises + falls) over the last {length} changes (bar {i})");
        }
    }

    /// <summary>
    /// Yang-Zhang weighs the open-to-close variance by k = 0.34 / (1.34 + (n + 1) / (n - 1)).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The constant is recovered from the indicator's own output rather than compared against a recorded
    /// series, so the test says what is wrong when it fails. On bars whose high and low both equal the
    /// close, every Rogers-Satchell term is a logarithm of one, so that part of the estimator is exactly
    /// zero and the variance is the overnight variance plus k times the open-to-close variance. Solving for
    /// k leaves the weight alone.
    /// </para>
    /// <para>
    /// The opens are moved off the previous close so the overnight term is real: a fixture whose open is
    /// the previous close makes every overnight return log(1), and k then scales a term that is already
    /// zero, which is how a wrong weight could pass unnoticed.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    public void YangZhangVolatility_WeighsTheOpenToCloseTermAsPublished(int length)
    {
        var bars = GappingSeriesWithoutRange(length + 60);
        var expectedK = 0.34 / (1.34 + ((double)(length + 1) / (length - 1)));

        var actual = Arm(IndicatorName.YangZhangVolatility, new YangZhangVolatilitySpecOptions(length), bars);

        var recovered = 0;
        for (var i = length; i < actual.Length; i++)
        {
            var (overnightVariance, openToCloseVariance) = WindowVariances(bars, i, length);
            openToCloseVariance.Should().BeGreaterThan(1e-12, $"the fixture must move from open to close (bar {i})");

            // yzVar = overnight + k * openToClose, because the Rogers-Satchell term is zero on these bars.
            var yzVariance = actual[i] * actual[i] / 252;
            var k = (yzVariance - overnightVariance) / openToCloseVariance;

            k.Should().BeApproximately(expectedK, 1e-9,
                $"the Yang-Zhang weight is 0.34 / (1.34 + (n + 1) / (n - 1)), which is {expectedK:F6} at a "
                + $"length of {length}; a denominator of 1 instead of 1.34 gives "
                + $"{0.34 / (1 + ((double)(length + 1) / (length - 1))):F6} (bar {i})");
            recovered++;
        }

        recovered.Should().BeGreaterThan(0, "the weight is recovered on at least one bar");
    }

    /// <summary>
    /// A one-bar window has no Yang-Zhang reading: both variances divide by one less than the length.
    /// </summary>
    [Fact]
    public void YangZhangVolatility_PublishesAFiniteValueAtItsShortestWindow()
    {
        var bars = GappingSeriesWithoutRange(40);

        var actual = Arm(IndicatorName.YangZhangVolatility, new YangZhangVolatilitySpecOptions(1), bars);

        foreach (var value in actual)
        {
            double.IsNaN(value).Should().BeFalse("a length of one is clamped rather than dividing by zero");
            double.IsInfinity(value).Should().BeFalse("a length of one is clamped rather than dividing by zero");
        }
    }

    /// <summary>
    /// A standard deviation does not depend on where the prices sit, only on how they are spread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A window of <paramref name="length"/> points taken from a straight line of slope s has a population
    /// deviation of exactly <c>s * sqrt((n^2 - 1) / 12)</c>, whatever the line's intercept. So the same
    /// series shifted to a higher price must give the same answer, and the closed form pins it without
    /// needing a second implementation to compare against.
    /// </para>
    /// <para>
    /// The discriminating case is a price that is large next to its own spread. Computed as
    /// <c>sqrt(E[x^2] - mean^2)</c> this subtracts two nearly-equal numbers, and the difference is lost to
    /// rounding - it can even come out negative, which is why that form carried a clamp to zero. Measured
    /// over 300 bars at length 14, the one-pass form agreed to 8.6e-13 on the AAPL fixture but at a price
    /// of 1e6 with a spread of 0.001 it was wrong by a factor of 36 and returned exactly 0 on 186 bars
    /// where the true deviation was positive.
    /// </para>
    /// <para>
    /// The spread has to be small for that to bite, which is why the slope is a parameter here. At a base
    /// of 1e6 a slope of 2.5 spreads a 14-bar window over about 35, and the one-pass error is then only
    /// about 5e-7 - it would pass this tolerance and the test would prove nothing. A slope of 0.001 is the
    /// regime the defect actually lives in.
    /// </para>
    /// <para>
    /// Driven through the batch calculation and the streaming state rather than the Builder arm: the arm
    /// already summed the squared deviations over the window, so it was the only one of the four
    /// implementations that was right, and a test written against it would not move if this regressed.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(14, 100.0, 2.5)]
    [InlineData(14, 1_000_000.0, 0.001)]
    [InlineData(20, 1_000_000.0, 0.001)]
    public void StandardDeviation_IsTheSameWhereverThePricesSit(int length, double start, double slope)
    {
        var bars = LinearSeries(length + 40, start, slope);
        var expected = slope * Math.Sqrt(((double)(length * length) - 1) / 12);

        // Far tighter than the defect, which replaced this value with zero, and loose enough that
        // subtracting a mean near 1e6 from prices near 1e6 does not trip it.
        const double tolerance = 1e-6;

        var batch = new StockData(bars)
            .CalculateStandardDevation(MovingAvgType.SimpleMovingAverage, length)
            .CustomValuesList;

        using var state = new StandardDeviationState(MovingAvgType.SimpleMovingAverage, length);
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            streaming.Add(state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: false).Value);
        }

        for (var i = length - 1; i < bars.Count; i++)
        {
            batch[i].Should().BeApproximately(expected, tolerance,
                $"a window of {length} points on a line of slope {slope} has a deviation of "
                + $"{expected:F4} at any price level, including {start} (bar {i})");
            streaming[i].Should().BeApproximately(expected, tolerance,
                $"the streaming state computes what the batch computes, at any price level (bar {i})");
        }
    }

    /// <summary>
    /// Historical volatility is the deviation of the log returns about their own mean, annualised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a series whose close alternates between P and P*r, the log returns alternate between +ln(r) and
    /// -ln(r). Over an even window their mean is exactly zero and every squared deviation is (ln r)^2, so
    /// the deviation is exactly ln(r) and the published value is 100 * ln(r) * sqrt(365). A closed form,
    /// with no second implementation to compare against.
    /// </para>
    /// <para>
    /// The indicator took its dispersion from CalculateStandardDeviationVolatility, which smooths the log
    /// returns and measures each bar's distance from that smoothed line rather than the spread of the window
    /// about its own mean. Historical volatility is defined as the standard deviation of returns, so that
    /// was the wrong quantity for the one indicator whose whole definition names it. See #190.
    /// </para>
    /// <para>
    /// The fixture has to alternate. A constant series and a geometric one both have log returns that never
    /// vary, so their deviation is zero under either quantity and the test would pass against the defect it
    /// was written for.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(14, 1.01)]
    [InlineData(20, 1.02)]
    public void HistoricalVolatility_IsTheDeviationOfItsLogReturns(int length, double ratio)
    {
        var bars = AlternatingReturnSeries(length + 40, 100.0, ratio);
        var expected = 100 * Math.Log(ratio) * Math.Sqrt(365);
        const double tolerance = 1e-7;

        var batch = new StockData(bars)
            .CalculateHistoricalVolatility(MovingAvgType.ExponentialMovingAverage, length)
            .CustomValuesList;

        using var state = new HistoricalVolatilityState(MovingAvgType.ExponentialMovingAverage, length);
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            streaming.Add(state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: false).Value);
        }

        // Bar 0 has no prior close, so its log return is a fabricated zero. A window still holding it is one
        // genuine return short, and publishing a deviation over it would dilute that value with an
        // observation that never happened - so nothing is published until index length.
        for (var i = 0; i < length; i++)
        {
            batch[i].Should().Be(0,
                $"the window still holds the first bar's fabricated return, so nothing is published (bar {i})");
            streaming[i].Should().Be(0,
                $"the streaming state withholds the same bars as the batch (bar {i})");
        }

        // From index length the window is returns 1..length, every one of them real.
        for (var i = length; i < bars.Count; i++)
        {
            batch[i].Should().BeApproximately(expected, tolerance,
                $"alternating returns of +/-ln({ratio}) have a deviation of exactly ln({ratio}), so the "
                + $"annualised value is {expected:F6} (bar {i})");
            streaming[i].Should().BeApproximately(expected, tolerance,
                $"the streaming state computes what the batch computes (bar {i})");
        }
    }

    /// <summary>
    /// The relative volatility index is the relative strength index with the standard deviation of the
    /// window in place of the price change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It took that dispersion from CalculateStandardDeviationVolatility, which smooths the series and
    /// measures each bar's distance from the smoothed line rather than the spread of the window about its
    /// own mean - and its own RviHigh and RviLow siblings already took the latter, through
    /// VolatilityCore.StandardDeviation. The family computed its defining quantity two different ways. See
    /// #190.
    /// </para>
    /// <para>
    /// Held to a reference implementation rather than a closed form, because no simple fixture separates
    /// the two quantities here. On any periodic series the deviation is the same constant on rising and
    /// falling bars, so it cancels in the ratio of the two averages and the reading is 50 either way; on a
    /// ramp every bar rises, the falling average stays zero and the reading is 100 either way. The series
    /// has to have both directions and a spread that varies, which is what the gapping fixture gives.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(10, 14)]
    [InlineData(14, 10)]
    public void RelativeVolatilityIndex_IsTheRelativeStrengthOfItsDeviation(int length, int smoothLength)
    {
        var bars = GappingSeriesWithoutRange(length + smoothLength + 60);
        var closes = bars.Select(bar => bar.Close).ToArray();
        var expected = RelativeVolatilityIndexReference(closes, length, smoothLength);
        const double tolerance = 1e-8;

        var batch = new StockData(bars)
            .CalculateRelativeVolatilityIndexV1(MovingAvgType.WildersSmoothingMethod, length, smoothLength)
            .CustomValuesList;

        using var state = new RelativeVolatilityIndexV1State(
            MovingAvgType.WildersSmoothingMethod, length, smoothLength);
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            streaming.Add(state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: false).Value);
        }

        for (var i = length - 1; i < bars.Count; i++)
        {
            batch[i].Should().BeApproximately(expected[i], tolerance,
                $"the relative volatility index is the relative strength of the window's own deviation "
                + $"(bar {i})");
            streaming[i].Should().BeApproximately(expected[i], tolerance,
                $"the streaming state computes what the batch computes (bar {i})");
        }
    }

    /// <summary>
    /// The Kase dev stop sets its stops a multiple of the range window's own deviation from that window's
    /// average.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It took that dispersion from CalculateStandardDeviationVolatility, which smooths the range series and
    /// measures each bar's distance from the smoothed line rather than the spread of the window about its
    /// own mean. A stop at avg + k * sigma is defined against the latter. See #190.
    /// </para>
    /// <para>
    /// The band factor is passed to all four stops on purpose. <c>stdDev1</c> defaults to zero, so the first
    /// stop is avg + 0 * dev - the deviation cancels out of it entirely, and it is the slot the existing
    /// parity spec drives. A reference test written against the default first stop would agree under either
    /// quantity and prove nothing about which one is taken.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(10, 21, 20, 1.0)]
    [InlineData(5, 13, 14, 2.2)]
    public void KaseDevStop_ScalesItsStopsByTheWindowsOwnDeviation(
        int fastLength, int slowLength, int length, double stdDev)
    {
        var bars = GappingSeriesWithoutRange(length + slowLength + 60);
        var expected = KaseDevStopV2Reference(bars, fastLength, slowLength, length, stdDev);
        const double tolerance = 1e-8;

        var batch = new StockData(bars)
            .CalculateKaseDevStopV2(MovingAvgType.SimpleMovingAverage, fastLength, slowLength, length,
                stdDev, stdDev, stdDev, stdDev)
            .OutputValues["Dev1"];

        using var state = new KaseDevStopV2State(MovingAvgType.SimpleMovingAverage, fastLength, slowLength,
            length, stdDev, stdDev, stdDev, stdDev);
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            streaming.Add(state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: false).Value);
        }

        for (var i = Math.Max(length, slowLength); i < bars.Count; i++)
        {
            batch[i].Should().BeApproximately(expected[i], tolerance,
                $"the stop is the range window's average plus {stdDev} of its own deviation (bar {i})");
            streaming[i].Should().BeApproximately(expected[i], tolerance,
                $"the streaming state computes what the batch computes (bar {i})");
        }
    }

    /// <summary>
    /// A fractal is a five-bar pattern, so none can be confirmed until five real bars exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Missing history is represented by 0, and 0 is below every positive price, so the two far-left
    /// comparisons of the five-bar test pass by default while the window is still filling. Three falling
    /// opening bars are then enough to confirm a fractal centred on bar 0 - a pattern whose left-hand half
    /// never happened. Raised by review on PR #214; the same fabricated-value mistake was fixed for the
    /// log-return windows in #205 and #209.
    /// </para>
    /// <para>
    /// Only the upper band can be fabricated this way, and that asymmetry is a property of the test rather
    /// than a gap in the fixture: confirming a down fractal requires the missing bars to sit above the
    /// centre, and 0 never does while prices are positive.
    /// </para>
    /// <para>
    /// Bar 10 is a genuine fractal and is asserted alongside, so the test cannot pass by the indicator
    /// having stopped finding anything at all.
    /// </para>
    /// </remarks>
    [Fact]
    public void FractalChaosBands_ConfirmNothingBeforeFiveBars()
    {
        var bars = SeriesOpeningWithFallingHighs(20);
        const double tolerance = 1e-9;

        var firstHigh = bars[0].High;
        var fractalHigh = bars[10].High;

        var batch = new StockData(bars).CalculateFractalChaosBands().OutputValues["UpperBand"];

        using var state = new FractalChaosBandsState();
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            var result = state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: true);
            result.Outputs.Should().NotBeNull("outputs were requested");
            streaming.Add(result.Outputs?["UpperBand"] ?? 0);
        }

        // Bars 0-3 have fewer than four bars behind them, so the band has nothing to anchor to yet.
        for (var i = 0; i < 4; i++)
        {
            batch[i].Should().BeApproximately(0, tolerance,
                $"bar {i} cannot complete a five-bar pattern, so no fractal is confirmed there");
            streaming[i].Should().BeApproximately(0, tolerance,
                $"the streaming state computes what the batch computes, including at bar {i}");
        }

        batch[2].Should().NotBeApproximately(firstHigh, tolerance,
            $"bar 0's high of {firstHigh} beats bars 1 and 2, but its two left-hand neighbours do not "
            + "exist and must not be read as 0");
        streaming[2].Should().NotBeApproximately(firstHigh, tolerance,
            "an unfilled buffer must not be read as 0 either");

        // The genuine fractal at bar 10 is confirmed at bar 12, so the guard has not silenced the indicator.
        batch[12].Should().BeApproximately(fractalHigh, tolerance,
            "bar 10 beats all four of its neighbours and has a complete window behind it");
        streaming[12].Should().BeApproximately(fractalHigh, tolerance,
            "the streaming state computes what the batch computes");
    }

    /// <summary>
    /// A fractal is a five-bar pattern, so a bar that beats only its immediate neighbours does not anchor
    /// the band.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The indicator compared its candidate centre against one bar on each side, which is an ordinary
    /// three-bar pivot: it fires on any single-bar wiggle, so the bands re-anchor far more often and sit
    /// much closer to price than fractal bands should. See #202.
    /// </para>
    /// <para>
    /// The fixture plants both shapes. Bar 10 beats all four of its neighbours and is a fractal under
    /// either test - it is here to show the fix has not simply stopped finding anything. Bar 20 beats bars
    /// 19 and 21 but not bars 18 and 22, so only the three-bar test accepts it. A fractal centred two bars
    /// back is confirmed two bars later, which is why the readings are taken at 12 and 22.
    /// </para>
    /// <para>
    /// Bars 18 and 22 are themselves fractals - that is unavoidable once they are raised above bar 20 - so
    /// the band legitimately re-anchors to bar 18's high at bar 20. That is what makes bar 22 the
    /// discriminating reading: the old code moves the band down to the wiggle's 102.20 there, the fixed
    /// code holds bar 18's 103.18.
    /// </para>
    /// </remarks>
    [Fact]
    public void FractalChaosBands_IgnoreAThreeBarWiggle()
    {
        var bars = SeriesWithOneFractalAndOneWiggle(40);
        const double tolerance = 1e-9;

        var fractalHigh = bars[10].High;
        var wiggleHigh = bars[20].High;
        var leftShoulderHigh = bars[18].High;

        var batch = new StockData(bars).CalculateFractalChaosBands().OutputValues["UpperBand"];

        using var state = new FractalChaosBandsState();
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            var result = state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: true);
            result.Outputs.Should().NotBeNull("outputs were requested");
            streaming.Add(result.Outputs?["UpperBand"] ?? 0);
        }

        // The genuine fractal at bar 10 is confirmed at bar 12 and anchors the band.
        batch[12].Should().BeApproximately(fractalHigh, tolerance,
            "bar 10 beats both bars on each side of it, so it is a fractal");
        streaming[12].Should().BeApproximately(fractalHigh, tolerance,
            "the streaming state computes what the batch computes");

        // The wiggle at bar 20 would be confirmed at bar 22 by a three-bar test, and must not be.
        batch[22].Should().BeApproximately(leftShoulderHigh, tolerance,
            $"bar 20's high of {wiggleHigh} beats bars 19 and 21 but not bars 18 and 22, so it is a "
            + "three-bar pivot rather than a fractal and must not move the band");
        batch[22].Should().NotBeApproximately(wiggleHigh, tolerance,
            "anchoring to the wiggle is precisely the defect");
        streaming[22].Should().BeApproximately(leftShoulderHigh, tolerance,
            "the streaming state rejects the wiggle exactly as the batch does");
    }

    /// <summary>
    /// The stop is measured against the range of the bars themselves, not against a range manufactured from
    /// the typical price.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The discriminating fixture is one whose bars have no range of their own. The typical price is
    /// (high + low + close) / 3, so on such a bar it is the close - but (c + c + c) / 3 does not always
    /// round-trip to c, and on twelve of these bars it does not. An exact containment test then read the
    /// typical price as a series with its own scale and gave those bars the move from the previous bar as
    /// their range, inflating every window holding one. See #212.
    /// </para>
    /// <para>
    /// No parity sweep could catch it, which is why it is asserted against the definition here. The two
    /// engines disagreed on precisely these bars: the streaming state reads the bar's own high and low, so
    /// only the batch arm was inflated, and on ordinary bars the typical price sits strictly inside the
    /// range and the two agree. Both arms are compared against the reference rather than against each
    /// other.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(5, 21, 20, 1.0)]
    [InlineData(5, 13, 14, 2.2)]
    public void KaseDevStopV1_MeasuresTheBarsOwnRange(
        int fastLength, int slowLength, int length, double stdDev)
    {
        var bars = GappingSeriesWithoutRange(length + slowLength + 60);
        var expected = KaseDevStopV1Reference(bars, fastLength, slowLength, length, stdDev);
        const double tolerance = 1e-8;

        var batch = new StockData(bars)
            .CalculateKaseDevStopV1(MovingAvgType.SimpleMovingAverage, fastLength, slowLength, length,
                stdDev, stdDev, stdDev, stdDev)
            .OutputValues["Dev1"];

        using var state = new KaseDevStopV1State(MovingAvgType.SimpleMovingAverage, fastLength, slowLength,
            length, stdDev, stdDev, stdDev, stdDev);
        var streaming = new List<double>(bars.Count);
        foreach (var bar in bars)
        {
            streaming.Add(state.Update(
                new OhlcvBar("TEST", BarTimeframe.Tick, bar.Date, bar.Date, bar.Open, bar.High, bar.Low,
                    bar.Close, bar.Volume, isFinal: true),
                isFinal: true,
                includeOutputs: false).Value);
        }

        for (var i = Math.Max(length, slowLength); i < bars.Count; i++)
        {
            batch[i].Should().BeApproximately(expected[i], tolerance,
                $"the stop is the bars' own range window plus {stdDev} of that window's deviation (bar {i})");
            streaming[i].Should().BeApproximately(expected[i], tolerance,
                $"the streaming state computes what the batch computes (bar {i})");
        }
    }

    /// <summary>
    /// The noise deviation is the deviation of the noise window about its own mean, and the variance is that
    /// squared.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both halves are published - <c>WhiteNoiseStdDev</c> and <c>WhiteNoiseVariance</c> - so the indicator
    /// states the relationship itself and can be held to it without a recorded number. The input is taken
    /// from the indicator's own <c>WhiteNoise</c> output rather than re-derived, so this measures the
    /// dispersion and nothing else.
    /// </para>
    /// <para>
    /// The discriminating case for #223: the deviation used to come from
    /// <c>CalculateStandardDeviationVolatility</c>, which is the mean squared distance from a moving average
    /// at each bar, rooted - about 55% wider than the window's own deviation on a typical series. Squaring
    /// that recovers the residual measure rather than a variance, so the published <c>WhiteNoiseVariance</c>
    /// was not the variance its name promises.
    /// </para>
    /// <para>
    /// <c>noiseLength</c> is passed explicitly and kept small. It defaults to 500, so on any fixture this
    /// suite can afford the window never fills, every value is zero, and the test would pass against the
    /// defect as readily as against the fix.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    public void QuasiWhiteNoise_VarianceIsItsOwnWindowsDeviationSquared(int noiseLength)
    {
        var bars = GappingSeriesWithoutRange(noiseLength + 80);
        const double tolerance = 1e-9;

        var result = new StockData(bars).CalculateQuasiWhiteNoise(MovingAvgType.WildersSmoothingMethod,
            length: 14, noiseLength: noiseLength);
        var noise = result.OutputValues["WhiteNoise"].ToArray();
        var deviation = result.OutputValues["WhiteNoiseStdDev"];
        var variance = result.OutputValues["WhiteNoiseVariance"];

        var moved = 0;
        for (var i = noiseLength; i < bars.Count; i++)
        {
            var expected = WindowDeviation(noise, i, noiseLength);
            if (expected > tolerance)
            {
                moved++;
            }

            deviation[i].Should().BeApproximately(expected, tolerance,
                $"the published deviation is the noise window's own deviation (bar {i})");
            variance[i].Should().BeApproximately(expected * expected, tolerance,
                $"WhiteNoiseVariance is that deviation squared (bar {i})");
        }

        moved.Should().BeGreaterThan(0,
            "the fixture must produce a non-zero deviation, or this would hold against any definition");
    }

    /// <summary>
    /// The crossover bands blend by a weight taken from the variance of the lagged price window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ka</c> is <c>1 - (prevVar / secma)</c>, and <c>secma</c> is a squared distance, so the ratio reads
    /// as "the share of the distance already accounted for" only when the numerator is a variance. This
    /// writes the definition out - the lagged window's variance, both squared distances, both weights and
    /// both blends - and holds the indicator to the values that follow from it.
    /// </para>
    /// <para>
    /// Held to values rather than to a property on purpose, and the first attempt at this test is worth
    /// recording. Asserting that the blend stays between its two endpoints proves nothing here: the
    /// indicator forms <c>ka</c> only on the branch where <c>prevVar &lt; secma</c>, so the weight is inside
    /// 0..1 by construction whatever quantity <c>prevVar</c> holds. That version passed against the defect.
    /// The values are what move. See #223.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    public void UhlMaCrossoverSystem_BlendsByTheLaggedWindowsVariance(int length)
    {
        var bars = GappingSeriesWithoutRange(length + 120);
        const double tolerance = 1e-9;

        var result = new StockData(bars).CalculateUhlMaCrossoverSystem(MovingAvgType.SimpleMovingAverage, length);
        var cma = result.OutputValues["Cma"];
        var cts = result.OutputValues["Cts"];
        var closes = bars.Select(b => b.Close).ToArray();

        var expectedCma = new double[bars.Count];
        var expectedCts = new double[bars.Count];
        var blended = 0;

        for (var i = 0; i < bars.Count; i++)
        {
            var value = closes[i];
            var sma = WindowAverage(closes, i, length);
            var prevCma = i >= 1 ? expectedCma[i - 1] : value;
            var prevCts = i >= 1 ? expectedCts[i - 1] : value;

            // The indicator reads the dispersion of the window that ended a full length ago.
            var prevDev = i >= length ? WindowDeviation(closes, i - length, length) : 0;
            var prevVar = prevDev * prevDev;

            var secma = (sma - prevCma) * (sma - prevCma);
            var sects = (value - prevCts) * (value - prevCts);
            var ka = prevVar < secma && secma != 0 ? 1 - (prevVar / secma) : 0;
            var kb = prevVar < sects && sects != 0 ? 1 - (prevVar / sects) : 0;

            if (ka > 0)
            {
                blended++;
            }

            expectedCma[i] = (ka * sma) + ((1 - ka) * prevCma);
            expectedCts[i] = (kb * value) + ((1 - kb) * prevCts);
        }

        for (var i = length; i < bars.Count; i++)
        {
            cma[i].Should().BeApproximately(expectedCma[i], tolerance,
                $"cma blends the average by the lagged window's variance (bar {i})");
            cts[i].Should().BeApproximately(expectedCts[i], tolerance,
                $"cts blends the price by the lagged window's variance (bar {i})");
        }

        blended.Should().BeGreaterThan(0,
            "the fixture must actually blend somewhere, or every weight is zero and this holds trivially");
    }

    /// <summary>
    /// The normal equations are solved against the variances of the very windows its covariances come from,
    /// and those windows are the bar index and its square.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>n</c> is the bar index and <c>n2</c> its square, so the two variance terms do not depend on the
    /// prices at all. Over a full window of <c>L</c> consecutive integers the population variance is exactly
    /// <c>(L^2 - 1) / 12</c> - 33.25 at a length of 20 and 208.25 at 50. This reference substitutes that
    /// closed form rather than re-deriving it from the series, so one side of the comparison is arithmetic
    /// the indicator cannot influence at all.
    /// </para>
    /// <para>
    /// Before #223 those terms came from <c>CalculateStandardDeviationVolatility</c> of the price series,
    /// which is of order one against a required 33.25 or 208.25. Reverting the fix moves the published
    /// series by orders of magnitude, so this cannot hold both ways.
    /// </para>
    /// <para>
    /// Raised by review on PR #224, and the reasoning there is worth keeping: the generic parity sweep
    /// compares this indicator's streaming state against its batch twin, so it passes whenever both engines
    /// make the same mistake, and the golden-file test asserts only that the values are finite. Neither
    /// could fail if the corrected terms were put back.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    public void QuadraticLeastSquaresMovingAverage_SolvesAgainstTheIndexWindowsVariances(int length)
    {
        var bars = GappingSeriesWithoutRange(length + 120);
        var closes = bars.Select(b => b.Close).ToArray();

        var result = new StockData(bars).CalculateQuadraticLeastSquaresMovingAverage(
            MovingAvgType.SimpleMovingAverage, length);
        var qlma = result.OutputValues["Qlma"];

        var n = new double[bars.Count];
        var n2 = new double[bars.Count];
        var nn2 = new double[bars.Count];
        var n2v = new double[bars.Count];
        var nv = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            n[i] = i;
            n2[i] = (double)i * i;
            nn2[i] = n[i] * n2[i];
            n2v[i] = n2[i] * closes[i];
            nv[i] = n[i] * closes[i];
        }

        // The population variance of L consecutive integers, which is what the n window always is.
        var indexVariance = (((double)length * length) - 1) / 12;
        var solved = 0;

        for (var i = length - 1; i < bars.Count; i++)
        {
            var sma = WindowAverage(closes, i, length);
            var nSma = WindowAverage(n, i, length);
            var n2Sma = WindowAverage(n2, i, length);
            var nn2Sma = WindowAverage(nn2, i, length);
            var n2vSma = WindowAverage(n2v, i, length);
            var nvSma = WindowAverage(nv, i, length);

            var nn2Cov = nn2Sma - (nSma * n2Sma);
            var n2vCov = n2vSma - (n2Sma * sma);
            var nvCov = nvSma - (nSma * sma);

            var n2Dev = WindowDeviation(n2, i, length);
            var n2Variance = n2Dev * n2Dev;

            var norm = (n2Variance * indexVariance) - (nn2Cov * nn2Cov);
            if (norm == 0)
            {
                continue;
            }

            solved++;
            var a = ((n2vCov * indexVariance) - (nvCov * nn2Cov)) / norm;
            var b = ((nvCov * n2Variance) - (n2vCov * nn2Cov)) / norm;
            var c = sma - (a * n2Sma) - (b * nSma);
            var expected = (a * n2[i]) + (b * i) + c;

            // Relative, because the index terms grow with the bar: n2 reaches five figures on this fixture,
            // and the determinant divides two large products, so an absolute floor would be meaningless.
            qlma[i].Should().BeApproximately(expected, 1e-6 * Math.Max(1, Math.Abs(expected)),
                $"the quadratic fit solves against the index window's variance of {indexVariance} (bar {i})");
        }

        solved.Should().BeGreaterThan(0,
            "the fixture must produce a solvable system, or every bar is skipped and this holds trivially");
    }

    #region Reference formula implementations

    /// <summary>
    /// The published arithmetic, written out plainly: the rises and the falls among the last
    /// <paramref name="length"/> changes, and the balance between them as a percentage.
    /// </summary>
    private static double[] SimplePriceZoneReference(double[] closes, int length)
    {
        var result = new double[closes.Length];

        for (var i = 0; i < closes.Length; i++)
        {
            if (i < 1)
            {
                result[i] = 0;
                continue;
            }

            double up = 0;
            double down = 0;
            var first = Math.Max(1, i - length + 1);
            for (var j = first; j <= i; j++)
            {
                var change = closes[j] - closes[j - 1];
                if (change > 0)
                {
                    up += change;
                }
                else if (change < 0)
                {
                    down += -change;
                }
            }

            var total = up + down;
            result[i] = total != 0 ? 100 * (up - down) / total : 0;
        }

        return result;
    }

    /// <summary>The mean of the window ending at <paramref name="index"/>, or zero before it fills.</summary>
    private static double WindowAverage(double[] values, int index, int length)
    {
        if (index < length - 1)
        {
            return 0;
        }

        double sum = 0;
        for (var j = index - length + 1; j <= index; j++)
        {
            sum += values[j];
        }

        return sum / length;
    }

    /// <summary>The deviation of that window about its own mean, or zero before it fills.</summary>
    private static double WindowDeviation(double[] values, int index, int length)
    {
        if (index < length - 1)
        {
            return 0;
        }

        var mean = WindowAverage(values, index, length);
        double sum = 0;
        for (var j = index - length + 1; j <= index; j++)
        {
            sum += (values[j] - mean) * (values[j] - mean);
        }

        return Math.Sqrt(sum / length);
    }

    /// <summary>
    /// The Kase dev stop written out from its definition: a stop set a multiple of the range window's own
    /// deviation away from that window's average.
    /// </summary>
    /// <remarks>
    /// Both the averages and the deviation are zero until their windows fill, which is what
    /// <c>MovingAverageCore.SimpleMovingAverage</c> and <c>VolatilityCore.StandardDeviation</c> both do, so
    /// the warm-up needs no special case. During it the two averages are equal, the trend reads -1 rather
    /// than 1, and that is reproduced here rather than corrected for.
    /// </remarks>
    private static double[] KaseDevStopV2Reference(
        List<TickerData> bars, int fastLength, int slowLength, int length, double stdDev)
    {
        var count = bars.Count;
        var closes = new double[count];
        for (var i = 0; i < count; i++)
        {
            closes[i] = bars[i].Close;
        }

        var ranges = new double[count];
        var prices = new double[count];
        var trends = new double[count];

        for (var i = 0; i < count; i++)
        {
            var high = bars[i].High;
            var low = bars[i].Low;
            var previousHigh = i >= 1 ? bars[i - 1].High : 0;
            var previousLow = i >= 1 ? bars[i - 1].Low : 0;
            var previousClose = i >= 2 ? closes[i - 2] : 0;

            trends[i] = WindowAverage(closes, i, fastLength) > WindowAverage(closes, i, slowLength) ? 1 : -1;

            var price = trends[i] == 1 ? high : low;
            prices[i] = trends[i] > 0 ? Math.Max(price, high) : Math.Min(price, low);

            ranges[i] = Math.Max(Math.Max(high, previousHigh), previousClose)
                - Math.Min(Math.Min(low, previousLow), previousClose);
        }

        var result = new double[count];
        for (var i = 0; i < count; i++)
        {
            var average = WindowAverage(ranges, i, length);
            var deviation = WindowDeviation(ranges, i, length);
            result[i] = (prices[i] + (-1 * trends[i])) * (average + (stdDev * deviation));
        }

        return result;
    }

    /// <summary>
    /// The Kase dev stop V1 written out from its definition: the typical price displaced by the average
    /// range of the bars themselves and a multiple of that window's own deviation.
    /// </summary>
    /// <remarks>
    /// The averages and the deviation are zero until their windows fill, as both engines leave them. The
    /// first two bars have no bar two back, and the zero standing in for it is reproduced here rather than
    /// corrected for, so the reference says what the indicator says. The test asserts only from the longest
    /// window on, so that warm-up is not what either side is being judged by.
    /// </remarks>
    private static double[] KaseDevStopV1Reference(
        List<TickerData> bars, int fastLength, int slowLength, int length, double stdDev)
    {
        var count = bars.Count;
        var typical = new double[count];
        var ranges = new double[count];

        for (var i = 0; i < count; i++)
        {
            var high = bars[i].High;
            var low = bars[i].Low;
            typical[i] = (high + low + bars[i].Close) / 3;

            var previousClose = i >= 2 ? bars[i - 2].Close : 0;
            var previousLow = i >= 2 ? bars[i - 2].Low : 0;
            ranges[i] = Math.Max(
                Math.Max(high - previousLow, Math.Abs(high - previousClose)),
                Math.Abs(low - previousClose));
        }

        var result = new double[count];
        for (var i = 0; i < count; i++)
        {
            var average = WindowAverage(ranges, i, length);
            var deviation = WindowDeviation(ranges, i, length);
            var fast = WindowAverage(typical, i, fastLength);
            var slow = WindowAverage(typical, i, slowLength);

            result[i] = fast < slow
                ? typical[i] + average + (stdDev * deviation)
                : typical[i] - average - (stdDev * deviation);
        }

        return result;
    }

    /// <summary>
    /// The relative volatility index written out from its definition: the relative strength index with the
    /// standard deviation of the window in place of the price change.
    /// </summary>
    /// <remarks>
    /// The deviation is the spread of the window about its own mean and is zero until the window fills, as
    /// <c>VolatilityCore.StandardDeviation</c> leaves it. The averages are Wilder's, which both engines
    /// implement as <c>value/n + previous*(1 - 1/n)</c> seeded at zero, with no separate warm-up.
    /// </remarks>
    private static double[] RelativeVolatilityIndexReference(double[] values, int length, int smoothLength)
    {
        var result = new double[values.Length];
        var k = 1.0 / Math.Max(1, smoothLength);
        double avgUp = 0;
        double avgDown = 0;

        for (var i = 0; i < values.Length; i++)
        {
            double deviation = 0;
            if (i >= length - 1)
            {
                double mean = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    mean += values[j];
                }

                mean /= length;

                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += (values[j] - mean) * (values[j] - mean);
                }

                deviation = Math.Sqrt(sum / length);
            }

            var previous = i >= 1 ? values[i - 1] : 0;
            var up = values[i] > previous ? deviation : 0;
            var down = values[i] < previous ? deviation : 0;

            avgUp = (up * k) + (avgUp * (1 - k));
            avgDown = (down * k) + (avgDown * (1 - k));

            var rs = avgDown != 0 ? avgUp / avgDown : 0;
            result[i] = avgDown == 0 ? 100 : avgUp == 0 ? 0 : Math.Min(Math.Max(100 - (100 / (1 + rs)), 0), 100);
        }

        return result;
    }

    /// <summary>
    /// The overnight and open-to-close variances of the window ending at <paramref name="index"/>, taken
    /// the way the estimator takes them: each mean over the window's length, each variance over one less
    /// than it, because the mean came from the same window.
    /// </summary>
    private static (double Overnight, double OpenToClose) WindowVariances(List<TickerData> bars, int index, int length)
    {
        var first = index - length + 1;

        double overnightMean = 0;
        double openToCloseMean = 0;
        for (var j = first; j <= index; j++)
        {
            if (j > 0 && bars[j - 1].Close != 0)
            {
                overnightMean += Math.Log(bars[j].Open / bars[j - 1].Close);
            }

            if (bars[j].Open != 0)
            {
                openToCloseMean += Math.Log(bars[j].Close / bars[j].Open);
            }
        }

        overnightMean /= length;
        openToCloseMean /= length;

        double overnightSum = 0;
        double openToCloseSum = 0;
        for (var j = first; j <= index; j++)
        {
            if (j > 0 && bars[j - 1].Close != 0)
            {
                var logOvernight = Math.Log(bars[j].Open / bars[j - 1].Close) - overnightMean;
                overnightSum += logOvernight * logOvernight;
            }

            if (bars[j].Open != 0)
            {
                var logOpenToClose = Math.Log(bars[j].Close / bars[j].Open) - openToCloseMean;
                openToCloseSum += logOpenToClose * logOpenToClose;
            }
        }

        return (overnightSum / (length - 1), openToCloseSum / (length - 1));
    }

    #endregion

    #region Fixtures

    /// <summary>A series lying exactly on a line, so its scatter about that line is zero.</summary>
    private static List<TickerData> LinearSeries(int count, double start, double slope)
    {
        var data = new List<TickerData>(count);
        var date = new DateTime(2024, 1, 1);
        for (var i = 0; i < count; i++)
        {
            var close = start + (slope * i);
            data.Add(new TickerData
            {
                Date = date.AddDays(i),
                Open = i == 0 ? close : start + (slope * (i - 1)),
                High = close,
                Low = close,
                Close = close,
                Volume = 1_000_000
            });
        }

        return data;
    }

    /// <summary>
    /// Bars that gap overnight and have no range of their own.
    /// </summary>
    /// <remarks>
    /// The high and the low both sit on the close, so every Rogers-Satchell term is a logarithm of one and
    /// that part of the Yang-Zhang estimator is exactly zero. The open is displaced off the previous close,
    /// so the overnight term is real: the two fixtures this file's neighbours use set the open to the
    /// previous close, which makes every overnight return log(1) and hides a wrong weight entirely. The
    /// open also never equals its own close, so the open-to-close variance the weight multiplies is not
    /// itself zero.
    /// </remarks>
    private static List<TickerData> GappingSeriesWithoutRange(int count)
    {
        var data = new List<TickerData>(count);
        var date = new DateTime(2024, 1, 1);
        var close = 100.0;

        for (var i = 0; i < count; i++)
        {
            // A gap that changes sign and size from bar to bar, so the overnight returns have a spread.
            var gap = (((i % 7) - 3) * 0.35) + 0.15;
            var open = i == 0 ? close : close + gap;

            // The close moves off its own open by an amount that also varies, so the open-to-close
            // returns have a spread of their own rather than a single repeated value.
            close = open + (((i % 5) - 2) * 0.4) + 0.55;

            data.Add(new TickerData
            {
                Date = date.AddDays(i),
                Open = open,
                High = close,
                Low = close,
                Close = close,
                Volume = 1_000_000
            });
        }

        return data;
    }

    /// <summary>
    /// A series whose opening bars fall, so that a five-bar test applied before its window has filled
    /// confirms a fractal centred on the first bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fall is what makes this discriminating, and it is why the wiggle fixture below cannot stand in:
    /// that one drifts upward, so bar 1's high never falls below bar 0's and the right-hand half of the
    /// pattern is never satisfied during warm-up. Here it is, leaving only the two absent left-hand
    /// neighbours between the indicator and a fabricated fractal.
    /// </para>
    /// <para>
    /// Highs and lows are given real ranges for the same reason as the fixture below: a fractal is a
    /// statement about highs and lows, and the fixtures that set both to the close cannot express one.
    /// </para>
    /// </remarks>
    private static List<TickerData> SeriesOpeningWithFallingHighs(int count)
    {
        var data = new List<TickerData>(count);
        var date = new DateTime(2024, 1, 1);

        for (var i = 0; i < count; i++)
        {
            // Bars 0-4 fall, so bar 0's high beats bars 1 and 2 - the whole right-hand half of a five-bar
            // test centred on it. Its left-hand half is the two bars that do not exist. The drift afterwards
            // is gentle and monotone so that nothing but the planted peak can form an extreme.
            var high = i <= 4 ? 110 - i : 106 + ((i - 4) * 0.01);
            var low = i <= 4 ? 90 + i : 94 - ((i - 4) * 0.01);

            // Bar 10 beats all four of its neighbours with a complete window behind it: a genuine fractal.
            if (i == 10)
            {
                high += 5;
                low -= 5;
            }

            data.Add(new TickerData
            {
                Date = date.AddDays(i),
                Open = (high + low) / 2,
                High = high,
                Low = low,
                Close = (high + low) / 2,
                Volume = 1_000_000
            });
        }

        return data;
    }

    /// <summary>
    /// A series carrying one genuine five-bar fractal and one three-bar wiggle that is not a fractal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other fixture here sets the high and the low to the close, so no fractal can be expressed in
    /// them at all: the pattern is a statement about highs and lows moving independently of where the bar
    /// closed. This one gives each bar a real range.
    /// </para>
    /// <para>
    /// The discriminating shape is the wiggle. Its high beats both immediate neighbours but not the bars
    /// two out, so a three-bar pivot test anchors a band to it and a five-bar fractal test does not - which
    /// is the whole of #202. The peak is the opposite: it beats all four of its neighbours, so both tests
    /// agree it is a fractal, and it is there to prove the fix has not simply stopped finding anything.
    /// </para>
    /// </remarks>
    private static List<TickerData> SeriesWithOneFractalAndOneWiggle(int count)
    {
        var data = new List<TickerData>(count);
        var date = new DateTime(2024, 1, 1);

        for (var i = 0; i < count; i++)
        {
            // A gentle drift, so nothing but the two planted shapes can form an extreme.
            var high = 100 + (i * 0.01);
            var low = 90 - (i * 0.01);

            // Bar 10 beats all four neighbours: a real fractal under either test.
            if (i == 10)
            {
                high += 5;
                low -= 5;
            }

            // Bar 20 beats only its immediate neighbours; bars 18 and 22 are raised above it, so the
            // five-bar test rejects it and the three-bar test does not.
            if (i == 20)
            {
                high += 2;
                low -= 2;
            }

            if (i == 18 || i == 22)
            {
                high += 3;
                low -= 3;
            }

            data.Add(new TickerData
            {
                Date = date.AddDays(i),
                Open = (high + low) / 2,
                High = high,
                Low = low,
                Close = (high + low) / 2,
                Volume = 1_000_000
            });
        }

        return data;
    }

    /// <summary>A series that never moves, so every mean of it is the price itself.</summary>
    private static List<TickerData> ConstantSeries(int count, double price)
    {
        var data = new List<TickerData>(count);
        var date = new DateTime(2024, 1, 1);
        for (var i = 0; i < count; i++)
        {
            data.Add(new TickerData
            {
                Date = date.AddDays(i),
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = 1_000_000
            });
        }

        return data;
    }

    /// <summary>
    /// A series whose close alternates between a price and that price times <paramref name="ratio"/>.
    /// </summary>
    /// <remarks>
    /// Its log returns alternate between +ln(ratio) and -ln(ratio), so over an even window their mean is
    /// exactly zero and every squared deviation is the same, making the population deviation exactly
    /// ln(ratio). That gives historical volatility a closed form to be held to, which neither a constant nor
    /// a geometric series can: both have log returns that never vary, so their deviation is zero under any
    /// definition - and zero is also what the defect produced, so such a fixture would pass against it.
    /// </remarks>
    private static List<TickerData> AlternatingReturnSeries(int count, double start, double ratio)
    {
        var data = new List<TickerData>(count);
        var date = new DateTime(2024, 1, 1);
        for (var i = 0; i < count; i++)
        {
            var close = i % 2 == 0 ? start : start * ratio;
            data.Add(new TickerData
            {
                Date = date.AddDays(i),
                Open = close,
                High = close,
                Low = close,
                Close = close,
                Volume = 1_000_000
            });
        }

        return data;
    }

    #endregion

    /// <summary>
    /// The Builder's own arm for a spec, which is the code a caller reaches. Not
    /// <c>TryComputeFast</c>: that serves the batch indicator whenever an arm is bound but unverified, so a
    /// test written against it would quietly start measuring the batch if the arm ever lost its verification.
    /// </summary>
    private static double[] Arm(IndicatorName name, IIndicatorSpecOptions options, List<TickerData> bars)
    {
        using var context = new ComputeContext();
        var spec = new IndicatorSpec(name, options);
        var result = IndicatorCompute.ComputeArm(new StockData(bars), spec, context);
        result.Should().NotBeNull($"{name} has a fast arm this test drives directly");
        using var buffer = result!.Value;
        return buffer.ToArray();
    }
}
