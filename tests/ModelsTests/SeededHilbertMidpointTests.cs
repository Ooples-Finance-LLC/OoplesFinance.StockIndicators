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
        // The dyadic stages canonicalize zero before the filter/publication. The
        // rational publisher can retain negative zero after underflow instead.
        var referenceBars = bars.Select(bar =>
        {
            double price = midpoint
                ? ((ReferenceFraction.FromDouble(bar.High) + ReferenceFraction.FromDouble(bar.Low))
                    / new ReferenceFraction(2)).ToDouble()
                : bar.Close;
            if (BitConverter.DoubleToInt64Bits(price) == long.MinValue) price = 0;
            return new Bar(bar.Time, price, price, price, price, bar.Volume);
        }).ToArray();
        var expected = SeededHilbertTrendReference.Values(referenceBars, false)
            .Select(row => row.Select(value => value.HasValue
                && BitConverter.DoubleToInt64Bits(value.Value) == long.MinValue ? 0d : value).ToArray())
            .ToArray();
        var state = (IMultiOutputState)new SeededHilbertTrendline(midpoint).CreateState();
        var output = new double[6];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                state.Update(in bar, output);
                if (midpoint && i == 3)
                    Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[0]));
                if (midpoint && i == 48)
                    Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[1]));
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
