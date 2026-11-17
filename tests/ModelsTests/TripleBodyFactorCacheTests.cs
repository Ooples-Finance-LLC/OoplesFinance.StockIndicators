using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TripleBodyFactorCacheTests
{
    [Theory]
    [InlineData((int)TripleBodyCandleKind.ThreeInside)]
    [InlineData((int)TripleBodyCandleKind.TwoCrows)]
    [InlineData((int)TripleBodyCandleKind.UpsideGapTwoCrows)]
    [InlineData((int)TripleBodyCandleKind.UniqueThreeRiver)]
    public void CachedBodiesMatchIndependentReferenceAndReset(int value)
    {
        var kind = (TripleBodyCandleKind)value;
        foreach (var (longPeriod, shortPeriod) in new[] { (1, 1), (10, 10), (3, 10), (10, 3), (63, 1) })
        foreach (double scale in new[] { 1d, double.Epsilon, double.MaxValue / 64 })
        {
            var bars = Data(kind, scale);
            var expected = TripleBodyCandleReference.Evaluate(bars, longPeriod, shortPeriod, kind);
            var state = new TripleBodyCandleState(longPeriod, shortPeriod, kind);
            Assert.Equal(expected, bars.Select(b => state.Update(b)).ToArray());
            state.Reset();
            Assert.Equal(expected, bars.Select(b => state.Update(b)).ToArray());
            if (longPeriod == 10 && shortPeriod == 10)
            {
                Assert.Contains(0d, expected);
                Assert.Contains(kind == TripleBodyCandleKind.UniqueThreeRiver ? 100d : -100d, expected);
                if (kind == TripleBodyCandleKind.ThreeInside) Assert.Contains(100d, expected);
            }
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteBodyRetainsMeanExceptionAndResetRecovery(double invalid)
    {
        foreach (var kind in Enum.GetValues<TripleBodyCandleKind>())
        foreach (bool invalidOpen in new[] { false, true })
        {
            var state = new TripleBodyCandleState(3, 10, kind);
            var bar = new Bar(default, invalidOpen ? invalid : 1, 2, 0, invalidOpen ? 1 : invalid, 1);
            var error = Assert.Throws<ArithmeticException>(() => state.Update(bar));
            Assert.Equal("An exact finite mean requires finite inputs.", error.Message);
            state.Reset();
            var bars = Data(kind, 1);
            Assert.Equal(TripleBodyCandleReference.Evaluate(bars, 3, 10, kind), bars.Select(b => state.Update(b)).ToArray());
        }
    }

    private static Bar[] Data(TripleBodyCandleKind kind, double scale) => Enumerable.Range(0, 256).Select(i =>
    {
        int phase = i % 16;
        Bar b = new(default, 4, 30, 0, 6, 1);
        if (phase == 13) b = kind == TripleBodyCandleKind.UniqueThreeRiver ? new(default, 9, 30, 0, 2, 1) : new(default, 2, 30, 0, 8, 1);
        if (phase == 14) b = kind switch
        {
            TripleBodyCandleKind.ThreeInside => new(default, 4, 30, 0, 6, 1),
            TripleBodyCandleKind.UniqueThreeRiver => new(default, 6, 30, -1, 4, 1),
            _ => new(default, 12, 30, 0, 10, 1)
        };
        if (phase == 15) b = kind switch
        {
            TripleBodyCandleKind.ThreeInside => new(default, 8, 30, -2, 1, 1),
            TripleBodyCandleKind.TwoCrows => new(default, 11, 30, -2, 5, 1),
            TripleBodyCandleKind.UpsideGapTwoCrows => new(default, 13, 30, -2, 9, 1),
            _ => new(default, 0, 30, -2, 1, 1)
        };
        if (kind == TripleBodyCandleKind.ThreeInside && i / 16 % 2 != 0)
            b = new(default, -b.Open, -b.Low, -b.High, -b.Close, 1);
        return new Bar(default, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, 1);
    }).ToArray();
}
