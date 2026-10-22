using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ParabolicClassicFastTests
{
    [Fact]
    public void SubtractionCertificatesRetainExactFusedRoundingAcrossScales()
    {
        var grid = BigInteger.One << 1074;
        void Check(double stop, double extreme, double factor)
        {
            if (!CertifiedParabolicClassic.TryAdvance(stop, extreme, factor, out double actual)) return;
            var a = ExactVarianceWindow.Units(stop); var b = ExactVarianceWindow.Units(extreme);
            var f = ExactVarianceWindow.Units(factor);
            double expected = ExactMeanAccumulator.UnitRatio(RocBankValue.RoundUnits(a * grid + f * (b - a), grid), 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        for (int exponent = -1074; exponent <= 1023; exponent++)
        {
            double value = Math.ScaleB(1, exponent);
            foreach (double factor in new[] { .02, .5, 1d })
            {
                Assert.True(CertifiedParabolicClassic.TryAdvance(value, value, factor, out _));
                Check(value, Math.BitIncrement(value), factor);
                Check(-value, -Math.BitIncrement(value), factor);
            }
        }
        var random = new Random(7232031);
        for (int i = 0; i < 1000; i++)
        {
            double a = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            double b = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            if (double.IsFinite(a) && double.IsFinite(b)) Check(a, b, .02);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryClassicAndExtendedHistoryUseTheFiniteLoopAndRetainAllBits(bool signed)
    {
        var bars = Enumerable.Range(0, 1000).Select(i =>
        {
            double middle = 100 + Math.Sin(i * .1) * 10;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), middle, middle + 2, middle - 2, middle, 1);
        }).ToArray();
        double start = signed ? -100 : 0, offset = signed ? .01 : 0;
        Assert.True(CertifiedParabolicClassic.TryCalculate(bars, signed, start, offset, .02, .02, .2, .02, .02, .2, out var actual));
        var expected = signed ? OriginalParabolicStopSnapshots.Extended(bars, start, offset) : OriginalParabolicStopSnapshots.Classic(bars);
        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(expected[i].HasValue, actual[i].HasValue);
            if (expected[i].HasValue)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]!.Value), BitConverter.DoubleToInt64Bits(actual[i]!.Value));
        }
    }

    [Fact]
    public void UnrepresentableDifferenceRetriesThroughTheExactImplementation()
    {
        Bar[] bars = [new(default, 0, double.MaxValue, double.MaxValue, 0, 1),
            new(default, 0, double.MaxValue, -double.MaxValue, 0, 1),
            new(default, 0, double.MaxValue, -double.MaxValue, 0, 1)];
        Assert.False(CertifiedParabolicClassic.TryCalculate(bars, false, 0, 0, .02, .02, .2, .02, .02, .2, out _));
        var expected = OriginalParabolicStopSnapshots.Classic(bars);
        var actual = ParabolicStopSnapshots.Classic(bars);
        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(expected[i].HasValue, actual[i].HasValue);
            if (expected[i].HasValue)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]!.Value), BitConverter.DoubleToInt64Bits(actual[i]!.Value));
        }
    }
}
