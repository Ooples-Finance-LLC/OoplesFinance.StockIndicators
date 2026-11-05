using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SeededHilbertMidpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactMidpointFallbackTransitionsRetainAllStagesAndReset(bool midpoint)
    {
        double boundary = Math.ScaleB(1d, -1021);
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.BitDecrement(boundary), boundary, Math.BitIncrement(boundary),
            -Math.BitDecrement(boundary), -boundary, -Math.BitIncrement(boundary),
            Math.BitDecrement(1d), 1d, Math.BitIncrement(1d), double.MaxValue, -double.MaxValue];
        var bars = new List<Bar>();
        foreach (double high in values)
        foreach (double low in values)
            bars.Add(new(DateTime.UnixEpoch.AddMinutes(bars.Count), 100, high, low,
                100 + bars.Count % 13 / 8d, 1));
        for (int i = 0; i < 129; i++)
        {
            double close = 100 + (i * 17 % 31) / 8d;
            bars.Add(new(DateTime.UnixEpoch.AddMinutes(bars.Count), close, close + 2, close - 1, close, 1));
        }
        var expected = SeededHilbertTrendReference.Values(bars, midpoint);
        var state = (IMultiOutputState)new SeededHilbertTrendline(midpoint).CreateState();
        var output = new double[6];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                state.Update(in bar, output);
                for (int slot = 0; slot < 3; slot++)
                {
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 3]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }
}
