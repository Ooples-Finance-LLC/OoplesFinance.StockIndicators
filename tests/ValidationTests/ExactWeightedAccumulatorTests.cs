using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactWeightedAccumulatorTests
{
    [Fact]
    public void CompactScaledRatiosDoNotAllocate()
    {
        // Cancel denominator powers of two before sizing the division workspace.
        static double Run() => ExactMeanAccumulator.ScaledRatio(305235, 31920, 0);
        for (var i = 0; i < 100; i++) Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        double total = 0;
        for (var i = 0; i < 1000; i++) total += Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(9562.5, total);
    }

    [Fact]
    public void ScaledRatiosMatchIndependentRationalsAtSignedIntegerBoundaries()
    {
        var boundary = System.Numerics.BigInteger.One << 63;
        foreach (var numerator in new[] { -boundary - 1, -boundary, -boundary + 1, -1, 0, 1, boundary - 1, boundary, boundary + 1 })
        foreach (var denominator in new[] { System.Numerics.BigInteger.One, 3, boundary - 1, boundary, boundary + 1 })
        foreach (var power in new[] { -2149, -1075, -1074, -63, 0, 63, 1023, 2047 })
        {
            var expected = new ReferenceFraction(numerator) / new ReferenceFraction(denominator);
            var scale = new ReferenceFraction(System.Numerics.BigInteger.One << Math.Abs(power));
            expected = power < 0 ? expected / scale : expected * scale;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.ToDouble()),
                BitConverter.DoubleToInt64Bits(ExactMeanAccumulator.ScaledRatio(numerator, denominator, power)));
        }
    }

    [Fact]
    public void WideWeightedMeansRoundMidpointsToEven()
    {
        // Multiplication by this odd divisor forces a >64-bit significand,
        // while the exact quotient lies halfway between adjacent doubles.
        const int weight = int.MaxValue;
        var halfUlp = Math.ScaleB(1, -53);
        foreach (var sign in new[] { -1, 1 })
        foreach (var offset in new[] { 0, 1 })
        {
            var baseValue = 1 + offset * Math.ScaleB(1, -52);
            var sum = new ExactMeanAccumulator();
            sum.Add(sign * baseValue, weight);
            sum.Add(sign * halfUlp, weight);
            var expected = sign * (1 + (offset == 0 ? 0 : Math.ScaleB(1, -51)));
            Assert.Equal(expected, sum.Mean(weight));
        }
    }

    [Fact]
    public void MixedGridsWeightsAndDivisorsMatchIndependentRationals()
    {
        var random = new Random(244);
        for (var trial = 0; trial < 500; trial++)
        {
            var actual = new ExactMeanAccumulator();
            var expected = new ReferenceFraction(0);
            for (var term = 0; term < 8; term++)
            {
                var bits = random.NextInt64(long.MinValue, long.MaxValue);
                var value = BitConverter.Int64BitsToDouble(bits);
                if (!double.IsFinite(value)) value = double.Epsilon;
                var weight = term % 3 == 0 ? int.MaxValue : random.Next(-20, 21);
                actual.Add(value, weight);
                expected += ReferenceFraction.FromDouble(value) * new ReferenceFraction(weight);
            }
            var divisor = trial % 3 == 0 ? long.MaxValue : random.NextInt64(1, 100000);
            Assert.Equal((expected / new ReferenceFraction(divisor)).ToDouble(), actual.Mean(divisor));
        }
    }

    [Fact]
    public void EvictionAcrossExponentSpansRetainsTheExactRemainingValue()
    {
        foreach (var small in new[] { double.Epsilon, -double.Epsilon, 1d, -1d, .1 })
        {
            var total = new ExactMeanAccumulator();
            var removed = new ExactMeanAccumulator();
            total.Add(small); total.Add(double.MaxValue, 7);
            removed.Add(double.MaxValue, 7);
            total.Subtract(removed);
            Assert.Equal(small, total.Mean(1));
            total.Add(-small);
            Assert.Equal(0, total.Mean(1));
            total.Add(100);
            Assert.Equal(100, total.Mean(1));
        }
    }

    [Fact]
    public void OrdinaryWeightedUpdatesDoNotAllocateAfterWarmup()
    {
        static double Run()
        {
            var total = new ExactMeanAccumulator();
            total.Add(100.125, 14); total.Add(99.375, 2);
            return total.Mean(16);
        }
        for (var i = 0; i < 100; i++) Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = 0d;
        for (var i = 0; i < 1000; i++) result += Run();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(100031.25, result);
    }

    [Fact]
    public void NormalizedProductsAndRatioDenominatorsDoNotAllocate()
    {
        static double Run()
        {
            var numerator = new ExactMeanAccumulator();
            var denominator = new ExactMeanAccumulator();
            numerator.AddProduct(10.125, 3); numerator.AddProduct(11.25, -1);
            denominator.Add(3); denominator.Add(-1);
            return numerator.Ratio(denominator);
        }
        for (var i = 0; i < 100; i++) Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        double total = 0;
        for (var i = 0; i < 1000; i++) total += Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(9562.5, total);
    }

    [Fact]
    public void CanonicalOperandsPreserveProductsBigWeightsAndMultiplication()
    {
        var random = new Random(517);
        for (var trial = 0; trial < 256; trial++)
        {
            var actual = new ExactMeanAccumulator();
            var expected = new ReferenceFraction(0);
            for (var term = 0; term < 8; term++)
            {
                var left = Math.ScaleB(random.Next(-4096, 4097) / 8d, random.Next(-1000, 1001));
                var right = Math.ScaleB(random.Next(-4096, 4097) / 8d, random.Next(-1000, 1001));
                var weight = term % 2 == 0 ? int.MinValue : int.MaxValue;
                actual.AddProduct(left, right, weight);
                expected += ReferenceFraction.FromDouble(left) * ReferenceFraction.FromDouble(right) * new ReferenceFraction(weight);
            }
            var bigWeight = (System.Numerics.BigInteger.One << 90) + 3;
            actual.Add(.125, bigWeight);
            expected += ReferenceFraction.FromDouble(.125) * new ReferenceFraction(bigWeight);
            actual.Multiply(-.5);
            expected *= ReferenceFraction.FromDouble(-.5);
            var divisor = trial + 1;
            Assert.Equal(BitConverter.DoubleToInt64Bits((expected / new ReferenceFraction(divisor)).ToDouble()),
                BitConverter.DoubleToInt64Bits(actual.Mean(divisor)));
        }
    }
}
