using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriceRelativeMeanTests
{
    private static void Check(double[] evaluation, double[] basis, int period)
    {
        var state = new PriceRelativeStrength.State(null, period, CandlePriceField.Close, CandlePriceField.Open);
        Span<double> output = stackalloc double[6];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < evaluation.Length; i++)
            {
                var bar = new Bar(default, basis[i], 1, 0, evaluation[i], 0);
                state.Update(in bar, output);
                bool present = i >= period - 1;
                BigInteger sum = 0;
                for (int j = Math.Max(0, i - period + 1); j <= i; j++)
                {
                    if (basis[j] == 0) { present = false; continue; }
                    double ratio = evaluation[j] == 0 ? 0 : evaluation[j] / basis[j];
                    sum += ExactVarianceWindow.Units(ratio);
                }
                Assert.Equal(present ? 1 : 0, output[4]);
                double expected = present ? ExactMeanAccumulator.UnitRatio(sum, period) : 0;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(output[1]));
            }
        }
    }

    [Fact]
    public void MissingRatioEvictionAndResetPreserveCompleteWindowMasks() =>
        Check([2, 4, 6, 8, 10, 12, 14, 16], [2, 0, 3, 4, 0, 6, 7, 8], 3);

    [Fact]
    public void WideCancellationAndSubnormalWindowsRoundOnlyTheExactMean()
    {
        double[] values = [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue,
            double.Epsilon, -double.Epsilon, 1e-300, -1e-300, 1, -1, 0];
        Check(values, Enumerable.Repeat(1d, values.Length).ToArray(), 3);
    }

    [Fact]
    public void MeanRetainsLowBitsAcrossRoundingMidpointsAndEviction()
    {
        double[] values = [1, Math.ScaleB(1, -53), -1, Math.ScaleB(1, -54), 1, -1,
            Math.BitIncrement(1d), Math.BitDecrement(1d), 0];
        Check(values, Enumerable.Repeat(1d, values.Length).ToArray(), 2);
    }
}
