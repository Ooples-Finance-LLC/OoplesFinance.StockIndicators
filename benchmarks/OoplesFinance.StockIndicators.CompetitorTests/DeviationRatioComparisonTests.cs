using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DeviationRatioComparisonTests
{
    [Theory]
    [InlineData(3, 12, .2)]
    [InlineData(5, 3, .5)]
    public async Task RationalContractCoversLifecycle(int shortPeriod, int longPeriod, double alpha)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(DeviationRatioAdaptiveAverage),
                "deviation ratio average",
                () => new DeviationRatioAdaptiveAverage(shortPeriod, longPeriod, alpha)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void ConfigurationsStartupAndUndefinedRecurrenceMatchIndependentReferences()
    {
        foreach (
            var c in new[]
            {
                (1, 4, .2),
                (2, 0, .2),
                (5, 3, .5),
                (3, 12, 0d),
                (3, 12, -.1),
                (3, 12, 1.2),
            }
        )
        {
            var pair = DeviationRatioComparison.Pair(c.Item2, c.Item3);
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), c.Item1);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 100), c.Item1);
        }
        var values = DeviationRatioComparison
            .Owned(CompetitorData.FromCloses([1, 3, 6, 10, 15]).IndicatorBars, 2, 4, .2)
            .Outputs["Value"];
        Assert.Equal(new[] { 1d, 2, 4.5, 8 }, values.Values.Take(4));
        Assert.True(values.Present![4]);
        var flat = new[] { 7d, 7, 7, 7, 7, 8, 9, 11 };
        var raw = DeviationRatioComparison.Native(flat, 2, 4, .2);
        Assert.Equal(DeviationRatioComparison.NativeReference(flat, 2, 4, .2), raw);
        Assert.All(raw.Skip(4), v => Assert.True(double.IsNaN(v)));
        var owned = DeviationRatioComparison
            .Owned(CompetitorData.FromCloses(flat).IndicatorBars, 2, 4, .2)
            .Outputs["Value"];
        Assert.Equal(new[] { true, true, true, true, false, false, false, false }, owned.Present);
    }

    [Fact]
    public void NativeSourceRevisionsAndRetainedBuffersAreExplicit()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Vidya(source, 3, 12, .2);
        var direct = new QuanTAlib.Vidya(3, 12, .2);
        var prices = CompetitorData.Create(40).Closes;
        for (var i = 0; i < prices.Length; i++)
        {
            var input = new QuanTAlib.TValue(prices[i], true, false);
            source.Add(input);
            Assert.Equal(direct.Calc(input).Value, subscribed.Value);
            Assert.Equal(i >= 11, direct.IsHot);
        }
        var revision = new QuanTAlib.TValue(121, false, false);
        source.Add(revision);
        direct.Calc(revision);
        Assert.Equal(direct.Value, subscribed.Value);
        Assert.Equal(
            DeviationRatioComparison.NativeReference(
                prices.Take(39).Append(121).ToArray(),
                3,
                12,
                .2
            )[^1],
            direct.Value
        );
        direct.Init();
        var expected = (prices[38] + 121 + 17) / 3;
        Assert.Equal(expected, direct.Calc(new QuanTAlib.TValue(17, true, false)).Value);
        Assert.NotEqual(17, direct.Value);
    }

    [Fact]
    public void ExtremeAndSubnormalMomentsMatchIndependentGridWithoutEagerLargeWindows()
    {
        foreach (
            var values in new[]
            {
                Enumerable
                    .Range(0, 20)
                    .Select(i => i % 2 == 0 ? double.MaxValue : -double.MaxValue)
                    .ToArray(),
                Enumerable.Range(0, 20).Select(i => (i % 3 - 1) * double.Epsilon).ToArray(),
                Enumerable.Range(0, 20).Select(i => 1e15 + (i % 5)).ToArray(),
            }
        )
        {
            var bars = values
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            ComparisonVerifier.Compare(
                DeviationRatioComparison.Series(
                    DeviationRatioComparison.GridReference(values, 2, 5, .2)
                ),
                DeviationRatioComparison.Owned(bars, 2, 5, .2),
                "extreme deviations",
                IndicatorErrorBudget.Exact
            );
        }
        var small = CompetitorData.FromCloses([1, 3, 8]);
        Assert.Equal(
            new[] { 1d, 2, 4 },
            DeviationRatioComparison
                .Owned(small.IndicatorBars, int.MaxValue, int.MaxValue, .2)
                .Outputs["Value"]
                .Values
        );
        var huge = Enumerable.Repeat(double.MaxValue, 8).ToArray();
        var raw = DeviationRatioComparison.Native(huge, 2, 4, .2);
        Assert.Contains(raw, v => !double.IsFinite(v));
        Assert.Equal(DeviationRatioComparison.NativeReference(huge, 2, 4, .2), raw);
    }

    [Fact]
    public void UnrepresentableDeviationRatioStillAllowsZeroGain()
    {
        var values = new[]
        {
            double.MaxValue,
            -double.MaxValue,
            double.Epsilon,
            2 * double.Epsilon,
            3 * double.Epsilon,
        };
        var bars = values
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        var actual = DeviationRatioComparison.Owned(bars, 5, 2, 0);
        Assert.Equal(new[] { double.MaxValue, 0, 0, 0, 0 }, actual.Outputs["Value"].Values);
        ComparisonVerifier.Compare(
            DeviationRatioComparison.Series(
                DeviationRatioComparison.GridReference(values, 5, 2, 0)
            ),
            actual,
            "extended ratio",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public async Task ChainingAndOutputPresenceGainMutationsAreVerified()
    {
        var d = CompetitorData.Create(45);
        var prices = FixedWeightedComparison.Stage(d.Closes, 3, false);
        var indicator = new DeviationRatioAdaptiveAverage(3, 12, .2);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = DeviationRatioComparison.GridReference(prices, 3, 12, .2);
        Assert.Equal(
            expected.Select(v => double.IsNaN(v) ? 0 : v),
            run[indicator.Average].ToArray()
        );
        Assert.Equal(
            expected.Select(v => double.IsNaN(v) ? 0d : 1d),
            run[indicator.IsDefined].ToArray()
        );
        var pair = DeviationRatioComparison.Pair(12, .2);
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (mask)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    d,
                    3
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = DeviationRatioComparison.Pair(12, .4).Library,
                },
                d,
                3
            )
        );
    }

    [Fact]
    public void InvalidConfigurationIsRejectedBeforeAllocation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeviationRatioAdaptiveAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeviationRatioAdaptiveAverage(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeviationRatioAdaptiveAverage(int.MaxValue)
        );
        foreach (
            var alpha in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DeviationRatioAdaptiveAverage(alpha: alpha)
            );
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Vidya(0));
        var native = new QuanTAlib.Vidya(1, 0, double.NaN);
        for (var i = 0; i < 5; i++)
            native.Calc(new QuanTAlib.TValue(i, true, false));
        Assert.True(double.IsNaN(native.Value));
    }
}
