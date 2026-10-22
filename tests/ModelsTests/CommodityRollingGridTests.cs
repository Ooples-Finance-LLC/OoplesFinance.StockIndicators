using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CommodityRollingGridTests
{
    [Theory]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(20, false)] [InlineData(20, true)]
    [InlineData(256, false)] [InlineData(256, true)]
    [InlineData(257, false)] [InlineData(257, true)]
    public void RollingGridAndFallbackTransitionsPreserveExactOutputs(int period, bool rolling)
    {
        // Fill and slide before forcing exponent-span, subnormal and extreme
        // fallbacks. Replay after reset starts the compact representation again.
        var prices = Enumerable.Range(0, 2 * period + 20)
            .Select(i => i < 4 ? 0d : (i % 3 == 0 ? -1d : 1d) * (100 + i % 17 / 8d))
            .Concat(new[] { Math.ScaleB(1d, 400), double.Epsilon, -double.MaxValue,
                double.MaxValue, -double.Epsilon, 0d })
            .Concat(Enumerable.Range(0, 2 * period + 4).Select(i => 100d + i % 7))
            .ToArray();
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var expected = CommodityChannelReference.Calculate(bars, period, rolling, false);
        var state = (IMultiOutputState)new WindowCommodityChannelIndex(period, rolling).CreateState();
        var output = new double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(bars[i], output);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0),
                    BitConverter.DoubleToInt64Bits(output[0]));
                Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
            }
        }
    }
}
