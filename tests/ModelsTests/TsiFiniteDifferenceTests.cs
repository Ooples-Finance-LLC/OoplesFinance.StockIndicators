using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TsiFiniteDifferenceTests
{
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 1, 3)]
    [InlineData(7, 3, 5)]
    public void RoundedDifferencesAndExtendedTransitionsMatchIndependentHistoryAfterReset(
        int period, int smooth, int signal)
    {
        double tiny = Math.ScaleB(1, -1022);
        double[] boundaries = [0, -0d, double.Epsilon, -double.Epsilon, tiny, -tiny,
            1, -Math.ScaleB(1, -53), Math.BitIncrement(1), Math.BitDecrement(2),
            2, -1, double.MaxValue, -double.MaxValue, double.MaxValue, 0];
        double[] ordinary = Enumerable.Range(0, 96).Select(i => 100 + (i * 37 % 193) / 32d).ToArray();
        var histories = new[]
        {
            ordinary,
            boundaries.Concat(ordinary).ToArray(),
            ordinary.Concat(boundaries).Concat(ordinary).ToArray(),
            boundaries.Reverse().Concat(ordinary).ToArray(),
            Enumerable.Repeat(-0d, 30).Concat(boundaries).Concat(ordinary).ToArray(),
            ordinary
        };
        var state = (IMultiOutputState)new SeededTrueStrength(period, smooth, signal).CreateState();
        var output = new double[4];
        foreach (var prices in histories)
        {
            var bars = prices.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
            var expected = SeededMomentumReference.Tsi(bars, period, smooth, signal);
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 2; slot++)
                {
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 2]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }
}
