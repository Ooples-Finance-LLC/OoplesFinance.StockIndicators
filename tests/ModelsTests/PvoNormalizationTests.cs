using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PvoNormalizationTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(-1d)]
    public void PercentageRetainsOnceRoundedDifferenceAndRatio(double sign)
    {
        double previous = sign * 0.8832541825499436, current = sign * 2.2520046369717046;
        Bar[] bars = [new(default, 1, 1, 1, 1, previous), new(default, 1, 1, 1, 1, current)];
        var owner = new EmaDifferenceSignal(1, 2, 1, percentage: true, volume: true);
        var expected = EmaDifferenceReference.Values(bars, owner);
        Assert.Equal(0x4045d40ea8c338efL, BitConverter.DoubleToInt64Bits(expected[0][1]!.Value));
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(expected[0][1]!.Value),
            BitConverter.DoubleToInt64Bits(100 * (current - (previous + current) / 2) / ((previous + current) / 2)));
        Check(owner, bars);
    }

    [Fact]
    public void NormalizationFallbackTransitionsRetainStagesMasksAndReset()
    {
        foreach (bool first in new[] { false, true })
        foreach (var (fast, slow) in new[] { (1, 1), (1, 2), (2, 5), (5, 2) })
        {
            var volumes = new List<double>();
            foreach (int exponent in new[] { 0, -1020, 1010, 0 })
                for (int i = 0; i < 50; i++)
                    volumes.Add(Math.ScaleB(i % 13 == 0 ? 0 : 100 + (i * 17 % 31) / 8d, exponent));
            volumes.AddRange(new[] { 0d, -0d, -1d, 1d, -2d, 2d, double.Epsilon, -double.Epsilon });
            var bars = volumes.Select(v => new Bar(default, 1, 1, 1, 1, v)).ToArray();
            Check(new EmaDifferenceSignal(fast, slow, 3, first, true, true), bars);
        }
    }

    private static void Check(EmaDifferenceSignal owner, Bar[] bars)
    {
        var expected = EmaDifferenceReference.Values(bars, owner);
        var state = (IMultiOutputState)owner.CreateState();
        var output = new double[10];
        for (int replay = 0; replay < 2; replay++)
        {
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
}
