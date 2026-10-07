using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ElderRayComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(13)]
    [InlineData(int.MaxValue)]
    public async Task ExactSeedRecurrenceAndLifecycleAreVerified(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ElderRayWithDetails),
                "Elder-ray details",
                () => new ElderRayWithDetails(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void DistinctCandleFieldsStartupAndPeriodOneAreExplicit()
    {
        var data = ElderRayComparison.Fixture();
        foreach (var period in new[] { 1, 3, 13 })
            ComparisonVerifier.Check(ElderRayComparison.Pair, data, period);
        var single = ElderRayComparison.Pair.Ooples(data, 1);
        Assert.Equal(data.Closes, single.Outputs["Ema"].Values);
        Assert.Equal(new[] { 1d, 3, 4, 3, 2, 5 }, single.Outputs["BullPower"].Values);
        Assert.Equal(new[] { -4d, -4, -2, -2, -4, -2 }, single.Outputs["BearPower"].Values);
        var three = ElderRayComparison.Pair.Ooples(data, 3);
        foreach (var output in three.Outputs.Values)
            Assert.Equal(new[] { false, false, true, true, true, true }, output.Present);
        Assert.Equal(1, three.Outputs["Ema"].Values[2]);
        Assert.Equal(-1, three.Outputs["BullPower"].Values[2]);
        Assert.Equal(-7, three.Outputs["BearPower"].Values[2]);
        var rows = data.Quotes.GetElderRay(3).ToArray();
        Assert.Equal(-8, ((IReusableResult)rows[2]).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetElderRay(0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ElderRayWithDetails(0));
    }

    [Fact]
    public async Task ExactSeedAndCompleteUpdatesPreserveWideAndTinyValues()
    {
        foreach (var price in new[] { double.MaxValue, double.Epsilon })
        {
            var actual = await VolumePriceComparisonTests.Run(
                new ElderRayWithDetails(2),
                (price, price, price, 0),
                (price, price, price, 0),
                (price, price, price, 0)
            );
            Assert.Equal(new[] { 0d, price, price }, actual[0]);
            Assert.Equal(new double[3], actual[1]);
            Assert.Equal(new double[3], actual[2]);
            Assert.Equal(new[] { 0d, 1, 1 }, actual[3]);
        }
        var maximum = await VolumePriceComparisonTests.Run(
            new ElderRayWithDetails(int.MaxValue),
            (1, 1, 1, 0)
        );
        Assert.All(maximum, series => Assert.Equal(new double[1], series));
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new ElderRayWithDetails(1),
                (double.MaxValue, -double.MaxValue, -double.MaxValue, 0)
            )
        );
    }

    [Fact]
    public void EveryOutputAndPresenceFlagDetectsCorruption()
    {
        var data = ElderRayComparison.Fixture();
        var pair = ElderRayComparison.Pair;
        foreach (var name in ElderRayComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData input, int period)
            {
                var result = native ? pair.Competitor(input, period) : pair.Ooples(input, period);
                var output = result.Outputs[name];
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
