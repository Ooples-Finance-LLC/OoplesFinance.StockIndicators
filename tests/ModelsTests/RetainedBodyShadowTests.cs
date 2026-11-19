using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RetainedBodyShadowTests
{
    [Fact]
    public void EveryPatternMatchesIndependentFractionsAcrossMixedScaleEvictions()
    {
        var bars = Corpus();
        foreach (var kind in Enum.GetValues<BodyShadowCandleKind>())
        foreach (int period in new[] { 1, 3, 10 })
        {
            var state = new BodyShadowCandleState(period, kind);
            var expected = BodyShadowCandleReference.Evaluate(bars, period, kind);
            for (int i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(bars[i]));
        }
    }

    [Fact]
    public void HistoryIgnoresTimeAndVolumeAcrossCertifiedAndFallbackEvictions()
    {
        var prices = Corpus();
        var changedMetadata = prices.Select((bar, i) => new Bar(DateTime.UnixEpoch.AddTicks(i * 193),
            bar.Open, bar.High, bar.Low, bar.Close,
            i % 3 == 0 ? double.NaN : i % 3 == 1 ? double.PositiveInfinity : -0d)).ToArray();
        bool sawSignal = false;
        foreach (var kind in Enum.GetValues<BodyShadowCandleKind>())
        foreach (int period in new[] { 1, 3, 10 })
        {
            var expected = BodyShadowCandleReference.Evaluate(prices, period, kind);
            sawSignal |= expected.Any(value => value != 0);
            var state = new BodyShadowCandleState(period, kind);
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int i = 0; i < prices.Length; i++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                        BitConverter.DoubleToInt64Bits(state.Update(changedMetadata[i])));
            }
        }
        Assert.True(sawSignal);
    }

    [Fact]
    public void ResetDropsRetainedCertificatesAndExactFallbackHistory()
    {
        var bars = Corpus();
        foreach (var kind in Enum.GetValues<BodyShadowCandleKind>())
        {
            var state = new BodyShadowCandleState(5, kind);
            foreach (var bar in bars.Take(17)) state.Update(bar);
            state.Reset();
            var fresh = new BodyShadowCandleState(5, kind);
            foreach (var bar in bars) Assert.Equal(fresh.Update(bar), state.Update(bar));
        }
    }

    [Fact]
    public void ZeroStrictBoundariesAndUnfilledMaximumPeriodsRemainSuppressed()
    {
        var zero = new Bar(default, -0d, 0d, -0d, 0d, 0d);
        foreach (var kind in Enum.GetValues<BodyShadowCandleKind>())
        {
            var zeros = new BodyShadowCandleState(1, kind);
            for (int i = 0; i < 8; i++) Assert.Equal(0L, BitConverter.DoubleToInt64Bits(zeros.Update(zero)));
            var unfilled = new BodyShadowCandleState(int.MaxValue, kind);
            foreach (var bar in Corpus().Take(12)) Assert.Equal(0d, unfilled.Update(bar));
        }
    }

    [Fact]
    public void UncertifiedBarsKeepOriginalEnqueueAndEvictionExceptions()
    {
        var ordinary = new Bar(default, 1, 2, 1, 2, 1);
        var longBody = new Bar(default, 1, 3, 1, 3, 1);
        foreach (var invalid in new[] { new Bar(default, 1, double.PositiveInfinity, 1, 2, 1),
            new Bar(default, 1, 2, 1, double.NaN, 1) })
        {
            var state = new BodyShadowCandleState(2, BodyShadowCandleKind.Marubozu);
            Assert.Throws<ArithmeticException>(() => state.Update(invalid));
            Assert.Equal(0d, state.Update(ordinary));
            Assert.Throws<ArithmeticException>(() => state.Update(ordinary));
            Assert.Equal(100d, state.Update(longBody));
        }
    }

    private static Bar[] Corpus() => Enumerable.Range(0, 64).Select(i =>
    {
        if (i % 8 == 0) return new Bar(default, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1);
        if (i % 8 == 1) return new Bar(default, 0, 2 * double.Epsilon, -double.Epsilon, double.Epsilon, 1);
        if (i % 8 == 2) return new Bar(default, -0d, 0d, -0d, 0d, 1);
        if (i % 8 == 3) return new Bar(default, 10, 9, 12, 11, 1);
        double open = 10 + (i * 7 % 13) / 8d;
        double close = open + (i % 5 - 2) / 4d;
        return new Bar(default, open, Math.Max(open, close) + (i % 3) / 16d,
            Math.Min(open, close) - (i % 4) / 16d, close, 1);
    }).ToArray();
}
