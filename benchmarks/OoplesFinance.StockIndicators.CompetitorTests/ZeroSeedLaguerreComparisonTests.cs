using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ZeroSeedLaguerreComparisonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(.1)]
    [InlineData(.5)]
    [InlineData(.9)]
    [InlineData(1)]
    [InlineData(double.Epsilon)]
    [InlineData(.9999999999999999)]
    public async Task ExactStagesAndLifecycleAreIndependentlyVerified(double gamma)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ZeroSeedLaguerreFilter),
                "zero-seeded Laguerre",
                () => new ZeroSeedLaguerreFilter(gamma)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EndpointsAndZeroSeedHaveExplicitImpulseResponses()
    {
        var data = CompetitorData.FromCloses([6, 0, 0, 0, 0]);
        foreach (var gamma in new[] { 0d, 1d })
        {
            var pair = ZeroSeedLaguerreComparison.Create(gamma);
            var expected = gamma == 0 ? new[] { 1d, 2, 2, 1, 0 } : new double[5]; // NOSONAR: These are exact endpoint configurations.
            Assert.Equal(expected, pair.Ooples(data, 20).Outputs["Value"].Values);
            Assert.Equal(expected, pair.Competitor(data, 20).Outputs["Value"].Values);
        }
        Assert.Equal(3, new ZeroSeedLaguerreFilter().WarmupBars);
        Assert.Equal(.1, new ZeroSeedLaguerreFilter().Gamma);
    }

    [Fact]
    public async Task WeightedSumsRetainFiniteExtremeAndSubnormalOutputs()
    {
        foreach (var magnitude in new[] { double.MaxValue, double.Epsilon })
        {
            var prices = Enumerable.Repeat(magnitude, 4).ToArray();
            var actual = await VolumePriceComparisonTests.Run(
                new ZeroSeedLaguerreFilter(0),
                prices.Select(x => (x, x, x, 0d)).ToArray()
            );
            Assert.Equal(ZeroSeedLaguerreComparison.Reference(prices, 0, false), actual[0]);
            Assert.Equal(magnitude, actual[0][^1]);
        }
        var native = new QuanTAlib.Ltma(0);
        native.Calc(new QuanTAlib.TValue(double.MaxValue, true, false));
        Assert.True(
            double.IsPositiveInfinity(
                native.Calc(new QuanTAlib.TValue(double.MaxValue, true, false)).Value
            )
        );
        var zero = await VolumePriceComparisonTests.Run(
            new ZeroSeedLaguerreFilter(1),
            (double.MaxValue, double.MaxValue, double.MaxValue, 0),
            (-double.MaxValue, -double.MaxValue, -double.MaxValue, 0)
        );
        Assert.Equal(new double[2], zero[0]);
    }

    [Fact]
    public async Task ExtendedUnpublishedStagesAgreeWithIndependentIntegerGrid()
    {
        foreach (var gamma in new[] { .1, .5, .9, Math.BitDecrement(1d) })
        foreach (var magnitude in new[] { double.MaxValue / 2, double.Epsilon })
        {
            var prices = Enumerable
                .Range(0, 32)
                .Select(i => i % 5 < 3 ? magnitude : -magnitude)
                .ToArray();
            var expected = ZeroSeedLaguerreComparison.Reference(prices, gamma, false);
            var actual = await VolumePriceComparisonTests.Run(
                new ZeroSeedLaguerreFilter(gamma),
                prices.Select(x => (x, x, x, 0d)).ToArray()
            );
            Assert.Equal(expected, actual[0]);
        }
    }

    [Fact]
    public void NativeSourceRevisionResetAndInvalidParametersArePinned()
    {
        foreach (var gamma in new[] { 0d, .1, .5, .9, 1 })
        {
            var source = new QuanTAlib.TSeries();
            var subscribed = new QuanTAlib.Ltma(source, gamma);
            var direct = new QuanTAlib.Ltma(gamma);
            var values = ZeroSeedLaguerreComparison.Fixture().Closes;
            foreach (var value in values)
            {
                var input = new QuanTAlib.TValue(value, true, false);
                var expected = direct.Calc(input).Value;
                source.Add(input);
                Assert.Equal(expected, subscribed.Value);
            }
            Assert.Equal(gamma, direct.Gamma);
            Assert.Equal(4, direct.WarmupPeriod);
            Assert.True(direct.IsHot);
            var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
            var fresh = new QuanTAlib.Ltma(gamma);
            foreach (var value in values.SkipLast(1).Append(7))
                fresh.Calc(new QuanTAlib.TValue(value, true, false));
            Assert.Equal(fresh.Value, revised);
            direct.Init();
            Assert.Equal(
                new QuanTAlib.Ltma(gamma).Calc(new QuanTAlib.TValue(7, true, false)).Value,
                direct.Calc(new QuanTAlib.TValue(7, true, false)).Value
            );
        }
        foreach (
            var bad in new[]
            {
                -1d,
                2,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZeroSeedLaguerreFilter(bad));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Ltma(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Ltma(2));
        Assert.True(
            double.IsNaN(
                new QuanTAlib.Ltma(double.NaN).Calc(new QuanTAlib.TValue(1, true, false)).Value
            )
        );
    }

    [Fact]
    public void IndependentTrajectoriesDetectSeedSignAndAlignmentCorruption()
    {
        foreach (var gamma in new[] { 0d, .1, .5, .9, 1, double.Epsilon })
        {
            var pair = ZeroSeedLaguerreComparison.Create(gamma);
            var data = ZeroSeedLaguerreComparison.Fixture();
            ComparisonVerifier.Check(pair, data, 20);
            foreach (var native in new[] { false, true })
            foreach (var aligned in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData input, int period)
                {
                    var output = (
                        native ? pair.Competitor(input, period) : pair.Ooples(input, period)
                    ).Outputs["Value"];
                    if (aligned)
                        return new(1, output.Values);
                    output.Values[0] += 1;
                    return new(0, output.Values);
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        20
                    )
                );
            }
        }
    }
}
