using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PositiveStrengthSmoothingTests
{
    [Fact]
    public void CompactRecurrenceMantissaCarryAndHalfwayCasesMatchWideRounding()
    {
        foreach (long mantissa in new[] { (1L << 52) + 1, (1L << 53) - 3, (1L << 53) - 1 })
        foreach (double change in new[] { Math.BitDecrement(Math.ScaleB(1, 53)), Math.ScaleB(1, 53),
            Math.BitIncrement(Math.ScaleB(1, 53)) })
        foreach (bool exponential in new[] { false, true })
        {
            var prior = new PositiveStrengthValue(mantissa, 0);
            var units = ExactVarianceWindow.Units(change);
            var expected = exponential ? prior.UpdateExponential(units, 2) : prior.Update(units, 2);
            Assert.Equal(expected, prior.Update(change, 2, exponential));
        }
    }

    [Fact]
    public void CertifiedDifferencesAreExactAtSterbenzAndSubnormalBoundaries()
    {
        double[] values = [0, -0d, double.Epsilon, 3 * double.Epsilon,
            double.BitDecrement(1), 1, double.BitIncrement(1), 2,
            double.BitIncrement(2), double.MaxValue / 2, double.MaxValue];
        foreach (var a in values)
        foreach (var b in values)
        foreach (var signA in new[] { -1, 1 })
        foreach (var signB in new[] { -1, 1 })
        {
            double current = signA * a, previous = signB * b;
            var exact = ExactVarianceWindow.Units(current) - ExactVarianceWindow.Units(previous);
            if (PositiveStrengthValue.TryDifference(current, previous, out var difference))
            {
                Assert.True(double.IsFinite(difference));
                Assert.Equal(exact, ExactVarianceWindow.Units(difference));
                if (exact.IsZero) Assert.Equal(0L, BitConverter.DoubleToInt64Bits(difference));
            }
        }
    }

    [Fact]
    public void GuardedIntegerRecurrencesPreserveProductShiftAndSumBoundaries()
    {
        foreach (int period in new[] { 1, 2, 3, 20, 1024, 1025, 4096, int.MaxValue })
        foreach (long mantissa in new[] { 0L, 1L << 52, (1L << 53) - 1 })
        foreach (long exponent in new[] { -62L, -52L, 10L })
        foreach (double change in new[] { 0d, double.Epsilon, .5, 1, 4, 1024 })
        foreach (bool exponential in new[] { false, true })
        {
            var prior = new PositiveStrengthValue(mantissa, exponent);
            var units = ExactVarianceWindow.Units(change);
            var expected = exponential ? prior.UpdateExponential(units, period) : prior.Update(units, period);
            Assert.Equal(expected, prior.Update(change, period, exponential));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void CompactSeedPreservesUnpublishedRangeAndExactWideSums(int period)
    {
        double[][] inputs = [[0], [double.Epsilon], [double.MaxValue, double.MaxValue],
            [1, double.Epsilon], [1.25, .125, .375], [double.MaxValue, double.Epsilon, 1]];
        foreach (var values in inputs)
        {
            var sum = new ExactMeanAccumulator();
            var units = System.Numerics.BigInteger.Zero;
            foreach (var value in values) { sum.Add(value); units += ExactVarianceWindow.Units(value); }
            var before = sum.Mean(1);
            Assert.Equal(PositiveStrengthValue.Seed(units, period), PositiveStrengthValue.Seed(sum, period));
            Assert.Equal(BitConverter.DoubleToInt64Bits(before), BitConverter.DoubleToInt64Bits(sum.Mean(1)));
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(14, false)]
    [InlineData(int.MaxValue, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(14, true)]
    [InlineData(int.MaxValue, true)]
    public void CompactUpdatesMatchWideRecurrenceAboveAndBelowBinary64Range(int period, bool exponential)
    {
        var random = new Random(329);
        double[] changes = [0, double.Epsilon, double.MaxValue, .125, 0, 1, 0];
        changes = changes.Concat(Enumerable.Range(0, 81)
            .Select(_ => Math.ScaleB(random.NextDouble(), random.Next(-1074, 1024)))).ToArray();
        foreach (long exponent in new long[] { -5000, -1074, -52, 971, 5000 })
        {
            var expected = new PositiveStrengthValue((1L << 52) + 3, exponent);
            var actual = expected;
            // Zero updates must retain means even when they are too tiny to publish.
            foreach (var change in Enumerable.Repeat(0d, 37).Concat(changes))
            {
                var units = ExactVarianceWindow.Units(change);
                expected = exponential ? expected.UpdateExponential(units, period) : expected.Update(units, period);
                actual = actual.Update(change, period, exponential);
                Assert.Equal(expected, actual);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void BothStrengthFamiliesAllocateNoPerBarObjectsIncludingResetStartup(int kind)
    {
        var state = (IMultiOutputState)(kind < 5
            ? new NullableStrengthOscillator(14, (NullableStrengthConvention)kind,
                kind >= 3 ? 2 : 1).CreateState()
            : new WilderStrengthOscillator(14, (WilderStrengthConvention)(kind - 5)).CreateState());
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 0, 0,
            100 + (i * 13 % 101) / 8d, 1)).ToArray();
        var output = new double[2];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        state.Reset();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, output[1]);
    }
}
