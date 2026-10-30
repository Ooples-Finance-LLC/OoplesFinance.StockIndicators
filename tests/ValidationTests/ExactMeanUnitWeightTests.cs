using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExactMeanUnitWeightTests
{
    [Fact]
    public void UnitWeightsPreserveWideTransitionsCancellationAndSignedUnderflow()
    {
        foreach (double value in new[] { double.Epsilon, Math.ScaleB(1, -1022),
            Math.BitIncrement(1), Math.ScaleB(1, 63), double.MaxValue })
        foreach (int sign in new[] { -1, 1 })
        {
            // Tiny residuals survive a wide excursion and return to zero.
            (double Value, int Weight)[] terms =
            [(-double.Epsilon, 1), (value, sign), (value, sign),
                (value, -sign), (value, -sign), (-double.Epsilon, -1),
                (-0d, 1), (0d, -1), (value, 0),
                (value, int.MinValue), (value, int.MaxValue), (value, 1)];
            Check(terms);
        }
    }

    [Fact]
    public void MixedWeightsAcrossEveryFiniteExponentMatchIndependentRationalSums()
    {
        var random = new Random(462937);
        var terms = new List<(double Value, int Weight)>();
        for (int exponent = 0; exponent < 2047; exponent++)
        {
            long fraction = random.NextInt64() & 0xfffffffffffffL;
            long bits = ((long)exponent << 52) | fraction;
            if ((exponent & 1) != 0) bits |= long.MinValue;
            double value = BitConverter.Int64BitsToDouble(bits);
            int weight = exponent % 4 == 0 ? -1 : 1;
            terms.Add((value, weight));
            terms.Add((value, -weight));
            if (exponent % 127 == 0)
            {
                terms.Add((value, 3));
                terms.Add((value, -3));
            }
        }
        Check(terms);
    }

    [Fact]
    public void ShiftedUnitFactorsPreserveExactSumsAndWideCancellation()
    {
        foreach (int shift in new[] { -1100, -1, 0, 1, 1100 })
        {
            var sum = new ExactMeanAccumulator();
            var exact = new ReferenceFraction(0);
            var power = new ReferenceFraction(BigInteger.One << Math.Abs(shift));
            var scale = shift < 0 ? new ReferenceFraction(1) / power : power;
            foreach (double value in new[] { double.Epsilon, -Math.BitIncrement(1), double.MaxValue })
            foreach (int weight in new[] { 1, -1, -1, 1 })
            {
                sum.AddFactor(new ExactMeanAccumulator.ProductFactor(value, shift), weight);
                exact += ReferenceFraction.FromDouble(value) * scale * new ReferenceFraction(weight);
                foreach (long divisor in new[] { 1L, 2, 3, int.MaxValue })
                    Assert.Equal(BitConverter.DoubleToInt64Bits((exact / new ReferenceFraction(divisor)).ToDouble()),
                        BitConverter.DoubleToInt64Bits(sum.Mean(divisor)));
            }
        }
    }

    [Fact]
    public void NonfiniteInputsRemainRejectedEvenWithZeroWeight()
    {
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (int weight in new[] { 0, 1, -1, 2, int.MinValue, int.MaxValue })
        {
            var sum = new ExactMeanAccumulator();
            Assert.Throws<ArithmeticException>(() => sum.Add(value, weight));
        }
    }

    private static void Check(IEnumerable<(double Value, int Weight)> terms)
    {
        var sum = new ExactMeanAccumulator();
        var factors = new ExactMeanAccumulator();
        var exact = new ReferenceFraction(0);
        foreach (var (value, weight) in terms)
        {
            sum.Add(value, weight);
            factors.AddFactor(new ExactMeanAccumulator.ProductFactor(value), weight);
            exact += ReferenceFraction.FromDouble(value) * new ReferenceFraction(weight);
            Assert.Equal(exact.Sign, sum.Sign);
            Assert.Equal(exact.Sign == 0, sum.IsExactlyZero);
            foreach (long divisor in new[] { 1L, 2, 3, int.MaxValue })
            {
                double expected = (exact / new ReferenceFraction(divisor)).ToDouble();
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
                    BitConverter.DoubleToInt64Bits(sum.Mean(divisor)));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
                    BitConverter.DoubleToInt64Bits(factors.Mean(divisor)));
            }
        }
    }
}
