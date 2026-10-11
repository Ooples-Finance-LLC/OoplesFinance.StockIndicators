using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedBodyShadowTests
{
    [Fact]
    public async Task EachPatternPreservesExactWindowsAndResetAcrossWideAndSubnormalBars()
    {
        double[] points = [-double.MaxValue, -10, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, 10, double.MaxValue];
        var bars = (from open in points from close in points select new Bar(default, open, double.MaxValue, -double.MaxValue, close, 1))
            .Concat(points.Select(v => new Bar(default, 0, double.Epsilon, 0, v, 1)))
            .Concat(Enumerable.Range(0, 40).Select(i => new Bar(default, 4, 10, 0, i % 3 == 0 ? 4 : 6, 1))).ToArray();
        foreach (int period in new[] { 1, 3, 10 })
        foreach (var kind in Enum.GetValues<BodyShadowCandleKind>())
        {
            IndicatorBase indicator = kind switch
            {
                BodyShadowCandleKind.Marubozu => new MarubozuCandle(period),
                BodyShadowCandleKind.ClosingMarubozu => new ClosingMarubozuCandle(period),
                BodyShadowCandleKind.BeltHold => new BeltHoldCandle(period),
                BodyShadowCandleKind.SpinningTop => new SpinningTopCandle(period),
                BodyShadowCandleKind.HighWave => new HighWaveCandle(period),
                BodyShadowCandleKind.LongLine => new LongLineCandle(period),
                _ => new ShortLineCandle(period)
            };
            var state = new BodyShadowCandleState(period, kind);
            var first = bars.Select(b => state.Update(in b)).ToArray();
            state.Reset();
            Assert.Equal(first, bars.Select(b => state.Update(in b)).ToArray());
            foreach (var rule in ((IIndicatorValidationContract)indicator).ValidationRules)
                rule.Check(new IndicatorValidationContext("selective-body-shadow-sums", bars, [first], 0));
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
                Assert.Equal(first, run[indicator].ToArray());
            }
        }
    }
}
