using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ExpandingRecurrenceComparisonTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, .5)]
    [InlineData(3, .5)]
    [InlineData(20, 2)]
    [InlineData(int.MaxValue, .5)]
    [InlineData(3, double.Epsilon)]
    [InlineData(3, double.MaxValue)]
    public async Task RegularizedContractVerifiesCompleteRatioAndLifecycle(
        int period,
        double lambda
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ExpandingRegularizedEma),
                "expanding regularization",
                () => new ExpandingRegularizedEma(period, lambda)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task ZeroLagContractVerifiesAvailableLagAndLifecycle(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ExpandingZeroLagEma),
                "expanding zero-lag",
                () => new ExpandingZeroLagEma(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void StartupAndPeriodOneAreExplicit()
    {
        var data = CompetitorData.FromCloses([1, 2, 10]);
        var regular = ExpandingRecurrenceComparison.Create(true);
        foreach (var native in new[] { false, true })
        {
            var result = (native ? regular.Competitor(data, 1) : regular.Ooples(data, 1))
                .Outputs["Value"]
                .Values;
            Assert.Equal(new[] { 1d, 2, 23d / 3 }, result);
        }
        Assert.Equal(
            data.Closes,
            ExpandingRecurrenceComparison.Create(false).Ooples(data, 1).Outputs["Value"].Values
        );
        var fixture = CompetitorData.FromCloses([1, 4, 2, 8]);
        Assert.Equal(
            new[] { 1d, 5, 2.5, 7.1 },
            ExpandingRecurrenceComparison.Create(false).Ooples(fixture, 4).Outputs["Value"].Values
        );
        Assert.Equal(
            new[] { 1d, 5, 2.5, 8.25 },
            ExpandingRecurrenceComparison.Create(false).Ooples(fixture, 3).Outputs["Value"].Values
        );
    }

    [Fact]
    public async Task WideAndSubnormalInputsPreserveFiniteCompleteUpdates()
    {
        foreach (var regularized in new[] { false, true })
        foreach (var magnitude in new[] { double.MaxValue, double.Epsilon })
        {
            var prices = Enumerable.Repeat(magnitude, 6).ToArray();
            IIndicator indicator = regularized
                ? new ExpandingRegularizedEma(3)
                : new ExpandingZeroLagEma(3);
            var actual = await VolumePriceComparisonTests.Run(
                indicator,
                prices.Select(x => (x, x, x, 0d)).ToArray()
            );
            Assert.Equal(prices, actual[0]);
            var lazy = regularized
                ? (IIndicator)new ExpandingRegularizedEma(int.MaxValue)
                : new ExpandingZeroLagEma(int.MaxValue);
            Assert.Equal(
                prices,
                (
                    await VolumePriceComparisonTests.Run(
                        lazy,
                        prices.Select(x => (x, x, x, 0d)).ToArray()
                    )
                )[0]
            );
        }
        var huge = await VolumePriceComparisonTests.Run(
            new ExpandingRegularizedEma(3, double.MaxValue),
            (1, 1, 1, 0),
            (2, 2, 2, 0),
            (3, 3, 3, 0)
        );
        Assert.Equal(new[] { 1d, 2, 3 }, huge[0]);
        var native = new QuanTAlib.Rema(3, double.MaxValue);
        native.Calc(new QuanTAlib.TValue(1, true, false));
        native.Calc(new QuanTAlib.TValue(2, true, false));
        Assert.False(double.IsFinite(native.Calc(new QuanTAlib.TValue(3, true, false)).Value));
        var nativeLag = new QuanTAlib.Zlema(3);
        Assert.False(
            double.IsFinite(
                nativeLag.Calc(new QuanTAlib.TValue(double.MaxValue, true, false)).Value
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenuineFinalOverflowIsRejected(bool regularized)
    {
        IIndicator indicator = regularized
            ? new ExpandingRegularizedEma(3)
            : new ExpandingZeroLagEma(3);
        double[] prices = regularized
            ? [-double.MaxValue, double.MaxValue, double.MaxValue]
            : [-double.MaxValue, double.MaxValue];
        var expected = ExpandingRecurrenceComparison.Reference(prices, 3, regularized, .5, false);
        Assert.True(double.IsPositiveInfinity(expected[^1]));
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(indicator, prices.Select(x => (x, x, x, 0d)).ToArray())
        );
    }

    [Fact]
    public void NativeSourceRevisionAndResetAgreeWithFreshReplay()
    {
        foreach (var regularized in new[] { false, true })
        foreach (var period in new[] { 1, 3, 4, 20 })
        {
            QuanTAlib.AbstractBase Make() =>
                regularized ? new QuanTAlib.Rema(period) : new QuanTAlib.Zlema(period);
            var source = new QuanTAlib.TSeries();
            QuanTAlib.AbstractBase subscribed = regularized
                ? new QuanTAlib.Rema(source, period)
                : new QuanTAlib.Zlema(source, period);
            var direct = Make();
            var values = ExpandingRecurrenceComparison.Fixture().Closes;
            foreach (var value in values)
            {
                var input = new QuanTAlib.TValue(value, true, false);
                var expected = direct.Calc(input).Value;
                source.Add(input);
                Assert.Equal(expected, subscribed.Value);
            }
            var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
            var fresh = Make();
            foreach (var value in values.SkipLast(1).Append(7))
                fresh.Calc(new QuanTAlib.TValue(value, true, false));
            Assert.Equal(fresh.Value, revised);
            direct.Init();
            Assert.Equal(7, direct.Calc(new QuanTAlib.TValue(7, true, false)).Value);
            Assert.Equal(period, direct.WarmupPeriod);
        }
        var maximum = new QuanTAlib.Rema(int.MaxValue);
        Assert.Equal(int.MaxValue, maximum.Period);
        Assert.Equal(.5, maximum.Lambda);
        foreach (var value in new[] { 1d, 2, 3, 4 })
            Assert.True(
                double.IsFinite(maximum.Calc(new QuanTAlib.TValue(value, true, false)).Value)
            );
    }

    [Fact]
    public void ParametersAndCorruptedOutputsAreChecked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpandingRegularizedEma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpandingZeroLagEma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Rema(0));
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Zlema(0));
        foreach (
            var lambda in new[]
            {
                -1d,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ExpandingRegularizedEma(3, lambda)
            );
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity })
        {
            var accepted = new QuanTAlib.Rema(3, invalid);
            accepted.Calc(new QuanTAlib.TValue(1, true, false));
            accepted.Calc(new QuanTAlib.TValue(2, true, false));
            Assert.True(double.IsNaN(accepted.Calc(new QuanTAlib.TValue(3, true, false)).Value));
        }
        foreach (var pair in ExpandingRecurrenceComparison.Pairs)
        {
            var data = ExpandingRecurrenceComparison.Fixture();
            ComparisonVerifier.Check(pair, data, 3);
            foreach (var native in new[] { false, true })
            foreach (var alignment in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData input, int period)
                {
                    var values = (
                        native ? pair.Competitor(input, period) : pair.Ooples(input, period)
                    )
                        .Outputs["Value"]
                        .Values;
                    if (alignment)
                        return new(1, values);
                    values[0] += 1;
                    return new(0, values);
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
}
