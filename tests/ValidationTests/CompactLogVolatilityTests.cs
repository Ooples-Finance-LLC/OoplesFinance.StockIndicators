using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactLogVolatilityTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void ExactReturnsGridChangesSkippedReturnsAndPoisoningMatchReference(bool realized, bool annualized)
    {
        double[][] fixtures = [
            [1, 2, 4, 8, 16, 0, 0, 0, 32, 16, 8, 4, 2, 1],
            [1, -1, -2, -4, -8, -16, -32, 0, 1, 2, 3, 4, 5, 6],
            [1, 1, 1, 1, Math.BitIncrement(1), 2, Math.BitIncrement(2), 4, 8, 16, 32, 64],
            [double.Epsilon, double.MaxValue, 1, Math.BitIncrement(1), double.Epsilon, 1, 2, 4, 8, 16]
        ];
        foreach (int period in new[] { 2, 4, int.MaxValue })
        foreach (var prices in fixtures)
        {
            var bars = prices.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
            var expected = LogVolatilityReference.Values(bars, period, realized, annualized);
            var state = new WindowLogVolatility.State(period, realized, annualized);
            var output = new double[2];
            for (int pass = 0; pass < 2; pass++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                    Assert.Equal(expected[i].HasValue ? 1d : 0, output[1]);
                }
            }
        }
    }
}
