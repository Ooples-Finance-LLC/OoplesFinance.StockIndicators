using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactWeightedAccumulatorTests
{
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
}
