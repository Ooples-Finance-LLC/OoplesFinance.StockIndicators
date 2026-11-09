using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DirectBinaryAccumulationPublicationTests
{
    [Fact]
    public void BinaryPublicationMatchesIndependentFractionsAcrossExponents()
    {
        Int128[] coefficients = [1, ((Int128)1 << 53) - 1, ((Int128)1 << 53) + 1,
            ((Int128)1 << 64) + 17, ((Int128)1 << 106) - 1];
        for (int exponent = -1074; exponent <= 1023; exponent++)
        foreach (var coefficient in coefficients)
        foreach (int sign in new[] { -1, 1 }) Check(sign * coefficient, exponent);
        foreach (int exponent in new[] { -1200, -1075, 1100 })
        foreach (var coefficient in coefficients)
        foreach (int sign in new[] { -1, 1 }) Check(sign * coefficient, exponent);
        Equal(0, JurikDyadic.RoundBinaryProduct(0, -1200));
    }

    [Fact]
    public void GuardStickyParityAndCarryMatchIndependentRoundingCells()
    {
        for (int width = 54; width <= 106; width++)
        {
            int shift = width - 53;
            Int128 half = (Int128)1 << (shift - 1);
            Int128 leading = (Int128)1 << (width - 1);
            Int128[] midpoints = [leading + half, leading + ((Int128)1 << shift) + half,
                ((Int128)1 << width) - half];
            foreach (var midpoint in midpoints)
            foreach (int offset in new[] { -1, 0, 1 })
            foreach (int sign in new[] { -1, 1 })
            foreach (int exponent in new[] { -1074, -1074 - width, -1021 - width, 1024 - width, 0 })
                Check(sign * (midpoint + offset), exponent);
        }
    }

    [Fact]
    public void CapturedPrefixPublisherPreservesWideFallbackOverflowAndCanonicalZero()
    {
        double[][] sequences = [
            [1, Math.ScaleB(1, -105), -1, -Math.ScaleB(1, -105)],
            [1, Math.ScaleB(1, -106), -1, -Math.ScaleB(1, -106)],
            [1, Math.ScaleB(1, -180), -1, -Math.ScaleB(1, -180)],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue],
            [Math.CopySign(0, -1), double.Epsilon, -double.Epsilon, -double.Epsilon, double.Epsilon]];
        foreach (var sequence in sequences)
        {
            var total = CompactMacdValue.Of(0);
            var reference = new ReferenceFraction(0);
            foreach (double value in sequence)
            {
                total += CompactMacdValue.Of(value);
                reference += ReferenceFraction.FromDouble(value);
                Equal(reference.ToDouble(), total.PublishRoundedSum());
            }
            Equal(0, total.PublishRoundedSum());
        }
    }

    private static void Check(Int128 coefficient, int exponent)
    {
        var reference = new ReferenceFraction((BigInteger)coefficient);
        var power = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
        reference = exponent < 0 ? reference / power : reference * power;
        double actual = JurikDyadic.RoundBinaryProduct(coefficient, exponent);
        Equal(reference.ToDouble(), actual);
        Equal(JurikDyadic.FromBinaryProduct(coefficient, exponent).Mean(1), actual);
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
