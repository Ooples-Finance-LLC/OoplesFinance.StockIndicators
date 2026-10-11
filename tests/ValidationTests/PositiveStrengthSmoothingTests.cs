using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PositiveStrengthSmoothingTests
{
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
    public void BothStrengthFamiliesAllocateNoPerBarObjectsAfterSeeding(int kind)
    {
        var state = (IMultiOutputState)(kind < 5
            ? new NullableStrengthOscillator(14, (NullableStrengthConvention)kind,
                kind >= 3 ? 2 : 1).CreateState()
            : new WilderStrengthOscillator(14, (WilderStrengthConvention)(kind - 5)).CreateState());
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 0, 0,
            100 + (i * 13 % 101) / 8d, 1)).ToArray();
        var output = new double[2];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, output[1]);
    }
}
