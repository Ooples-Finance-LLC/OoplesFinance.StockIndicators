using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class HoltWinterComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task DefaultFactorsAndLifecycleAreIndependentlyVerified(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(HoltWinterForecast),
                "Holt-Winter forecast",
                () => new HoltWinterForecast(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(.5, .2, .1)]
    [InlineData(1, 1, 1)]
    [InlineData(-.1, 0, 0)]
    [InlineData(2, 0, 0)]
    public async Task ExplicitFactorsHaveIndependentStageContracts(double a, double b, double c)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(HoltWinterForecast),
                "explicit Holt-Winter factors",
                () => new HoltWinterForecast(3, a, b, c)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void CoefficientConstructorsPeriodOneAndZeroFactorsAreExplicit()
    {
        var data = HoltWinterComparison.Fixture();
        var derived = new HoltWinterForecast(.5, .2, .1);
        Assert.Equal(3, derived.Period);
        Assert.Equal(.5, derived.LevelFactor);
        Assert.Equal(.2, derived.TrendFactor);
        Assert.Equal(.1, derived.AccelerationFactor);
        ComparisonVerifier.Check(HoltWinterComparison.Create([.5, .2, .1], true), data, 20);
        Assert.Equal(
            data.Closes,
            HoltWinterComparison.Create([.2, -.1, 2]).Ooples(data, 1).Outputs["Value"].Values
        );
        Assert.Equal(
            data.Closes,
            HoltWinterComparison.Create([.8, .2, .1], true).Ooples(data, 20).Outputs["Value"].Values
        );
        Assert.All(
            HoltWinterComparison.Create([0, 0, 0]).Ooples(data, 3).Outputs["Value"].Values,
            v => Assert.Equal(1, v)
        );
        var nearlyMaximum = 2d / (int.MaxValue + 1.5);
        Assert.Equal(int.MaxValue, new HoltWinterForecast(nearlyMaximum, .1, .1).Period);
        Assert.Equal(int.MaxValue, new QuanTAlib.Hwma(nearlyMaximum, .1, .1).WarmupPeriod);
        ComparisonVerifier.Check(HoltWinterComparison.Pair, data, int.MaxValue);
    }

    [Fact]
    public async Task FiniteExtremeSeedsUpdatesAndSubnormalHalfTermsArePreserved()
    {
        foreach (var magnitude in new[] { double.MaxValue, double.Epsilon })
        {
            var constant = Enumerable.Repeat(magnitude, 5).ToArray();
            var actual = await Run(new HoltWinterForecast(3), constant);
            Assert.Equal(constant, actual);
            double[] prices = [magnitude / 4, magnitude / 2, -magnitude / 4, 0, magnitude / 4];
            Assert.Equal(
                HoltWinterComparison.Reference(prices, 3, null, false),
                await Run(new HoltWinterForecast(3), prices)
            );
        }
        double[] tiny =
        [
            double.Epsilon,
            3 * double.Epsilon,
            -2 * double.Epsilon,
            5 * double.Epsilon,
            0,
        ];
        Assert.Equal(
            HoltWinterComparison.Reference(tiny, 3, [.5, .5, .5], false),
            await Run(new HoltWinterForecast(3, .5, .5, .5), tiny)
        );
        Assert.Equal(
            new[] { -double.MaxValue, double.MaxValue },
            await Run(new HoltWinterForecast(1), [-double.MaxValue, double.MaxValue])
        );
        var native = new QuanTAlib.Hwma(1);
        native.Calc(new QuanTAlib.TValue(-double.MaxValue, true, false));
        Assert.False(
            double.IsFinite(native.Calc(new QuanTAlib.TValue(double.MaxValue, true, false)).Value)
        );
    }

    [Fact]
    public async Task OversizedUnpublishedStagesCancelToFiniteForecast()
    {
        double[] prices = [0, double.MaxValue];
        var expected = HoltWinterComparison.Reference(prices, 3, [2, -1, 0], false);
        Assert.Equal(new double[2], expected);
        Assert.Equal(expected, await Run(new HoltWinterForecast(3, 2, -1, 0), prices));
        var native = new QuanTAlib.Hwma(3, 2, -1, 0);
        native.Calc(new QuanTAlib.TValue(0, true, false));
        Assert.False(
            double.IsFinite(native.Calc(new QuanTAlib.TValue(double.MaxValue, true, false)).Value)
        );
    }

    [Fact]
    public async Task TrueFinalOverflowIsRejected()
    {
        double[] prices = [0, double.MaxValue];
        Assert.True(
            double.IsPositiveInfinity(
                HoltWinterComparison.Reference(prices, 3, [1, 1, 1], false)[1]
            )
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(new HoltWinterForecast(3, 1, 1, 1), prices)
        );
    }

    private static async Task<double[]> Run(HoltWinterForecast indicator, double[] prices) =>
        (
            await VolumePriceComparisonTests.Run(
                indicator,
                prices.Select(x => (x, x, x, 0d)).ToArray()
            )
        )[0];

    [Fact]
    public void SourceRevisionResetAndParameterDomainsArePinned()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Hwma(source, 3);
        var direct = new QuanTAlib.Hwma(3);
        var values = HoltWinterComparison.Fixture().Closes;
        foreach (var value in values)
        {
            var input = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(input).Value;
            source.Add(input);
            Assert.Equal(expected, subscribed.Value);
        }
        var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
        var fresh = new QuanTAlib.Hwma(3);
        foreach (var value in values.SkipLast(1).Append(7))
            fresh.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(fresh.Value, revised);
        direct.Init();
        Assert.Equal(
            new QuanTAlib.Hwma(3).Calc(new QuanTAlib.TValue(7, true, false)).Value,
            direct.Calc(new QuanTAlib.TValue(7, true, false)).Value
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoltWinterForecast(0));
        foreach (
            var invalid in new[] { 0d, -1, 2, double.Epsilon, double.NaN, double.PositiveInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HoltWinterForecast(invalid, .1, .1)
            );
        foreach (
            var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HoltWinterForecast(3, invalid, .1, .1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HoltWinterForecast(3, .1, invalid, .1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HoltWinterForecast(3, .1, .1, invalid)
            );
        }
        Assert.True(
            double.IsNaN(
                new QuanTAlib.Hwma(3, double.NaN, .1, .1)
                    .Calc(new QuanTAlib.TValue(1, true, false))
                    .Value
            )
        );
    }

    [Fact]
    public void IndependentReferencesRejectWrongSeedFactorsAndAlignment()
    {
        var data = HoltWinterComparison.Fixture();
        var pair = HoltWinterComparison.Pair;
        ComparisonVerifier.Check(pair, data, 3);
        foreach (var native in new[] { false, true })
        foreach (var alignment in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData input, int period)
            {
                var values = (native ? pair.Competitor(input, period) : pair.Ooples(input, period))
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
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = HoltWinterComparison.Create([0, 0, 0]).Library,
                },
                data,
                3
            )
        );
    }
}
