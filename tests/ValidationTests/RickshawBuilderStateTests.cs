using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RickshawBuilderStateTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    [InlineData(10, 5)]
    public async Task SelectedStatePreservesIndependentFormulaPreviewResetAndFallback(int doji, int near)
    {
        var bars = Enumerable.Range(0, 70).Select(i => new Bar(default,
            i % 2 == 0 ? 100 : -100, 110 + i % 3, -110 - i % 7,
            i % 2 == 0 ? 100.25 : -100.25, 0)).ToArray();
        bars[35] = new Bar(default, double.Epsilon, double.MaxValue, -double.MaxValue, 0, 0);
        bars[48] = new Bar(default, -2, -4, 5, 1, 0);
        var indicator = new RickshawManCandle(doji, near);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        var expected = run[indicator].ToArray();
        foreach (var rule in indicator.ValidationRules)
            rule.Check(new IndicatorValidationContext("Rickshaw builder independent formula", bars, [expected], 0));
        var state = (IPreviewIndicatorState)indicator.CreateState();
        for (var pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(bars[35], false);
                Assert.Equal(expected[i], state.Update(bars[i], false));
                Assert.Equal(expected[i], state.Update(bars[i], true));
            }
        }
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(int.MaxValue)]
    public async Task LargePublicPeriodsStillAcceptShortHistoriesWithoutPeriodSizedAllocation(int period)
    {
        var indicator = new RickshawManCandle(period, period);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var state = (IIndicatorState)indicator.CreateState();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 100_000, $"State reserved {allocated} bytes for period {period}.");
        var bars = new[] { new Bar(default, 100, 110, 90, 101, 0) };
        Assert.Equal(0d, state.Update(bars[0]));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d }, run[indicator].ToArray());
    }
}
