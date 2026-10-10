using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WideRootCertificateTests
{
    [Fact]
    public void FullWidthOperandsMatchIndependentRootsAcrossNormalExponents()
    {
        var random = new Random(7251);
        for (var i = 0; i < 2000; i++)
        {
            var numerator = (((ulong)random.NextInt64() | (1UL << 63)) >> random.Next(64)) | 1;
            var denominator = (((ulong)random.NextInt64() | (1UL << 63)) >> random.Next(64)) | 1;
            var power = 2 * random.Next(-970, 971);
            var expected = Expected(numerator, denominator, power);
            Assert.True(ExactPopulationDeviation.TryWideScaledRoot(numerator, denominator, power, out var actual));
            Bits(expected, actual);
            Bits(expected, ExactPopulationDeviation.ScaledRootRatio(numerator, denominator, power));
        }
    }

    [Fact]
    public void WordBoundariesAndPowerOfTwoNeighborsKeepCorrectRounding()
    {
        ulong[] values = [1, 2, 3, uint.MaxValue, 1UL << 32, (1UL << 53) + 1,
            (1UL << 63) - 1, 1UL << 63, (1UL << 63) + 1, ulong.MaxValue - 1, ulong.MaxValue];
        foreach (var numerator in values)
        foreach (var denominator in values)
        foreach (var power in new[] { -1900, -2, 0, 2, 1900 })
        {
            Assert.True(ExactPopulationDeviation.TryWideScaledRoot(numerator, denominator, power, out var actual));
            Bits(Expected(numerator, denominator, power), actual);
        }
    }

    [Fact]
    public void OutOfCertificateRangesKeepTheGeneralRoundingPath()
    {
        foreach (var power in new[] { -4300, -2148, -2147, -2048, -1, 1, 2048, 4090 })
        {
            Assert.False(ExactPopulationDeviation.TryWideScaledRoot(1, 1, power, out _));
            Bits(Expected(1, 1, power), ExactPopulationDeviation.ScaledRootRatio(1, 1, power));
        }
        Assert.False(ExactPopulationDeviation.TryWideScaledRoot(0, 1, 0, out _));
        Assert.False(ExactPopulationDeviation.TryWideScaledRoot(1, 0, 0, out _));
        var beyond = BigInteger.One << 80;
        Bits(Expected(beyond + 1, beyond - 1, 0), ExactPopulationDeviation.ScaledRootRatio(beyond + 1, beyond - 1, 0));
    }

    [Fact]
    public void CompactFullWidthRootPublicationDoesNotAllocate()
    {
        var numerator = new BigInteger(ulong.MaxValue - 13);
        var denominator = new BigInteger((1UL << 63) + 159);
        var expected = Expected(numerator, denominator, 0);
        for (var i = 0; i < 100; i++) ExactPopulationDeviation.ScaledRootRatio(numerator, denominator, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        double difference = 0;
        for (var i = 0; i < 1000; i++) difference += ExactPopulationDeviation.ScaledRootRatio(numerator, denominator, 0) - expected;
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, difference);
    }

    private static double Expected(BigInteger numerator, BigInteger denominator, int power)
    {
        var ratio = new ReferenceFraction(numerator) / new ReferenceFraction(denominator);
        var scale = new ReferenceFraction(BigInteger.One << Math.Abs(power));
        return (power < 0 ? ratio / scale : ratio * scale).SqrtToDouble();
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
