using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SampleShapeComparisonTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(false, 20)]
    [InlineData(true, 4)]
    [InlineData(true, 20)]
    public async Task IndependentLifecycleContracts(bool kurtosis, int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowSampleShape),
                "sample shape",
                () => new WindowSampleShape(period, kurtosis)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownCoefficientsStartupAndFlatRecovery()
    {
        var skew = SampleShapeComparison.Create(false);
        Assert.Equal(
            new[] { 0d, 0, Math.Sqrt(3) },
            skew.Ooples(CompetitorData.FromCloses([0, 0, 1]), 3).Outputs["Value"].Values
        );
        var kurt = SampleShapeComparison.Create(true);
        Assert.Equal(
            new[] { 0d, 0, 0, -6 },
            kurt.Ooples(CompetitorData.FromCloses([-1, 1, -1, 1]), 4).Outputs["Value"].Values
        );
        Assert.Equal(
            4,
            kurt.Ooples(CompetitorData.FromCloses([0, 0, 0, 1]), 4).Outputs["Value"].Values[3]
        );
        var flat = CompetitorData.FromCloses([3, 3, 3, 3, 4, 3, 3, 3, 3]);
        var output = kurt.Ooples(flat, 4).Outputs["Value"];
        Assert.Equal(
            new[] { true, true, true, false, true, true, true, true, false },
            output.Present
        );
        Assert.True(double.IsNaN(output.Values[3]));
        Assert.Equal(
            new double[flat.Count],
            skew.Ooples(CompetitorData.FromCloses(Enumerable.Repeat(3d, flat.Count).ToArray()), 4)
                .Outputs["Value"]
                .Values
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TinyWideAdjacentPricesAndMaximumPeriodsUseExactMoments(bool kurtosis)
    {
        foreach (
            var prices in new[]
            {
                new[] { 0d, 0, 0, double.Epsilon },
                new[] { double.MaxValue, -double.MaxValue, 0, double.MaxValue / 2 },
                new[]
                {
                    double.MaxValue,
                    Math.BitDecrement(double.MaxValue),
                    Math.BitDecrement(Math.BitDecrement(double.MaxValue)),
                    double.MaxValue,
                },
                new[] { 1d, Math.BitIncrement(1), 1, 1 },
                new[] { -3d, 8, -2, 9, 4, 4, -1, 2 },
            }
        )
        foreach (var period in new[] { 4, int.MaxValue })
        {
            var indicator = new WindowSampleShape(period, kurtosis);
            var actual = await VolumePriceComparisonTests.Run(
                indicator,
                prices.Select(v => (v, v, v, 0d)).ToArray()
            );
            var expected = SampleShapeComparison.Reference(prices, period, kurtosis);
            Assert.Equal(expected.Select(v => v ?? 0), actual[0]);
            Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0), actual[1]);
        }
        var data = SampleShapeComparison.Fixture();
        foreach (var period in new[] { kurtosis ? 4 : 3, 9, 20 })
            ComparisonVerifier.Check(SampleShapeComparison.Create(kurtosis), data, period);
    }

    [Fact]
    public void NativeFlatAndExtremeFailuresRemainVisible()
    {
        var flat = CompetitorData.FromCloses([3, 3, 3, 3, 3]);
        var pair = SampleShapeComparison.Create(true);
        var native = pair.Competitor(flat, 4).Outputs["Value"];
        Assert.Null(native.Present);
        Assert.True(double.IsNaN(native.Values[3]));
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(pair, flat, 4));
        foreach (var kurtosis in new[] { false, true })
        {
            var indicator = SampleShapeComparison.NativeIndicator(4, kurtosis);
            foreach (
                var value in new[] { double.MaxValue, -double.MaxValue, 0, double.MaxValue / 2 }
            )
                indicator.Calc(new QuanTAlib.TValue(value, true, false));
            Assert.False(double.IsFinite(indicator.Value));
            var tiny = SampleShapeComparison.NativeIndicator(4, kurtosis);
            foreach (var value in new[] { 0d, 0, 0, double.Epsilon })
                tiny.Calc(new QuanTAlib.TValue(value, true, false));
            if (kurtosis)
                Assert.True(double.IsNaN(tiny.Value));
            else
                Assert.Equal(0, tiny.Value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubscriptionRevisionResetAndCloseChaining(bool kurtosis)
    {
        var source = new QuanTAlib.TSeries();
        QuanTAlib.AbstractBase subscribed = kurtosis
            ? new QuanTAlib.Kurtosis(source, 4)
            : new QuanTAlib.Skew(source, 4);
        var direct = SampleShapeComparison.NativeIndicator(4, kurtosis);
        foreach (var value in new[] { 1d, 5, 2, 4, 9 })
        {
            var tick = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(tick).Value;
            source.Add(tick);
            Assert.Equal(expected, subscribed.Value);
        }
        var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
        var replay = SampleShapeComparison.NativeIndicator(4, kurtosis);
        foreach (var value in new[] { 1d, 5, 2, 4, 7 })
            replay.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(replay.Value, revised);
        direct.Init();
        var fresh = SampleShapeComparison.NativeIndicator(4, kurtosis);
        foreach (var value in new[] { 8d, 3, 9, 2, 5 })
            Assert.Equal(
                fresh.Calc(new QuanTAlib.TValue(value, true, false)).Value,
                direct.Calc(new QuanTAlib.TValue(value, true, false)).Value
            );
        var prices = new[] { 1d, 8, -3, 7, 2, 9 };
        var indicator = new WindowSampleShape(4, kurtosis);
        indicator.Of(new FixedPeriodWma(2));
        var actual = await VolumePriceComparisonTests.Run(
            indicator,
            prices.Select(v => (v, v, v, 0d)).ToArray()
        );
        Assert.Equal(
            SampleShapeComparison
                .Reference(FixedWeightedComparison.Stage(prices, 2, false), 4, kurtosis)
                .Select(v => v ?? 0),
            actual[0]
        );
    }

    [Fact]
    public void InvalidPeriodsAndEveryValuePresenceMutationAreRejected()
    {
        foreach (var kurtosis in new[] { false, true })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WindowSampleShape(kurtosis ? 3 : 2, kurtosis)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SampleShapeComparison.NativeIndicator(kurtosis ? 3 : 2, kurtosis)
            );
            var pair = SampleShapeComparison.Create(kurtosis);
            foreach (var native in new[] { false, true })
            foreach (var startup in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int period)
                {
                    var values = (
                        native ? pair.Competitor(data, period) : pair.Ooples(data, period)
                    )
                        .Outputs["Value"]
                        .Values;
                    if (!startup)
                        values[^1] += 1;
                    return new(startup ? 1 : 0, values);
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        SampleShapeComparison.Fixture(),
                        4
                    )
                );
            }
            ComparisonSeries BadPresence(CompetitorData data, int period)
            {
                var result = pair.Ooples(data, period);
                result.Outputs["Value"].Present![^1] = false;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = BadPresence,
                    },
                    SampleShapeComparison.Fixture(),
                    4
                )
            );
        }
    }
}
