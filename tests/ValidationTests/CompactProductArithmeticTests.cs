using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactProductArithmeticTests
{
    [Fact]
    public void RoundedProductSumAllowsAliasedReadonlyOperandsWithoutMutation()
    {
        BigInteger grid = BigInteger.One << 1074;
        BigInteger[] inputs = [0, 1, -1, ExactVarianceWindow.Units(1.3),
            (BigInteger.One << 3000) + 1, -(BigInteger.One << 3000) - 1];
        foreach (BigInteger units in inputs)
        foreach (double factor in new[] { -1d, .2, 2, double.Epsilon })
        {
            var original = JurikDyadic.FromUnits(units);
            var result = JurikDyadic.SumProduct(in original, in original, factor);
            BigInteger expected = RocBankValue.RoundUnits(units * (grid + ExactVarianceWindow.Units(factor)), grid);
            Assert.Equal(expected, result.RoundedUnits());
            // Compare unrounded values so a mutated low bit cannot hide behind
            // binary64 publication or extended-value rounding.
            var difference = original;
            difference.Subtract(JurikDyadic.FromUnits(units));
            Assert.Equal(0, difference.Sign);
        }
        var simple = JurikDyadic.FromDouble(1.3);
        var simpleResult = JurikDyadic.SumProduct(in simple, in simple, .2);
        var reference = ReferenceFraction.FromDouble(1.3)
            * (ReferenceFraction.FromDouble(1) + ReferenceFraction.FromDouble(.2));
        Assert.Equal(BitConverter.DoubleToInt64Bits(reference.ToDouble()), BitConverter.DoubleToInt64Bits(simpleResult.Mean(1)));
        Assert.Equal(1.3, simple.Mean(1));
    }

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
