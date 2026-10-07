using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class HullComparisonTests
{
    public static IEnumerable<object[]> Configurations =>
        Enum.GetValues<HullWindowConvention>()
            .SelectMany(c => new[] { 2, 3, 7 }.Select(p => new object[] { c, p }));

    [Theory, MemberData(nameof(Configurations))]
    public async Task CompleteOutputsAndLifecycle(HullWindowConvention convention, int period)
    {
        ComparisonVerifier.Check(
            HullComparison.Create(convention),
            HullComparison.Fixture(),
            period
        );
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(HullWindow),
                "Hull convention",
                () => new HullWindow(period, convention)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        var definition = new HullWindow(period, convention);
        var flags = HullComparison
            .Create(convention)
            .Ooples(HullComparison.Fixture(), period)
            .Outputs["Value"]
            .Present!;
        var first =
            convention == HullWindowConvention.ExpandingFloor
                ? 0
                : period + definition.RootPeriod - 2;
        Assert.All(flags.Take(first), present => Assert.False(present));
        Assert.All(flags.Skip(first), present => Assert.True(present));
    }

    [Fact]
    public void HalfEvenAndRootRoundingAreExplicit()
    {
        foreach (
            var (period, half, root) in new[]
            {
                (3, 2, 2),
                (5, 2, 2),
                (7, 4, 3),
                (8, 4, 3),
                (9, 4, 3),
            }
        )
        {
            var indicator = new HullWindow(period, HullWindowConvention.FullWindowRounded);
            Assert.Equal(half, indicator.HalfPeriod);
            Assert.Equal(root, indicator.RootPeriod);
            foreach (var pair in HullComparison.Pairs)
                ComparisonVerifier.Check(pair, HullComparison.Fixture(), period);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new HullWindow(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HullWindow(3, (HullWindowConvention)99)
        );
        Assert.Equal(int.MaxValue, new HullWindow(int.MaxValue).WarmupBars);
    }

    [Fact]
    public void FiniteFinalValueSurvivesOversizedSyntheticStage()
    {
        var max = double.MaxValue;
        double[] prices = [-max, -max, max, max, -max];
        foreach (
            var convention in new[]
            {
                HullWindowConvention.FullWindowFloor,
                HullWindowConvention.FullWindowRounded,
            }
        )
        {
            var actual = HullComparison.Owned(BarsFor(prices), 4, convention);
            ComparisonVerifier.Compare(
                HullComparison.Reference(prices, 4, convention),
                actual,
                "oversized unpublished Hull stage",
                IndicatorErrorBudget.Exact
            );
            Assert.All(actual.Outputs["Value"].Present!.Take(4), p => Assert.False(p));
            Assert.InRange(actual.Outputs["Value"].Values[4] / max, .0888, .089);
        }
        var flat = HullComparison.Owned(
            BarsFor([max, max, max]),
            3,
            HullWindowConvention.ExpandingFloor
        );
        Assert.All(flat.Outputs["Value"].Values, value => Assert.Equal(max, value));
        var native = new QuanTAlib.Hma(3);
        // Native final convolution rejects its overflowing synthetic input and returns its fallback.
        Assert.Equal(0, native.Calc(new QuanTAlib.TValue(max, true, false)).Value);
    }

    [Fact]
    public void GenuineOverflowIsRejectedAndTinyPricesRemainIndependent()
    {
        double[] overflowing = [-double.MaxValue, double.MaxValue];
        Assert.True(
            double.IsPositiveInfinity(
                HullComparison
                    .Reference(overflowing, 2, HullWindowConvention.ExpandingFloor)
                    .Outputs["Value"]
                    .Values[1]
            )
        );
        Assert.Throws<IndicatorOutputException>(() =>
            HullComparison.Owned(BarsFor(overflowing), 2, HullWindowConvention.ExpandingFloor)
        );
        double[] tiny =
        [
            0,
            double.Epsilon,
            2 * double.Epsilon,
            -double.Epsilon,
            4 * double.Epsilon,
            0,
            -2 * double.Epsilon,
        ];
        foreach (var convention in Enum.GetValues<HullWindowConvention>())
        foreach (var period in new[] { 2, 3, int.MaxValue })
            ComparisonVerifier.Compare(
                HullComparison.Reference(tiny, period, convention),
                HullComparison.Owned(BarsFor(tiny), period, convention),
                "tiny and lazy Hull",
                IndicatorErrorBudget.Exact
            );
    }

    private static Bar[] BarsFor(double[] prices) =>
        prices.Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1)).ToArray();

    [Fact]
    public void QuanSourceRevisionAndRetainedNestedHistoryAfterReset()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Hma(source, 4);
        var direct = new QuanTAlib.Hma(4);
        foreach (var x in new[] { 1d, 2, 4, 8, 16 })
        {
            var input = new QuanTAlib.TValue(x, true, false);
            source.Add(input);
            Assert.Equal(direct.Calc(input).Value, subscribed.Value);
        }
        Assert.Equal(5, direct.WarmupPeriod);
        Assert.True(direct.IsHot);
        direct.Calc(new QuanTAlib.TValue(7, false, false));
        var fresh = new QuanTAlib.Hma(4);
        foreach (var x in new[] { 1d, 2, 4, 8, 7 })
            fresh.Calc(new QuanTAlib.TValue(x, true, false));
        Assert.Equal(fresh.Value, direct.Value);
        direct.Init();
        Assert.Equal(
            fresh.Calc(new QuanTAlib.TValue(10, true, false)).Value,
            direct.Calc(new QuanTAlib.TValue(10, true, false)).Value
        );
        Assert.False(direct.IsHot);
        Assert.NotEqual(10, direct.Value);
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Hma(1));
    }

    [Fact]
    public void SkenderQuoteTupleAndReusableRoutes()
    {
        var data = CompetitorData.FromCloses([1, Math.BitIncrement(1d), 2, 4, 8, 3, 9]);
        var tuples = data.Closes.Select((x, i) => (DateTime.UnixEpoch.AddDays(i), x)).ToArray();
        var rows = tuples.GetHma(3).ToArray();
        ComparisonVerifier.Compare(
            HullComparison.SkenderReference(data.Closes, 3),
            VolumePriceComparison.Mask(rows.Select(r => r.Hma).ToArray()),
            "Skender tuple",
            IndicatorErrorBudget.Exact
        );
        Assert.Equal(rows.Select(r => r.Hma), tuples.GetSma(1).GetHma(3).Select(r => r.Hma));
        Assert.Equal(tuples.Select(t => t.Item1), rows.Select(r => r.Date));
        ComparisonVerifier.Check(
            HullComparison.Create(HullWindowConvention.FullWindowFloor),
            data,
            3
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetHma(1).ToArray());
    }

    [Fact]
    public void TradyTupleMapperRangesNullsAndDegeneratePeriods()
    {
        decimal?[] values = [1, 2, null, 4, 8, 3, null, 9, -1];
        foreach (var period in new[] { 0, 1, 2, 3, 5, 7 })
        {
            var expected = HullComparison.DecimalReference(values, period);
            var tuple = new T.HullMovingAverageByTuple(values, period);
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(expected.Skip(2).Take(4), tuple.Compute(startIndex: 2, endIndex: 5));
            Assert.Equal(expected[4], tuple[4]);
            var mapped = new T.HullMovingAverage<int, decimal?>(
                Enumerable.Range(0, values.Length),
                i => values[i],
                period
            );
            Assert.Equal(expected, mapped.Compute());
        }
        Assert.Equal(
            new decimal?[] { -1, -2, 0 },
            new T.HullMovingAverageByTuple([1m, 2m, null], 1).Compute()
        );
        Assert.Throws<OverflowException>(() => new T.HullMovingAverageByTuple(values, -1));
        decimal?[] overflowing = [decimal.MaxValue, decimal.MaxValue, decimal.MaxValue];
        Assert.Throws<OverflowException>(() =>
            new T.HullMovingAverageByTuple(overflowing, 2).Compute()
        );
    }

    [Fact]
    public void CorruptValuesPresenceAndConventionAreDetected()
    {
        foreach (var pair in HullComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    HullComparison.Fixture(),
                    3
                )
            );
        }
        var wrong = HullComparison.Create(HullWindowConvention.FullWindowRounded) with
        {
            Library = HullComparison.Create(HullWindowConvention.FullWindowFloor).Library,
        };
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(wrong, HullComparison.Fixture(), 3)
        );
    }
}
