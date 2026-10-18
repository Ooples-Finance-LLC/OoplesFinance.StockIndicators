using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactDeviationBandsTests
{
    [Theory]
    [InlineData(.1)] [InlineData(-2d)] [InlineData(0d)] [InlineData(3d)]
    [InlineData(double.MaxValue)] [InlineData(double.Epsilon)]
    public void CompactAndExtendedPathsMatchIndependentRationalOutputs(double factor)
    {
        double[][] fixtures = [
            [double.MaxValue / 2, double.MaxValue, double.MaxValue / 2, 1, 1, 1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), -1, -0d, 0, 3, 2, 1],
            [double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 1, -1, 3 * double.Epsilon, 1, 1],
            [double.Epsilon, 3 * double.Epsilon, 0, -double.Epsilon, Math.ScaleB(1, -1022), 1, Math.ScaleB(1, 900), 1, -1]
        ];
        foreach (int period in new[] { 1, 2, 3 })
        foreach (bool percent in new[] { false, true })
        foreach (var values in fixtures)
        {
            var bars = values.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
            var indicator = new WindowDeviationBands(period, factor, percent);
            var expected = WindowDeviationBandsReference.Values(bars, indicator);
            // The reference converts its rounded center back to a rational, which
            // erases negative zero. The published middle preserves the first
            // rounding, so derive that slot directly from the exact mean.
            for (int i = period - 1; i < bars.Length; i++)
                expected[0][i] = (values.Skip(i - period + 1).Take(period)
                    .Select(ReferenceFraction.FromDouble).Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                    / new ReferenceFraction(period)).ToDouble();
            var state = new WindowDeviationBands.State(indicator);
            var actual = new double[12];
            for (int pass = 0; pass < 2; pass++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], actual);
                    for (int slot = 0; slot < 6; slot++)
                    {
                        Assert.True(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0) == BitConverter.DoubleToInt64Bits(actual[slot]), $"period={period}, percent={percent}, bar={i}, slot={slot}, price={values[i]:R}, expected={expected[slot][i]:R}, actual={actual[slot]:R}");
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0, actual[slot + 6]);
                    }
                }
            }
        }
    }
}
