using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AccelerationGcdScaleTests
{
    [Fact]
    public void SharedBinaryScalePreservesIndependentReferenceAcrossSignsRangesAndReset()
    {
        double[] prices = [-double.MaxValue, -128d, -double.Epsilon, -0d, 0d,
            double.Epsilon, Math.ScaleB(1d, -1022), 3d, 100d, Math.BitIncrement(100d), double.MaxValue];
        Bar[] bars = prices.SelectMany(high => prices.Select(low =>
            new Bar(default, 0, high, low, 0, 1))).ToArray();
        Span<double> actual = stackalloc double[6];
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        {
            double?[][] expected = RangeAccelerationReference.Values(bars, period);
            var state = new RangeAccelerationBands.State(period);
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(bars[i], actual);
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                            BitConverter.DoubleToInt64Bits(actual[slot]));
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, actual[slot + 3]);
                    }
                }
            }
        }
    }
}
