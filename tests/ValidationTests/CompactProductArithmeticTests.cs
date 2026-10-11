using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactProductArithmeticTests
{
    [Fact]
    public void ProductsOfExactSumsPreserveWideFallbackAndOperandOwnership()
    {
        double[][] inputs = [[1, -1], [1, double.Epsilon], [double.MaxValue, double.MaxValue],
            [1.25, .125], [-double.Epsilon, -double.Epsilon], [Math.BitIncrement(1), -.25],
            [Math.ScaleB(1, 63), 1], [Math.ScaleB(1, 64), 1]];
        foreach (var left in inputs)
        foreach (var right in inputs)
        {
            var a = JurikDyadic.FromDouble(left[0]); a.Add(left[1]);
            var b = JurikDyadic.FromDouble(right[0]); b.Add(right[1]);
            var expectedA = ReferenceFraction.FromDouble(left[0]) + ReferenceFraction.FromDouble(left[1]);
            var expectedB = ReferenceFraction.FromDouble(right[0]) + ReferenceFraction.FromDouble(right[1]);
            var product = a;
            product.Multiply(b);
            Assert.Equal(BitConverter.DoubleToInt64Bits((expectedA * expectedB).ToDouble()),
                BitConverter.DoubleToInt64Bits(product.Mean(1)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedA.ToDouble()), BitConverter.DoubleToInt64Bits(a.Mean(1)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedB.ToDouble()), BitConverter.DoubleToInt64Bits(b.Mean(1)));
        }
    }
}
