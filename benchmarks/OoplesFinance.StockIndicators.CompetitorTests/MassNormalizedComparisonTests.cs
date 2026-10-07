using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MassNormalizedComparisonTests
{
    [Theory]
    [InlineData(false, .2)]
    [InlineData(true, .2)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, double.Epsilon)]
    [InlineData(true, double.Epsilon)]
    public async Task IndependentLifecycleContracts(bool quadruple, double alpha)
    {
        var alphas = Enumerable.Repeat(alpha, quadruple ? 4 : 1).ToArray();
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                quadruple ? typeof(MassNormalizedQuadrupleEma) : typeof(MassNormalizedEma),
                "mass normalized EMA",
                () => MassNormalizedComparison.Indicator(3, alphas)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void NativeConfigurationsAndCutoffTransitionsHaveIndependentReferences()
    {
        foreach (var period in new[] { 1, 2, 3, 20 })
            ComparisonVerifier.Check(
                MassNormalizedComparison.Create(),
                CompensatedAverageComparison.Fixture(),
                period
            );
        foreach (
            var alphas in new[]
            {
                new[] { 1d },
                new[] { .2 },
                new[] { .01 },
                new[] { .1, .3, .7, .9 },
                new[] { .9, .7, .3, .1 },
                new[] { 1d, 1, 1, 1 },
            }
        )
        {
            var pair = MassNormalizedComparison.Create(alphas);
            ComparisonVerifier.Check(pair, CompensatedAverageComparison.Fixture(), 3);
            ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture("walk", 2400), 3);
        }
        var data = CompensatedAverageComparison.Fixture();
        Assert.Equal(
            data.Closes,
            MassNormalizedComparison.Create([1]).Ooples(data, 3).Outputs["Value"].Values
        );
        Assert.Equal(
            data.Closes,
            MassNormalizedComparison.Create([1, 1, 1, 1]).Ooples(data, 3).Outputs["Value"].Values
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TinyAlphaAndExtremePricesRemainFinite(bool quadruple)
    {
        foreach (var alpha in new[] { double.Epsilon, 1e-100, .2, 1 })
        foreach (
            var price in new[]
            {
                double.Epsilon,
                -double.Epsilon,
                double.MaxValue,
                -double.MaxValue,
            }
        )
        {
            var alphas = Enumerable.Repeat(alpha, quadruple ? 4 : 1).ToArray();
            var indicator = MassNormalizedComparison.Indicator(3, alphas);
            var actual = await VolumePriceComparisonTests.Run(
                indicator,
                (price, price, price, 0),
                (price, price, price, 0),
                (price, price, price, 0)
            );
            Assert.Equal(new[] { price, price, price }, actual[0]);
        }
        foreach (
            var alphas in quadruple
                ? new[] { new[] { double.Epsilon, .5, 1e-100, .2 } }
                : new[] { new[] { double.Epsilon } }
        )
        {
            var prices = new[] { 1d, -1, 3, 0, -4 };
            var actual = await VolumePriceComparisonTests.Run(
                MassNormalizedComparison.Indicator(3, alphas),
                prices.Select(v => (v, v, v, 0d)).ToArray()
            );
            Assert.Equal(MassNormalizedComparison.Reference(prices, alphas, false), actual[0]);
        }
        var lazy = await VolumePriceComparisonTests.Run(
            new MassNormalizedEma(int.MaxValue),
            (3, 3, 3, 0),
            (3, 3, 3, 0)
        );
        Assert.Equal(new[] { 3d, 3 }, lazy[0]);
    }

    [Fact]
    public void NativeTinyAlphaAndInvalidParameterHolesStayVisible()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MassNormalizedEma(0));
        foreach (
            var alpha in new[]
            {
                0d,
                -1,
                1.01,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            }
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MassNormalizedEma(alpha));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MassNormalizedQuadrupleEma(alpha1: alpha)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MassNormalizedQuadrupleEma(alpha2: alpha)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MassNormalizedQuadrupleEma(alpha3: alpha)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MassNormalizedQuadrupleEma(alpha4: alpha)
            );
        }
        foreach (
            var alphas in new[]
            {
                new[] { double.Epsilon },
                new[] { double.Epsilon, .2, .2, .2 },
                new[] { double.NaN },
                new[] { double.PositiveInfinity },
            }
        )
        {
            var native = MassNormalizedComparison.NativeIndicator(3, alphas);
            Assert.False(double.IsFinite(native.Calc(new QuanTAlib.TValue(1, true, false)).Value));
        }
        // Native alpha outside its documented QEMA domain is accepted.
        Assert.NotNull(new QuanTAlib.Ema(-1d));
        Assert.NotNull(new QuanTAlib.Qema(2, 2, 2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Qema(0, .2, .2, .2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeSubscriptionRevisionAndResetContract(bool quadruple)
    {
        var source = new QuanTAlib.TSeries();
        var alphas = quadruple ? new[] { .1, .3, .7, .9 } : null;
        QuanTAlib.AbstractBase subscribed = quadruple
            ? new QuanTAlib.Qema(source, .1, .3, .7, .9)
            : new QuanTAlib.Ema(source, 3, false);
        var direct = MassNormalizedComparison.NativeIndicator(3, alphas);
        foreach (var value in new[] { 1d, 5, 2, 4 })
        {
            var tick = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(tick).Value;
            source.Add(tick);
            Assert.Equal(expected, subscribed.Value);
        }
        var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
        var replay = MassNormalizedComparison.NativeIndicator(3, alphas);
        foreach (var value in new[] { 1d, 5, 2, 7 })
            replay.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(replay.Value, revised);
        direct.Init();
        var fresh = MassNormalizedComparison.NativeIndicator(3, alphas);
        var input = new QuanTAlib.TValue(20, true, false);
        if (quadruple)
            Assert.NotEqual(fresh.Calc(input).Value, direct.Calc(input).Value);
        else
            Assert.Equal(fresh.Calc(input).Value, direct.Calc(input).Value);
    }

    [Fact]
    public async Task ChainingAndGenuineQuadrupleOverflow()
    {
        var prices = new[] { 1d, 8, -3, 7, 2, 9 };
        foreach (var alphas in new[] { new[] { .2 }, new[] { .1, .3, .7, .9 } })
        {
            var indicator = MassNormalizedComparison.Indicator(3, alphas);
            ((IndicatorBase)indicator).Of(new FixedPeriodWma(2));
            var actual = await VolumePriceComparisonTests.Run(
                indicator,
                prices.Select(v => (v, v, v, 0d)).ToArray()
            );
            Assert.Equal(
                MassNormalizedComparison.Reference(
                    FixedWeightedComparison.Stage(prices, 2, false),
                    alphas,
                    false
                ),
                actual[0]
            );
        }
        var extreme = new[] { 0d, double.MaxValue, double.MaxValue };
        Assert.True(
            double.IsInfinity(
                MassNormalizedComparison.Reference(extreme, [.5, .5, .5, .5], false)[2]
            )
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new MassNormalizedQuadrupleEma(.5, .5, .5, .5),
                extreme.Select(v => (v, v, v, 0d)).ToArray()
            )
        );
    }

    [Fact]
    public void CorruptedValuesStartupAndStageWeightsAreDetected()
    {
        foreach (var alphas in new[] { new[] { .2 }, new[] { .1, .3, .7, .9 } })
        foreach (var native in new[] { false, true })
        foreach (var startup in new[] { false, true })
        {
            var pair = MassNormalizedComparison.Create(alphas);
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var values = (native ? pair.Competitor(data, period) : pair.Ooples(data, period))
                    .Outputs["Value"]
                    .Values;
                if (!startup)
                    values[^1] += 1;
                return new(startup ? 1 : 0, values);
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompensatedAverageComparison.Fixture(),
                    3
                )
            );
        }
        var quad = MassNormalizedComparison.Create([.1, .3, .7, .9]);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                quad with
                {
                    Library = MassNormalizedComparison.Create([.1]).Ooples,
                },
                CompensatedAverageComparison.Fixture(),
                3
            )
        );
    }
}
