using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DetrendedDirectDifferenceTests
{
    [Fact]
    public void PeriodOneDifferencesPreserveExactRoundingAndCanonicalZero()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022),
            1, -1, Math.BitIncrement(1), Math.BitDecrement(1),
            double.MaxValue, -double.MaxValue,
            Math.BitIncrement(Math.ScaleB(1, -54)), -Math.BitIncrement(Math.ScaleB(1, -54))];
        foreach (double a in values)
        foreach (double b in values)
        {
            var exact = new ExactMeanAccumulator(); exact.Add(a); exact.Add(b, -1);
            double expected = exact.Mean(1);
            Bar[] bars = [new(default, a, a, a, a, 1), new(default, b, b, b, b, 1)];
            if (double.IsInfinity(expected))
            {
                Assert.Throws<OverflowException>(() => DetrendedPriceSnapshot.Calculate(bars, 1));
                continue;
            }
            var result = DetrendedPriceSnapshot.Calculate(bars, 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(result[0].Dpo!.Value));
            Assert.Null(result[1].Dpo);
            Assert.Null(result[1].Sma);
        }
    }
}
