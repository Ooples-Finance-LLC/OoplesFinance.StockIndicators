using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PathRatioComparisonTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(20, false)]
    [InlineData(20, true)]
    [InlineData(int.MaxValue, false)]
    [InlineData(int.MaxValue, true)]
    public async Task IndependentContractsVerifyWindowExpirationAndLifecycle(
        int period,
        bool absolute
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowPathRatio),
                $"{period}/{absolute}",
                () =>
                    new WindowPathRatio(
                        period,
                        absolute
                            ? PathRatioConvention.AbsoluteFraction
                            : PathRatioConvention.SignedPercent
                    )
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void FlatWindowsAreAbsentButZeroNetMovementIsPresent()
    {
        var data = CompetitorData.FromCloses([0, 1, 0, 0, 0, 1]);
        foreach (var pair in PathRatioComparison.Pairs)
        {
            ComparisonVerifier.Check(pair, data, 2);
            var output = pair.Ooples(data, 2).Outputs["Value"];
            Assert.Equal(new[] { false, false, true, true, false, true }, output.Present);
            Assert.Equal(0, output.Values[2]);
            Assert.Equal(PathRatioComparison.Absolute(pair.Id) ? 1 : -100, output.Values[3]);
            ComparisonVerifier.Check(pair, PathRatioComparison.Fixture(), int.MaxValue);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowPathRatio(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowPathRatio(1, (PathRatioConvention)100)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetCmo(0).ToArray());
    }

    [Fact]
    public async Task ExactMovementRatiosSurviveOverflowAndSubnormals()
    {
        foreach (var absolute in new[] { false, true })
        foreach (var scale in new[] { double.MaxValue, double.Epsilon })
        {
            var indicator = new WindowPathRatio(
                2,
                absolute ? PathRatioConvention.AbsoluteFraction : PathRatioConvention.SignedPercent
            );
            var result = await VolumePriceComparisonTests.Run(
                indicator,
                (-scale, -scale, -scale, 1),
                (scale, scale, scale, 1),
                (-scale, -scale, -scale, 1),
                (0, 0, 0, 1)
            );
            Assert.Equal(new[] { 0d, 0, 1, 1 }, result[1]);
            Assert.Equal(0, result[0][2]);
            Assert.Equal(absolute ? 1d / 3 : -100d / 3, result[0][3]);
        }
        var tiny = await VolumePriceComparisonTests.Run(
            new WindowPathRatio(1),
            (1, 1, 1, 1),
            (Math.BitIncrement(1), Math.BitIncrement(1), Math.BitIncrement(1), 1)
        );
        Assert.Equal(100, tiny[0][1]);
        var native = new[]
        {
            (DateTime.UnixEpoch, -double.MaxValue),
            (DateTime.UnixEpoch.AddDays(1), double.MaxValue),
        }
            .GetCmo(1)
            .ToArray();
        Assert.Null(native[1].Cmo);
    }

    [Fact]
    public void NullableEfficiencySkipsUnknownStepsAndCanExceedOne()
    {
        decimal?[] input = [1, null, 9, 10, 10, null, 11, 12, 12, 12];
        var values = input.Select(v => (double?)v).ToArray();
        var native = new T.EfficiencyRatioByTuple(input, 3);
        var expected = native.Compute().Select(v => (double?)v).ToArray();
        Assert.Equal(9, expected[3]);
        Assert.Equal(expected, WindowPathRatio.FromValues(values, 3).ToArray());
        Assert.Equal(expected, PathRatioComparison.NullableReference(values, 3, true));
        var generic = new T.EfficiencyRatio<int, decimal?>(
            Enumerable.Range(0, input.Length),
            i => input[i],
            3
        );
        Assert.Equal(native.Compute(), generic.Compute());
        Assert.Equal(
            expected.Skip(3).Take(4),
            native.Compute(startIndex: 3, endIndex: 6).Select(v => (double?)v)
        );
        Assert.Equal(
            new[] { expected[7], expected[3], expected[7] },
            native.Compute((IEnumerable<int>)new[] { 7, 3, 7 }).Select(v => (double?)v)
        );
        Assert.Equal(expected[3], (double?)native.Compute(3, 6));
        Assert.Equal(expected, native.Compute().Select(v => (double?)v));
        Assert.All(new T.EfficiencyRatioByTuple(input, 0).Compute(), v => Assert.Null(v));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new T.EfficiencyRatioByTuple(input, -1).Compute()
        );
        foreach (var absolute in new[] { false, true })
        {
            var mode = absolute
                ? PathRatioConvention.AbsoluteFraction
                : PathRatioConvention.SignedPercent;
            Assert.Equal(
                PathRatioComparison.NullableReference(values, 3, absolute),
                WindowPathRatio.FromValues(values, 3, mode)
            );
        }
    }

    [Fact]
    public void NullableRoutesHaveFreshEnumerationsAndExplicitFailures()
    {
        double?[] values = [1, null, 9, 10, null, 0, 1, 2, 3, 3, 3];
        var enumerable = WindowPathRatio.FromValues(values, 3);
        Assert.Equal(enumerable.ToArray(), enumerable.ToArray());
        using var first = enumerable.GetEnumerator();
        using var second = enumerable.GetEnumerator();
        for (var i = 0; i < 4; i++)
            Assert.True(first.MoveNext());
        Assert.Equal(9, first.Current);
        Assert.True(second.MoveNext());
        Assert.Null(second.Current);
        Assert.Empty(WindowPathRatio.FromValues([]));
        Assert.Null(WindowPathRatio.FromValues([1], 1).Single());
        Assert.Throws<ArgumentNullException>(() => WindowPathRatio.FromValues(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowPathRatio.FromValues(values, 0));
        foreach (
            var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WindowPathRatio.FromValues([1, value], 1).ToArray()
            );
        Assert.Throws<ArithmeticException>(() =>
            WindowPathRatio.FromValues([double.MaxValue, null, 0, double.Epsilon], 3).ToArray()
        );
    }

    [Fact]
    public void SkenderQuoteTupleReusableSortingAndRecoveryRoutesAgree()
    {
        var data = PathRatioComparison.Fixture();
        var expected = data.Quotes.GetCmo(3).ToArray();
        var tuples = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var reusable = tuples
            .Select(q => new AwesomeResult(q.Date) { Oscillator = q.Item2 })
            .Cast<IReusableResult>();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetCmo(3).ToArray(),
                reusable.GetCmo(3).ToArray(),
                data.Quotes.AsEnumerable().Reverse().GetCmo(3).ToArray(),
            }
        )
        {
            Assert.Equal(expected.Select(r => r.Date), rows.Select(r => r.Date));
            Assert.Equal(expected.Select(r => r.Cmo), rows.Select(r => r.Cmo));
        }
        Assert.Equal(expected[3].Cmo, ((IReusableResult)expected[3]).Value);
    }

    [Fact]
    public async Task ChainingUsesTheSelectedClose()
    {
        var data = PathRatioComparison.Fixture();
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new WindowPathRatio(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(source, indicator)
            .BuildAsync();
        var prices = run[source.Value].ToArray();
        var expected = PathRatioComparison.NullableReference(
            prices.Select(v => (double?)v).ToArray(),
            3,
            false
        );
        var actual = run[indicator.Value].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expected[i].HasValue, flags[i] > 0);
            if (flags[i] > 0)
                Assert.Equal(expected[i]!.Value, actual[i]);
        }
    }

    [Fact]
    public void ValueAndPresenceCorruptionAreRejected()
    {
        var data = PathRatioComparison.Fixture();
        foreach (var pair in PathRatioComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs["Value"];
                if (presence)
                    output.Present![^1] = false;
                else
                    output.Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
    }
}
