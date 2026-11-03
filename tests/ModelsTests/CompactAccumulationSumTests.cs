using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactAccumulationSumTests
{
    [Fact]
    public void ExactPrefixesRetainTinyResidualsAndExtendedCancellation()
    {
        RocBankValue[] extremes = [new(double.MaxValue), new(double.MaxValue),
            new(-double.MaxValue), new(-double.MaxValue), new(double.Epsilon),
            new(1, 1024), new(-1, 1024), new(-double.Epsilon), new(-0d)];
        Check(extremes);
        var random = new Random(70103);
        Check(Enumerable.Range(0, 4000).Select(i =>
        {
            int exponent = i % 5 == 0 ? random.Next(-1074, 1024) : random.Next(-10, 20);
            double value = Math.ScaleB((random.NextDouble() * 2 - 1), exponent);
            return new RocBankValue(value);
        }));
        // Ordinary flow totals exceed binary64 precision without exceeding
        // the compact integer width; preserve all low bits until publication.
        Check(Enumerable.Range(0, 4000).Select(i => new RocBankValue(
            i % 3 == 0 ? 1e12 : i % 3 == 1 ? .00003125 : -1e12)));
    }

    private static void Check(IEnumerable<RocBankValue> values)
    {
        var sum = new CompactAccumulationSum();
        var reference = new ExactMeanAccumulator();
        foreach (var value in values)
        {
            // Preview updates must not mutate a committed sum, including its
            // wide representation. This is the streaming final=false contract.
            var preview = sum;
            preview.Add(new RocBankValue(double.MaxValue, 1024));
            AssertBits(reference.Mean(1), sum.Publish());
            sum.Add(value);
            value.AddTo(ref reference);
            AssertBits(reference.Mean(1), sum.Publish());
            var expected = RocBankValue.Round(reference);
            var actual = sum.Rounded();
            Assert.Equal(expected.UpperShift, actual.UpperShift);
            AssertBits(expected.Mantissa, actual.Mantissa);
        }
        sum = default;
        sum.Add(-0d);
        AssertBits(0, sum.Publish());
        sum.Add(1d);
        AssertBits(1, sum.Publish());
    }

    private static void AssertBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
