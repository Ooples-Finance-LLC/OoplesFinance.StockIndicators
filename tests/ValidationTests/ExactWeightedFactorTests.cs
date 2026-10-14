using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactWeightedFactorTests
{
    [Fact]
    public void ReusedPricesPreserveIndependentWeightedRounding()
    {
        double[] prices = [-0d, 0, double.Epsilon, -double.Epsilon, .1, -.1,
            Math.BitIncrement(1), -Math.BitIncrement(1), double.MaxValue, -double.MaxValue];
        foreach (double price in prices)
        foreach (double other in prices)
        foreach (int weight in new[] { int.MinValue, -2000, -11, -1, 0, 1, 11, 4000, int.MaxValue })
        foreach (int divisor in new[] { 1, 3, 4, 120, 3000 })
        {
            var factor = new ExactMeanAccumulator.ProductFactor(price);
            var sum = new ExactMeanAccumulator();
            sum.AddFactor(factor, weight);
            sum.AddFactor(new ExactMeanAccumulator.ProductFactor(other), 3);
            // Independent rational arithmetic covers cancellation, wide products,
            // signed underflow, halfway rounding and representational overflow.
            var expected = ((ReferenceFraction.FromDouble(price) * new ReferenceFraction(weight)
                + ReferenceFraction.FromDouble(other) * new ReferenceFraction(3))
                / new ReferenceFraction(divisor)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(sum.Mean(divisor)));
        }
    }
}
