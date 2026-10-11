using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PositiveStrengthPublicationTests
{
    private static ReferenceFraction Exact(long mantissa, int exponent)
    {
        var value = new ReferenceFraction(mantissa);
        var scale = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
        return exponent < 0 ? value / scale : value * scale;
    }

    [Fact]
    public void PublicationMatchesRationalReferenceAcrossWideAndTinyUnpublishedValues()
    {
        var random = new Random(711);
        for (int i = 0; i < 1000; i++)
        {
            long up = random.NextInt64(1, 1L << 53);
            long down = random.NextInt64(1, 1L << 53);
            int gap = i < 500 ? random.Next(-60, 61) : random.Next(-5000, 5001);
            var a = Exact(up, gap);
            var b = Exact(down, 0);
            // Absolute exponents are irrelevant to the ratio. Exercise values
            // well outside both binary64 limits and near the long-domain limits.
            long common = (i % 3) switch { 0 => -10000, 1 => long.MinValue + 10000, _ => long.MaxValue - 10000 };
            var gain = new PositiveStrengthValue(up, common + gap);
            var loss = new PositiveStrengthValue(down, common);
            Bits((a / b).ToDouble(), PositiveStrengthValue.Quotient(gain, loss));
            Bits((new ReferenceFraction(100) * a / (a + b)).ToDouble(),
                PositiveStrengthValue.Percent(gain, loss, false));
            Bits((new ReferenceFraction(100) * (a - b) / (a + b)).ToDouble(),
                PositiveStrengthValue.Percent(gain, loss, true));
        }
    }

    [Fact]
    public void ExactTiesZeroEndpointsAndSignedCancellationArePreserved()
    {
        var one = new PositiveStrengthValue(1, -10000);
        Bits(1, PositiveStrengthValue.Quotient(one, one));
        Bits(50, PositiveStrengthValue.Percent(one, one, false));
        Bits(0, PositiveStrengthValue.Percent(one, one, true));
        Bits(0, PositiveStrengthValue.Percent(default, one, false));
        Bits(-100, PositiveStrengthValue.Percent(default, one, true));
        Bits(100, PositiveStrengthValue.Percent(one, default, true));
        Bits(0, PositiveStrengthValue.Quotient(default, one));
        Assert.Throws<DivideByZeroException>(() => PositiveStrengthValue.Quotient(one, default));
        var halfSubnormal = new PositiveStrengthValue(1, -11075);
        Bits(0, PositiveStrengthValue.Quotient(halfSubnormal, one));
        Bits(2 * double.Epsilon, PositiveStrengthValue.Quotient(new(3, -11075), one));
    }

    [Fact]
    public void OrdinaryPublicationsAllocateNoObjects()
    {
        var gain = new PositiveStrengthValue((1L << 52) + 3, -54);
        var loss = new PositiveStrengthValue((1L << 52) + 7, -56);
        double result = 0;
        void Publish()
        {
            result = PositiveStrengthValue.Quotient(gain, loss);
            result += PositiveStrengthValue.Percent(gain, loss, false);
            result += PositiveStrengthValue.Percent(gain, loss, true);
        }
        for (int i = 0; i < 1000; i++) Publish();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) Publish();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(result));
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
