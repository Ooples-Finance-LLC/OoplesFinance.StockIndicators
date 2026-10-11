using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedMoneyFlowCertificateTests
{
    [Fact]
    public void CertifiedFlowRetainsExactRatiosAcrossRoundingAndExponentBoundaries()
    {
        var random = new Random(59216);
        var bytes = new byte[8];
        double Finite()
        {
            random.NextBytes(bytes);
            long bits = BitConverter.ToInt64(bytes);
            return BitConverter.Int64BitsToDouble(bits & ~0x0010000000000000L);
        }
        for (int i = 0; i < 2048; i++)
        {
            Compare(Finite(), Finite(), Finite(), Finite());
            double close = (i % 37 - 18) / 8d;
            Compare(close + 1.5, close - 2.5, close, i % 17 - 8);
        }
        foreach (int exponent in new[] { -1074, -1022, -600, -500, -485, -484, -483, 0, 500, 1020 })
        foreach (double fraction in new[] { 1d, Math.BitIncrement(1), 1.125, 1.5, Math.BitDecrement(2) })
        {
            double high = Math.ScaleB(fraction, exponent);
            foreach (double volume in new[] { high, -high, double.Epsilon, -double.Epsilon, 1, -1, double.MaxValue })
            {
                Compare(high, 0, high, volume);
                Compare(high, -high, high / 2, volume);
                Compare(high, 0, Math.BitDecrement(high / 2), volume);
                Compare(high, 0, Math.BitIncrement(high / 2), volume);
            }
        }
        foreach (double zero in new[] { 0d, -0d })
        {
            Compare(1, 0, zero, zero);
            Compare(1, 0, .5, zero);
            Compare(zero, zero, 1, 1);
        }
    }

    private static void Compare(double high, double low, double close, double volume)
    {
        var expected = Reference(high, low, close, volume);
        var actual = MoneyFlowAccumulationWindow.Flow(high, low, close, volume);
        Assert.Equal(expected.UpperShift, actual.UpperShift);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Mantissa), BitConverter.DoubleToInt64Bits(actual.Mantissa));
    }

    private static RocBankValue Reference(double high, double low, double close, double volume)
    {
        if (high == low) return default;
        var top = new ExactMeanAccumulator();
        top.AddProduct(close, volume, 2); top.AddProduct(high, volume, -1); top.AddProduct(low, volume, -1);
        for (int shift = 0; ; shift += 1024)
        {
            var bottom = new ExactMeanAccumulator(); var scale = BigInteger.One << shift;
            bottom.Add(high, scale); bottom.Add(low, -scale);
            double value = top.Ratio(bottom);
            if (!double.IsInfinity(value)) return new RocBankValue(value, shift);
        }
    }
}
