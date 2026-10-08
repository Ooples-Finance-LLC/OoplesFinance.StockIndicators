using OoplesFinance.StockIndicators.Helpers;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CertifiedKaufmanBinaryTests
{
    [Fact]
    public void ZeroDistanceAndPossiblyZeroVarianceRequireExactDecision()
    {
        // With filter -1, distance zero clears the threshold only when variance
        // is strictly positive. Bounds [0,1] cannot certify either answer.
        var compare = typeof(CertifiedKaufmanBinaryWindow).GetMethod("Exceeds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var distance = CertifiedKaufmanBinaryWindow.Interval.Exact(0);
        Assert.Null(compare.Invoke(null, new object[] { distance, (F)(-1), new CertifiedKaufmanBinaryWindow.Interval(0, 1) }));
        Assert.Equal(false, compare.Invoke(null, new object[] { distance, (F)(-1), CertifiedKaufmanBinaryWindow.Interval.Exact(0) }));
        Assert.Equal(true, compare.Invoke(null, new object[] { distance, (F)(-1), CertifiedKaufmanBinaryWindow.Interval.Exact(1) }));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-2, false)]
    [InlineData(-3, false)]
    public void ExactFallbackHonorsNegativeSquareRootThreshold(int distance, bool expected)
    {
        // A negative filter with unit variance has threshold -2; equality is strict.
        Assert.Equal(expected, KaufmanBinaryWindow.Exceeds(distance, -2, 1));
    }

    [Theory]
    [InlineData(1, 0.6022, 0.0645, 10)]
    [InlineData(3, 0.6022, 0.0645, 10)]
    [InlineData(3, 0.6022, 0.0645, -10)]
    [InlineData(2, 1, 0, 100)]
    [InlineData(2, 0, 1, -100)]
    [InlineData(2147483647, 0.6022, 0.0645, 0)]
    public void CertifiedAndAmbiguousDecisionsMatchExactReplay(int length, double fast, double slow, double filter)
    {
        var random = new Random(915);
        var samples = Enumerable.Range(0, 24).Select(_ => (double)random.Next(-9, 10)).ToArray();
        var certified = new CertifiedKaufmanBinaryWindow(length, fast, slow, filter);
        var exact = new KaufmanBinaryWindow(length, fast, slow, filter);
        for (var replay = 0; replay < 2; replay++)
        {
            certified.Reset(); exact.Reset();
            foreach (var value in samples)
            {
                certified.Next(17, false);
                foreach (var final in new[] { false, false, true })
                    Assert.Equal(exact.Next(value, final), certified.Next(value, final));
            }
        }
    }

    [Fact]
    public void OutwardArithmeticContainsExactCancellationAndProducts()
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1d, -3d, 0d };
        foreach (var left in values)
        foreach (var right in values)
        {
            var a = F.Of(left) / 3; var b = F.Of(right) / 7;
            var x = CertifiedKaufmanBinaryWindow.Interval.Exact(a);
            var y = CertifiedKaufmanBinaryWindow.Interval.Exact(b);
            void Contains(CertifiedKaufmanBinaryWindow.Interval interval, F value)
            { Assert.True(interval.Lower.CompareTo(value) <= 0); Assert.True(interval.Upper.CompareTo(value) >= 0); }
            Contains(x + y, a + b); Contains(x - y, a - b); Contains(x * y, a * b);
            Contains((x + y).Square(), (a + b) * (a + b));
            Contains((x * y).Divide(3), a * b / 3);
        }
        var crossing = new CertifiedKaufmanBinaryWindow.Interval(-1, 2).Square();
        Assert.True(crossing.Lower.Sign <= 0);
        Assert.True(crossing.Upper.CompareTo(4) >= 0);
    }

    [Fact]
    public void NegativeThresholdAndZeroVarianceRetainStrictTie()
    {
        foreach (var filter in new[] { -100d, Math.BitDecrement(-100d), Math.BitIncrement(-100d), 100d })
        {
            var actual = new CertifiedKaufmanBinaryWindow(2, 0, 1, filter);
            var exact = new KaufmanBinaryWindow(2, 0, 1, filter);
            foreach (var value in new[] { 1d, 2, 1, 1, 1, 1, -2, -2 })
                Assert.Equal(exact.Next(value, true), actual.Next(value, true));
        }
    }

    [Fact]
    public void AmbiguousExtremeCancellationUsesLosslessExactFallback()
    {
        var actual = new CertifiedKaufmanBinaryWindow(2, 0, 1, -100);
        var exact = new KaufmanBinaryWindow(2, 0, 1, -100);
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, 1, 1, 1, -1, -1, -1, -1, 2 })
        foreach (var final in new[] { false, false, true })
            Assert.Equal(exact.Next(value, final), actual.Next(value, final));
        Assert.True(actual.ExactReplayUpdates > 0);
        actual.Reset();
        Assert.Equal(0, actual.ExactReplayUpdates);
        Assert.Equal(0d, actual.Next(0, true));
    }

    [Fact]
    public void FallbackAfterResetPreservesRationalThresholdTie()
    {
        var actual = new CertifiedKaufmanBinaryWindow(5, 0, 1, 125);
        // Changes [0,-1.5,0,0,0] have population deviation 0.6.
        // The 125% threshold is exactly 0.75, equal to the final sell distance.
        for (var replay = 0; replay < 2; replay++)
        {
            actual.Reset();
            var values = new[] { .75, -.75, -.75, -.75, -.75 };
            Assert.Equal(new[] { 1d, -1, -1, -1, 0 }, values.Select(value => actual.Next(value, true)).ToArray());
            Assert.True(actual.ExactReplayUpdates > 0);
        }
    }

    [Fact]
    public void OrdinaryLongStreamDoesNotReplayExactGrowingFractions()
    {
        var actual = new CertifiedKaufmanBinaryWindow(20, .6022, .0645, 10);
        for (var i = 0; i < 1000; i++)
            Assert.InRange(actual.Next(100 + i % 17 + i * .01, true), -1, 1);
        Assert.True(actual.ExactReplayUpdates < 100, $"Exact replay updates: {actual.ExactReplayUpdates}");
    }
}
