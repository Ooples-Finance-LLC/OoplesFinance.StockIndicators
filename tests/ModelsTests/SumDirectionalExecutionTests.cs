using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SumDirectionalExecutionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public void CompactStagesPreserveReferenceBitsAndReset(int period)
    {
        var ordinary = Enumerable.Range(0, 121).Select(i => new Bar(default, 0,
            100 + (i * 17 % 31) / 8d, 95 - (i * 7 % 19) / 8d, 98, 1)).ToArray();
        Bar[] extreme =
        [
            new(default, 0, 10, -10, 0, 1),
            new(default, 0, double.MaxValue, -double.MaxValue, 1, 1),
            new(default, 0, 1, -1, -double.MaxValue, 1),
            new(default, 0, double.MaxValue, double.MaxValue, 2, 1),
            new(default, 0, -5, 7, 0, 1),
            new(default, 0, double.Epsilon, -double.Epsilon, double.Epsilon, 1),
            new(default, 0, -0d, 0d, -0d, 1)
        ];
        var missedSeed = new[] { 0, double.Epsilon, double.Epsilon, double.Epsilon,
            2 * double.Epsilon, 3 * double.Epsilon, 4 * double.Epsilon, 5 * double.Epsilon }
            .Select(v => new Bar(default, v, v, v, v, 0)).ToArray();
        var ties = Enumerable.Range(0, 61).Select(i => new Bar(default, 0, 10 + i, -10 - i, 0, 1)).ToArray();
        var random = new Random(127);
        var varied = Enumerable.Range(0, 151).Select(_ =>
        {
            double Value() => Math.ScaleB(random.NextDouble() - .5, random.Next(-1070, 1024));
            return new Bar(default, 0, Value(), Value(), Value(), 1);
        }).ToArray();
        var state = (IMultiOutputState)new SumSeededDirectionalIndex(period).CreateState();
        var output = new double[10];
        foreach (var bars in new[] { ordinary, extreme.Concat(ordinary).ToArray(),
            extreme.Reverse().Concat(ordinary).ToArray(), missedSeed, ties, varied, ordinary })
        {
            var expected = SumDirectionalReference.Calculate(bars, period);
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 5; slot++)
                {
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 5]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }

    [Fact]
    public void OrdinaryStreamingUpdatesAllocateNoPerBarObjects()
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0,
            100 + i / 8d, 98 + i / 8d, 99 + i / 8d, 1)).ToArray();
        var state = (IMultiOutputState)new SumSeededDirectionalIndex(14).CreateState();
        var output = new double[10];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.All(output.Skip(5), value => Assert.Equal(1d, value));
    }
}
