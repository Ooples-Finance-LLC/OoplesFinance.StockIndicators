using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CertifiedUltimateRatioTests
{
    [Fact]
    public void CertifiedRatiosMatchIndependentExactWeightedExpression()
    {
        var random = new Random(74913);
        int accepted = 0;
        for (int sample = 0; sample < 2000; sample++)
        {
            var pressure = new JurikDyadic[3]; var range = new JurikDyadic[3];
            BigInteger top = 0, bottom = 1;
            for (int k = 0; k < 3; k++)
            {
                long r = random.NextInt64(1, 1L << 54);
                long p = random.NextInt64(0, r + 1);
                // Matching per-window powers cancel in the exact rational.
                int exponent = sample % 1200 - 600;
                pressure[k] = JurikDyadic.FromBinaryProduct(p, exponent);
                range[k] = JurikDyadic.FromBinaryProduct(r, exponent);
                top = top * r + (4 >> k) * p * bottom; bottom *= r;
            }
            if (!WindowUltimateOscillator.TryWeightedRatio(pressure, range, false, out double actual)) continue;
            accepted++;
            double expected = ExactMeanAccumulator.UnitRatio((100 * top) << 1074, 7 * bottom);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.True(accepted > 1900, $"Only {accepted} ordinary ratios were certified.");
    }

    [Fact]
    public void ExactEndpointsZeroPolicyAndUnsupportedInputsRemainExplicit()
    {
        JurikDyadic[] zeros = [default, default, default];
        JurikDyadic[] ones = [JurikDyadic.FromDouble(1), JurikDyadic.FromDouble(1), JurikDyadic.FromDouble(1)];
        Assert.True(WindowUltimateOscillator.TryWeightedRatio(ones, ones, false, out double full));
        Assert.Equal(100d, full);
        Assert.True(WindowUltimateOscillator.TryWeightedRatio(zeros, ones, false, out double empty));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(empty));
        Assert.False(WindowUltimateOscillator.TryWeightedRatio(ones, zeros, false, out _));
        Assert.True(WindowUltimateOscillator.TryWeightedRatio(ones, zeros, true, out double ignored));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(ignored));
        ones[0] = JurikDyadic.FromDouble(-1);
        Assert.False(WindowUltimateOscillator.TryWeightedRatio(ones, [JurikDyadic.FromDouble(1), JurikDyadic.FromDouble(1), JurikDyadic.FromDouble(1)], false, out _));
        ones[0] = JurikDyadic.FromDouble(double.Epsilon);
        Assert.False(WindowUltimateOscillator.TryWeightedRatio(ones, [JurikDyadic.FromDouble(1), JurikDyadic.FromDouble(1), JurikDyadic.FromDouble(1)], false, out _));
        Assert.False(WindowUltimateOscillator.TryWeightedRatio([], zeros, true, out _));
    }

    [Fact]
    public void UncertainHalfwayPublicationRetainsExactFallback()
    {
        // All three terms equal p/r, so the weighted output is exactly
        // (2^53 + 1) / 2^54: the midpoint above 0.5. The outward grid
        // endpoints straddle that midpoint and must not certify a result.
        var p = JurikDyadic.FromInteger(((Int128)1 << 53) + 1);
        var r = JurikDyadic.FromInteger((Int128)100 << 54);
        Assert.False(WindowUltimateOscillator.TryWeightedRatio([p, p, p], [r, r, r], false, out _));
    }

    [Fact]
    public void ExactBinaryReadPreservesCompactValueAndRejectsWideValue()
    {
        foreach (double value in new[] { 0d, -0d, double.Epsilon, -double.Epsilon, 1d, -13.75, double.MaxValue })
        {
            var exact = JurikDyadic.FromDouble(value);
            Assert.True(exact.TryBinaryParts(out Int128 integer, out int exponent));
            var reconstructed = new ExactMeanAccumulator(); reconstructed.Add(1, (BigInteger)integer);
            reconstructed.ScaleByPowerOfTwo(exponent);
            Assert.Equal(BitConverter.DoubleToInt64Bits(value == 0 ? 0 : value),
                BitConverter.DoubleToInt64Bits(reconstructed.Ratio(Unit())));
            Assert.True(exact.TryDouble(out double original));
            Assert.Equal(value == 0 ? 0L : BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(original));
        }
        var wide = JurikDyadic.FromDouble(double.MaxValue); wide.Add(double.Epsilon);
        Assert.False(wide.TryBinaryParts(out _, out _));
        static ExactMeanAccumulator Unit() { var one = new ExactMeanAccumulator(); one.Add(1); return one; }
    }
}
