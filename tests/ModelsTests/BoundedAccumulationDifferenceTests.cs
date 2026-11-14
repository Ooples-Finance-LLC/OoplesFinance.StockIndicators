using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BoundedAccumulationDifferenceTests
{
    [Fact]
    public async Task NormalExponentSweepMatchesIndependentFractions()
    {
        var bars = new List<Bar>();
        for (int exponent = -1021; exponent <= 1023; exponent++)
        {
            double high = Math.ScaleB(1.5, exponent), low = high * .5;
            foreach (double close in new[] { low, Math.BitIncrement(low), high, Math.BitDecrement(high) })
                bars.Add(new(default, 0, high, low, close, bars.Count % 2 == 0 ? .1 : -.1));
        }
        await Check(bars.ToArray());
    }

    [Fact]
    public async Task FactorTwoAndBinadeBoundariesMatchIndependentFractions()
    {
        var bars = new List<Bar>();
        foreach (int exponent in new[] { -1021, -1000, -53, -1, 0, 53, 500, 1023 })
        {
            double pivot = Math.ScaleB(1, exponent);
            foreach (double high in new[] { Math.BitDecrement(pivot), pivot, Math.BitIncrement(pivot) })
            foreach (double low in new[] { Math.BitDecrement(high * .5), high * .5, Math.BitIncrement(high * .5) })
            foreach (double close in new[] { low, Math.BitIncrement(low), low + (high - low) * .5, Math.BitDecrement(high), high })
                bars.Add(new(default, 0, high, low, close, bars.Count % 2 == 0 ? 5 : -5));
        }
        await Check(bars.ToArray());
    }

    [Fact]
    public async Task UnsupportedPricesAndDegenerateRangesKeepExactFallback()
    {
        await Check([
            new(default, 0, 3 * double.Epsilon, double.Epsilon, 2 * double.Epsilon, .1),
            new(default, 0, Math.ScaleB(1, -1022), Math.ScaleB(1, -1023), Math.ScaleB(1, -1022), -.1),
            new(default, 0, 3, 1, 2.75, .1),
            new(default, 0, 2, 1, Math.BitIncrement(2), -.1),
            new(default, 0, 2, 1, Math.BitDecrement(1), .1),
            new(default, 0, -1, -2, -1.25, -.1),
            new(default, 0, 1, -1, .25, .1),
            new(default, 0, 1, 2, 1.25, -.1),
            new(default, 0, 2, 2, 3, double.MaxValue),
            new(default, 0, double.MaxValue, double.MaxValue * .5, double.MaxValue, 0)
        ]);
    }

    private static async Task Check(Bar[] bars)
    {
        double[][] expected = Enumerable.Range(0, 5).Select(_ => new double[bars.Length]).ToArray();
        var total = new ReferenceFraction(0);
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            var high = ReferenceFraction.FromDouble(bar.High);
            var low = ReferenceFraction.FromDouble(bar.Low);
            var balance = ReferenceFraction.FromDouble(bar.Close) * new ReferenceFraction(2) - high - low;
            var range = high - low;
            expected[0][i] = bar.High == bar.Low ? 0 : (balance / range).ToDouble();
            expected[1][i] = bar.High == bar.Low ? 0 :
                (balance * ReferenceFraction.FromDouble(bar.Volume) / range).ToDouble();
            total += ReferenceFraction.FromDouble(expected[1][i]);
            expected[2][i] = total.ToDouble();
            if (i == 0) continue;
            expected[3][i] = ((ReferenceFraction.FromDouble(expected[2][i - 1])
                + ReferenceFraction.FromDouble(expected[2][i])) / new ReferenceFraction(2)).ToDouble();
            expected[4][i] = 1;
        }
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var indicator = new AccumulationDistributionWithAverage(2);
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            for (int slot = 0; slot < expected.Length; slot++)
                Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                    actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }
}
