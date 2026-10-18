using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedArithmeticCertificateTests
{
    [Fact]
    public void CertificatesProveExactValuesAndRefuseUnderflowedResiduals()
    {
        Assert.True(Binary64ArithmeticCertificate.TryProduct(1.25, 100, out double product));
        Assert.Equal(125, product);
        double tiny = Math.ScaleB(Math.BitIncrement(1), -500);
        Assert.Equal(0, Math.FusedMultiplyAdd(tiny, tiny, -(tiny * tiny)));
        Assert.False(Binary64ArithmeticCertificate.TryProduct(tiny, tiny, out _));
        Assert.False(Binary64ArithmeticCertificate.TryProduct(Math.BitIncrement(1), Math.BitIncrement(1), out _));
        var random = new Random(34277); var bytes = new byte[8];
        double Finite() { random.NextBytes(bytes); return BitConverter.Int64BitsToDouble(BitConverter.ToInt64(bytes) & ~0x0010000000000000L); }
        for (int i = 0; i < 8192; i++)
        {
            double left = Finite(), right = i % 2 == 0 ? 100 : Finite();
            if (Binary64ArithmeticCertificate.TryDifference(left, right, out double difference))
            {
                var exact = new ExactMeanAccumulator(); exact.Add(left); exact.Add(right, -1); exact.Add(difference, -1);
                Assert.True(exact.IsExactlyZero);
            }
            if (Binary64ArithmeticCertificate.TryProduct(left, right, out product))
            {
                var exact = new ExactMeanAccumulator(); exact.AddProduct(left, right); exact.Add(product, -1);
                Assert.True(exact.IsExactlyZero);
            }
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.False(Binary64ArithmeticCertificate.TryProduct(invalid, 100, out _));
            Assert.False(Binary64ArithmeticCertificate.TryDifference(invalid, 1, out _));
        }
    }
}
