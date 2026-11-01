using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonCertifiedCoefficientsTests
{
    public static IEnumerable<object[]> Factors => new[]
    {
        .25, .7, -1, 2, Math.BitIncrement(2), Math.BitDecrement(.25), double.Epsilon, double.MaxValue
    }.Select(factor => new object[] { factor });

    [Theory, MemberData(nameof(Factors))]
    public void CertifiedAndFallbackCombinationsMatchExactPolynomial(double factor)
    {
        var coefficients = new TillsonWindow.Coefficients(factor);
        var random = new Random(301030);
        for (int i = 0; i < 96; i++)
        {
            long exponent = (long)random.Next(1, 2047) << 52;
            long fraction = random.NextInt64(1L << 51);
            var values = Enumerable.Range(0, 4).Select(j =>
            {
                long bits = exponent | (fraction + random.NextInt64(1L << 49));
                if (i % 3 == 0 && j % 2 == 0) bits |= long.MinValue;
                return BitConverter.Int64BitsToDouble(bits);
            }).ToArray();
            AssertMatches(values);
        }
        foreach (double[] values in new double[][]
        {
            [1, 1, 1, 1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1],
            [0, -0d, 0, -0d],
            [double.Epsilon, -double.Epsilon, 0, double.Epsilon],
            [double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue],
            [-double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue],
            [double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue],
            [Math.ScaleB(1, -1022), double.Epsilon, -double.Epsilon, -Math.ScaleB(1, -1022)]
        }) AssertMatches(values);

        void AssertMatches(double[] values)
        {
            var weight = ReferenceFraction.FromDouble(factor);
            var combined = new ReferenceFraction(1) + weight;
            var third = ReferenceFraction.FromDouble(values[0]);
            var fourth = ReferenceFraction.FromDouble(values[1]);
            var fifth = ReferenceFraction.FromDouble(values[2]);
            var sixth = ReferenceFraction.FromDouble(values[3]);
            double expected = (third * combined * combined * combined
                - new ReferenceFraction(3) * fourth * weight * combined * combined
                + new ReferenceFraction(3) * fifth * weight * weight * combined
                - sixth * weight * weight * weight).ToDouble();
            double actual = coefficients.Combine(values[0], values[1], values[2], values[3]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
    }
}
