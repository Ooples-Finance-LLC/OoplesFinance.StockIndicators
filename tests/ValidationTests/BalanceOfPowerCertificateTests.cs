using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BalanceOfPowerCertificateTests
{
    [Fact]
    public void CertifiedAndFallbackRatiosMatchIndependentRationalRounding()
    {
        double[] edges = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            1, -1, Math.BitIncrement(1d), Math.BitDecrement(1d), Math.ScaleB(1, -1022), Math.ScaleB(1, 500)];
        for (int i = 0; i < edges.Length; i++)
        for (int j = 0; j < edges.Length; j++)
        {
            Check(edges[i], edges[j], edges[(i + 1) % edges.Length], edges[(j + 3) % edges.Length]);
            Check(edges[i], edges[j], 0, 0);
        }
        var random = new Random(80341);
        double Finite()
        {
            var bits = random.NextInt64();
            if (random.Next(2) == 0) bits = ~bits;
            if (((ulong)bits >> 52 & 2047) == 2047) bits ^= 1L << 52;
            return BitConverter.Int64BitsToDouble(bits);
        }
        for (int i = 0; i < 4000; i++)
        {
            Check(Finite(), Finite(), Finite(), Finite());
            var basis = 1 + random.NextDouble();
            var scale = random.Next(-1000, 1000);
            Check(Math.ScaleB(basis, scale), Math.ScaleB(Math.BitIncrement(basis), scale),
                Math.ScaleB(1 + random.NextDouble(), scale), Math.ScaleB(1 + random.NextDouble(), scale));
        }
    }

    private static void Check(double open, double close, double high, double low)
    {
        var expected = high == low ? 0 : ((ReferenceFraction.FromDouble(close) - ReferenceFraction.FromDouble(open))
            / (ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low))).ToDouble();
        var actual = RoundedBalanceOfPower.Of(open, high, low, close);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}
