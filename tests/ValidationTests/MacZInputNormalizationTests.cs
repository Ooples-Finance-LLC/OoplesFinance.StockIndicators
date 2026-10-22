using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MacZInputNormalizationTests
{
    private static MacZWindow.Number Legacy(double value) =>
        (MacZWindow.Number)Activator.CreateInstance(typeof(MacZWindow.Number),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { ExactVarianceWindow.Units(value), -1074, BigInteger.One, false }, null)!;

    private static void EqualValue(MacZWindow.Number expected, MacZWindow.Number actual)
    {
        var a = expected.ToFraction(); var b = actual.ToFraction();
        Assert.Equal(a.Numerator * b.Denominator, b.Numerator * a.Denominator);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Publish()), BitConverter.DoubleToInt64Bits(actual.Publish()));
    }

    [Fact]
    public void EveryBinaryExponentRetainsItsExactValueAndPublication()
    {
        var random = new Random(7232029);
        for (int exponent = 0; exponent < 2047; exponent++)
        foreach (long fraction in new[] { 0L, 1L, (1L << 52) - 1, random.NextInt64(1L << 52) })
        foreach (long sign in new[] { 0L, long.MinValue })
        {
            double value = BitConverter.Int64BitsToDouble(sign | ((long)exponent << 52) | fraction);
            EqualValue(Legacy(value), MacZWindow.Number.Of(value));
        }
    }

    [Fact]
    public void MixedScaleArithmeticAndRootRoundingRetainLegacyValues()
    {
        double[] inputs = [0, -0d, 0.1, -3.25, Math.BitIncrement(1), double.Epsilon,
            -double.Epsilon, Math.ScaleB(1, -1022), double.MaxValue, -double.MaxValue];
        foreach (double left in inputs)
        foreach (double right in inputs)
        {
            var a = MacZWindow.Number.Of(left); var b = MacZWindow.Number.Of(right);
            var x = Legacy(left); var y = Legacy(right);
            EqualValue(x + y, a + b); EqualValue(x - y, a - b); EqualValue(x * y, a * b);
            if (right != 0) EqualValue(x.Divide(y), a.Divide(b));
            EqualValue((x + y).Round(), (a + b).Round());
            EqualValue((x + y).Round(72), (a + b).Round(72));
            var square = b * b + MacZWindow.Number.Integer(3);
            var oldSquare = y * y + MacZWindow.Number.Integer(3);
            EqualValue(x.OverRoot(oldSquare), a.OverRoot(square));
            EqualValue(x.OverDeviation(3, 7), a.OverDeviation(3, 7));
        }
    }

    [Fact]
    public void RecursiveRoundingRetainsLegacyHistory()
    {
        var current = MacZWindow.Number.Of(.1); var old = Legacy(.1);
        for (int i = 0; i < 256; i++)
        {
            double value = i % 13 - 6.125;
            current = (current.Times(19) + MacZWindow.Number.Of(value).Times(2)).Divide(21).Round();
            old = (old.Times(19) + Legacy(value).Times(2)).Divide(21).Round();
            EqualValue(old, current);
        }
    }

    [Fact]
    public void SmootherPreviewCommitAndResetRetainLegacyValues()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (int period in new[] { 1, 2, 20, int.MaxValue })
        {
            using var current = new MacZWindow.Average(kind, period);
            using var old = new MacZWindow.Average(kind, period);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                current.Reset(); old.Reset();
                for (int i = 0; i < 64; i++)
                {
                    double value = i % 11 - 5.125;
                    EqualValue(old.Next(Legacy(-value), false), current.Next(MacZWindow.Number.Of(-value), false));
                    EqualValue(old.Next(Legacy(value), true), current.Next(MacZWindow.Number.Of(value), true));
                }
            }
        }
    }

    [Fact]
    public void NonfiniteInputsRetainTheirExceptionContract()
    {
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => MacZWindow.Number.Of(value)).ParamName);
    }

    [Fact]
    public void OrdinaryInputConstructionAvoidsWideIntegerAllocation()
    {
        for (int i = 0; i < 64; i++) _ = MacZWindow.Number.Of(100.125);
        long start = GC.GetAllocatedBytesForCurrentThread(); int signs = 0;
        for (int i = 0; i < 1000; i++) signs += MacZWindow.Number.Of(100.125 + i % 7).Sign;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(1000, signs);
        Assert.InRange(allocated, 0, 64_000);
    }
}
