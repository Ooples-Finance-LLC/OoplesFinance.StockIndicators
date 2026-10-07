using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class BalanceOfPowerComparisonTests
{
    [Fact]
    public void MissingRangeLeavesWindowBeforeSmoothedValuesResume()
    {
        var data = BalanceOfPowerComparison.Fixture();
        foreach (var pair in BalanceOfPowerComparison.Pairs)
        foreach (var period in new[] { 1, 2, 3, 20, int.MaxValue })
            ComparisonVerifier.Check(pair, data, period);
        var skender = ComparisonPairs.Get("Skender.GetBop").Ooples(data, 2).Outputs["Value"];
        Assert.Equal(new[] { false, false, false, true, true, true, true, true }, skender.Present);
        Assert.Equal(-.125, skender.Values[3]);
        Assert.Equal(
            new[] { .5, 0, -.5, .25, .25, -.25, -.5, .5 },
            ComparisonPairs.Get("TaLib.Functions.Bop").Ooples(data, 2).Outputs["Value"].Values
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task IndependentReferenceLifecycleAndLazyCapacity(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(BalanceOfPowerWithValidity),
                "smoothed balance of power",
                () => new BalanceOfPowerWithValidity(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BalanceOfPowerWithValidity(0));
    }

    [Fact]
    public async Task MaximumPeriodConsumesOnlyObservedHistory()
    {
        var indicator = new BalanceOfPowerWithValidity(int.MaxValue);
        var data = BalanceOfPowerComparison.Fixture();
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            Assert.Equal(new double[data.Count], run[indicator.Value].ToArray());
            Assert.Equal(new double[data.Count], run[indicator.IsDefined].ToArray());
        }
    }

    [Fact]
    public void NativeMinimumInputIsPinned()
    {
        double[] input = [1];
        var output = new double[1];
        Assert.Equal(
            TALib.Core.RetCode.OutOfRangeParam,
            Functions.Bop<double>(input, input, input, input, System.Range.All, output, out _)
        );
    }
}
