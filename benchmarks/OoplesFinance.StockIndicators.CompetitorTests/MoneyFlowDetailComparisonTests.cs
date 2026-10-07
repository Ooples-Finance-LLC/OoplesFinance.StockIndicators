using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MoneyFlowDetailComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task IndependentRationalContractCoversLifecycleAndExtremes(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(MoneyFlowWithDetails),
                "complete money flow",
                () => new MoneyFlowWithDetails(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task FlowRoundsCompleteFormulaAndCmfUsesExactSumOfPublishedFlows()
    {
        var result = await VolumePriceComparisonTests.Run(
            new MoneyFlowWithDetails(2),
            (3, 0, 2, 5),
            (3, 0, 1, 2)
        );
        Assert.Equal(new[] { 1d / 3, -1d / 3 }, result[0]);
        Assert.Equal(new[] { 5d / 3, -2d / 3 }, result[1]);
        Assert.Equal(Math.BitIncrement(1d / 7), result[2][1]);
        Assert.Equal(new[] { 0d, 1 }, result[3]);
        var outside = await VolumePriceComparisonTests.Run(
            new MoneyFlowWithDetails(1),
            (1, 0, 2, 1)
        );
        Assert.Equal(3, outside[2][0]);
    }

    [Fact]
    public async Task ZeroRangeAndZeroVolumeHaveIndependentRecovery()
    {
        var result = await VolumePriceComparisonTests.Run(
            new MoneyFlowWithDetails(2),
            (1, 0, 1, 0),
            (1, 0, 1, 0),
            (1, 0, 1, 2),
            (1, 1, 1, 0),
            (1, 0, 1, 0),
            (1, 0, 1, 3)
        );
        Assert.Equal(new[] { 1d, 1, 1, 0, 1, 1 }, result[0]);
        Assert.Equal(new[] { 0d, 0, 2, 0, 0, 3 }, result[1]);
        Assert.Equal(new[] { 0d, 0, 1, 1, 0, 1 }, result[2]);
        Assert.Equal(new[] { 0d, 0, 1, 1, 0, 1 }, result[3]);
    }

    [Fact]
    public async Task WideAndSubnormalTotalsNormalizeWithoutEarlyRounding()
    {
        var huge = await VolumePriceComparisonTests.Run(
            new MoneyFlowWithDetails(2),
            (1, 0, 1, double.MaxValue),
            (1, 0, 1, double.MaxValue)
        );
        Assert.Equal(1, huge[2][1]);
        var tiny = await VolumePriceComparisonTests.Run(
            new MoneyFlowWithDetails(2),
            (1, 0, 1, 0),
            (1, 0, 1, double.Epsilon)
        );
        Assert.Equal(1, tiny[2][1]);
        Assert.Equal(1, tiny[3][1]);
        var data = CompetitorData.FromOhlcv([1, 1], [1, 1], [0, 0], [1, 1], [0, double.Epsilon]);
        ComparisonVerifier.Check(MoneyFlowDetailComparison.Pair, data, 2);
        Assert.Null(data.Quotes.GetCmf(2).Last().Cmf);
        var lazy = await VolumePriceComparisonTests.Run(
            new MoneyFlowWithDetails(int.MaxValue),
            (1, 0, 1, 1),
            (1, 0, 0, 1)
        );
        Assert.Equal(new[] { 1d, -1 }, lazy[1]);
        Assert.Equal(new[] { 0d, 0 }, lazy[3]);
    }

    [Fact]
    public async Task UnrepresentablePublishedFieldsAreRejected()
    {
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new MoneyFlowWithDetails(3),
                (double.Epsilon, 0, double.MaxValue, 1)
            )
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new MoneyFlowWithDetails(3),
                (1, 0, 1, double.MaxValue),
                (1, 0, 0, -double.MaxValue),
                (1, 0, .5, double.Epsilon)
            )
        );
    }

    [Fact]
    public void QuoteConversionCanCollapseTheRangeAndChangeEveryFlowReading()
    {
        var pair = MoneyFlowDetailComparison.Pair;
        var data = MoneyFlowDetailComparison.CollapsedRangeFixture();
        ComparisonVerifier.Check(pair, data, 2);
        var owned = pair.Ooples(data, 2);
        var native = pair.Competitor(data, 2);
        Assert.Equal(new[] { 1d, -1, 1, -1 }, owned.Outputs["MoneyFlowMultiplier"].Values);
        Assert.Equal(new[] { 0d, 0, 0, 0 }, native.Outputs["MoneyFlowMultiplier"].Values);
        Assert.Equal(-1d / 3, owned.Outputs["Cmf"].Values[1]);
        Assert.Equal(0, native.Outputs["Cmf"].Values[1]);
    }

    [Fact]
    public void AllFieldsAndPresenceHaveIndependentReferencesAndRejectCorruption()
    {
        var pair = MoneyFlowDetailComparison.Pair;
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, VolumePriceComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, VolumePriceComparison.Fixture(true), period);
        }
        foreach (var native in new[] { false, true })
        foreach (var name in MoneyFlowDetailComparison.Names)
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                var i = data.Count - 1;
                if (presence)
                    output.Present![i] = !output.Present[i];
                else
                    output.Values[i] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    VolumePriceComparison.Fixture(),
                    3
                )
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyFlowWithDetails(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VolumePriceComparison.Fixture().Quotes.GetCmf(0).ToArray()
        );
    }
}
