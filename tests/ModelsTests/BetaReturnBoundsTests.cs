using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class BetaReturnBoundsTests
{
    [Theory]
    [InlineData(-1021)] [InlineData(0)] [InlineData(1022)]
    public void PriceBoundsAndAdjacentFallbacksPreserveIndependentReference(int exponent)
    {
        double unit = Math.ScaleB(1, exponent);
        double[] prices = [unit, unit * .5, Math.BitDecrement(unit * .5),
            unit, unit * 2, Math.BitIncrement(unit * 2), unit,
            0, double.Epsilon, Math.ScaleB(1, -1022), double.MaxValue, unit];
        foreach (int sign in new[] { -1, 1 })
        foreach (bool full in new[] { false, true })
        {
            var bars = prices.Select((price, i) => new Bar(default, sign * price,
                0, 0, sign * prices[(i + 2) % prices.Length], 1)).ToArray();
            var state = (IMultiOutputState)(full
                ? new WindowBetaStatistics(3, ReturnBetaSelection.All).CreateState()
                : new WindowReturnBeta(3, CandlePriceField.Open, CandlePriceField.Close).CreateState());
            var expected = ReturnBetaReference.Calculate(bars, 3, CandlePriceField.Open,
                CandlePriceField.Close, ReturnBetaSelection.All, full);
            var output = new double[full ? 14 : 2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    Array.Fill(output, double.NaN);
                    state.Update(in bars[i], output);
                    for (int slot = 0; slot < expected.Length; slot++)
                    {
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0d,
                            output[slot + expected.Length]);
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                            BitConverter.DoubleToInt64Bits(output[slot]));
                    }
                }
            }
        }
    }
}
