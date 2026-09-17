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
        var spec = new IndicatorSpec(name, options, IndicatorOutput.Primary);
        var result = IndicatorCompute.ComputeArm(new StockData(bars), spec, context);
        result.Should().NotBeNull($"{name} has a fast arm this test drives directly");
        using var buffer = result!.Value;
        return buffer.ToArray();
    }
}
