using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactMeanDivisionCertificateTests
{
    [Fact]
    public void ExtendedCompactDivisionRoundsBothSignsOfHalfwayCasesToEven()
    {
        foreach (int sign in new[] { -1, 1 })
        foreach (int offset in new[] { 0, 1 })
        {
            var sum = new ExactMeanAccumulator();
            sum.Add(sign * (1 + offset * Math.ScaleB(1, -52)));
            sum.Add(sign * Math.ScaleB(1, -53));
            // A 54-bit numerator shifted by 19 bits cannot use ulong division.
            var expected = Math.ScaleB(sign * (1 + (offset == 0 ? 0 : Math.ScaleB(1, -51))), -20);
            Bits(expected, sum.Mean(1 << 20));
        }
    }

    [Fact]
    public void ExtendedCompactDivisionDoesNotAllocate()
    {
        static double Run() => ExactMeanAccumulator.ScaledRatio(305235, long.MaxValue - 24, 0);
        var expected = (new ReferenceFraction(305235) / new ReferenceFraction(long.MaxValue - 24)).ToDouble();
        for (int i = 0; i < 100; i++) Bits(expected, Run());
        var before = GC.GetAllocatedBytesForCurrentThread();
        double total = 0;
        for (int i = 0; i < 1000; i++) total += Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(total > 0);
    }

    [Fact]
    public void ExtendedGridLimitsAndFallbackPreserveSignedUnderflowAndOverflow()
    {
        foreach (long numerator in new[] { long.MinValue, -long.MaxValue, -3, 3, (1L << 53) + 1, long.MaxValue })
        foreach (long denominator in new[] { 1L, 3, (1L << 53) + 1, long.MaxValue - 24, long.MaxValue })
        foreach (int exponent in new[] { -1203, -1202, -1139, -1138, -1137, -1076, -1075, -1074, 0, 1023, 1088 })
        {
            var exact = new ReferenceFraction(numerator) / new ReferenceFraction(denominator);
            var power = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
            exact = exponent < 0 ? exact / power : exact * power;
            Bits(exact.ToDouble(), ExactMeanAccumulator.ScaledRatio(numerator, denominator, exponent));
        }
    }

    [Fact]
    public void IntegerAndExponentBoundariesMatchIndependentRounding()
    {
        long[] numerators = [long.MinValue, -((1L << 53) + 1), -(1L << 53), -((1L << 53) - 1),
            -3, -1, 0, 1, 3, (1L << 52) - 1, 1L << 52, (1L << 53) - 1, 1L << 53, (1L << 53) + 1, long.MaxValue];
        long[] denominators = [1, 2, 3, 20, 210, (1L << 53) - 1, 1L << 53, (1L << 53) + 1, long.MaxValue];
        foreach (var numerator in numerators)
        foreach (var denominator in denominators)
        foreach (int exponent in new[] { -2149, -1128, -1076, -1075, -1074, -1073, -1022, -53, 0, 970, 971, 972, 1023, 2047 })
        {
            var exact = new ReferenceFraction(numerator) / new ReferenceFraction(denominator);
            var power = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
            exact = exponent < 0 ? exact / power : exact * power;
            Bits(exact.ToDouble(), ExactMeanAccumulator.ScaledRatio(numerator, denominator, exponent));
        }
    }

    [Fact]
    public void FiniteTotalsSubnormalTiesOverflowingTotalsAndInvalidDivisorsRetainTheirContracts()
    {
        double[] values = [double.Epsilon, -double.Epsilon, 3 * double.Epsilon, -3 * double.Epsilon,
            Math.BitDecrement(Math.ScaleB(1, -1022)), Math.ScaleB(1, -1022),
            1, Math.BitIncrement(1d), double.MaxValue, -double.MaxValue];
        foreach (double value in values)
        foreach (int weight in new[] { 1, 2, 3, int.MaxValue })
        foreach (long divisor in new[] { 1L, 2, 3, 210, (1L << 53) - 1, 1L << 53, (1L << 53) + 1, long.MaxValue })
        {
            var sum = new ExactMeanAccumulator(); sum.Add(value, weight);
            var exact = ReferenceFraction.FromDouble(value) * new ReferenceFraction(weight) / new ReferenceFraction(divisor);
            Bits(exact.ToDouble(), sum.Mean(divisor));
        }
        var zero = new ExactMeanAccumulator(); zero.Add(-0d);
        Bits(0, zero.Mean(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => zero.Mean(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => zero.Mean(-1));
    }

    [Fact]
    public void RandomFiniteTotalsAndUnrepresentableResidualsMatchIndependentRationals()
    {
        var random = new Random(87193);
        for (int i = 0; i < 5000; i++)
        {
            var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            if (!double.IsFinite(value)) continue;
            long divisor = i % 2 == 0 ? random.NextInt64(1, 1L << 53) : random.NextInt64(1, long.MaxValue);
            var sum = new ExactMeanAccumulator(); sum.Add(value);
            var exact = ReferenceFraction.FromDouble(value);
            Bits((exact / new ReferenceFraction(divisor)).ToDouble(), sum.Mean(divisor));
            // A tiny residual can make the numerator unrepresentable even though
            // rounding it first would erase the distinction near a quotient tie.
            var residual = Math.ScaleB(1, random.Next(-1074, 1024));
            sum.Add(residual);
            exact += ReferenceFraction.FromDouble(residual);
            Bits((exact / new ReferenceFraction(divisor)).ToDouble(), sum.Mean(divisor));
        }
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
