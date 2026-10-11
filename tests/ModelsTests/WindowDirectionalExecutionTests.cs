using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WindowDirectionalExecutionTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    [InlineData(20, 2)]
    [InlineData(int.MaxValue, 2)]
    public void CompactStagesPreserveReferenceBitsAndReset(int period, int lag)
    {
        var ordinary = Enumerable.Range(0, 81).Select(i => new Bar(default, 0,
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
        var flatThenMoving = Enumerable.Repeat(new Bar(default, 0, 10, -10, 0, 1), 25)
            .Concat(ordinary).ToArray();
        var ties = Enumerable.Range(0, 45).Select(i => new Bar(default, 0, 10 + i, -10 - i, 0, 1)).ToArray();
        var states = Enumerable.Range(0, 7).Select(i => (IMultiOutputState)
            new WindowDirectionalMeasure((DirectionalWindowMeasure)i, period, lag).CreateState()).ToArray();
        var output = new double[2];
        foreach (var bars in new[] { ordinary, extreme, extreme.Reverse().ToArray(), flatThenMoving, ties })
        {
            var expected = DirectionalMeanReference.Calculate(bars, period, lag);
            for (int measure = 0; measure < states.Length; measure++)
            {
                states[measure].Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    states[measure].Update(in bars[i], output);
                    Assert.Equal(expected[measure][i].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[measure][i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[0]));
                }
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
    public void OrdinaryStreamingUpdatesAllocateNoPerBarObjects(int measure)
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0,
            100 + i / 8d, 98 + i / 8d, 99 + i / 8d, 1)).ToArray();
        var state = (IMultiOutputState)new WindowDirectionalMeasure((DirectionalWindowMeasure)measure, 20, 2).CreateState();
        var output = new double[2];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, output[1]);
    }
}
