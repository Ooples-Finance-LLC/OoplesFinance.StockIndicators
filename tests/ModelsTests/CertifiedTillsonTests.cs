using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CertifiedTillsonTests
{
    [Fact]
    public void AcceptedStageRatiosMatchExactArithmetic()
    {
        var random = new Random(77391);
        int accepted = 0;
        foreach (int period in new[] { 1, 2, 3, 20, int.MaxValue })
        foreach (int exponent in new[] { -1022, -500, 0, 500, 1010 })
        for (int i = 0; i < 200; i++)
        {
            double a = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double b = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            if (!CertifiedTillson.TrySmooth(a, b, period, out double actual)) continue;
            var exact = new ExactMeanAccumulator();
            exact.Add(a, period - 1L); exact.Add(b, 2);
            Equal(exact.Mean((long)period + 1), actual);
            accepted++;
        }
        Assert.True(accepted > 3000, $"Only {accepted} certified stage updates");
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(1023)] [InlineData(1024)] [InlineData(2047)] [InlineData(2048)]
    [InlineData(int.MaxValue)]
    public void StageRoundingAcrossMachineWordBoundariesMatchesExactArithmetic(int period)
    {
        (double Prior, double Input)[] pairs =
        [
            (1, Math.BitIncrement(1)), (Math.BitDecrement(1), 1),
            (-1, 1), (1, -1), (1, Math.ScaleB(1, -12)),
            (Math.ScaleB(1, -12), 1), (-1, -Math.BitIncrement(1))
        ];
        foreach (int exponent in new[] { -1000, -1, 0, 900 })
        foreach (var pair in pairs)
        {
            double prior = Math.ScaleB(pair.Prior, exponent);
            double input = Math.ScaleB(pair.Input, exponent);
            Assert.True(CertifiedTillson.TrySmooth(prior, input, period, out double actual));
            var exact = new ExactMeanAccumulator();
            exact.Add(prior, period - 1L); exact.Add(input, 2);
            Equal(exact.Mean((long)period + 1), actual);
        }
    }

    [Theory]
    [InlineData(.25)] [InlineData(.7)] [InlineData(2)]
    [InlineData(-.25)] [InlineData(-.7)] [InlineData(-2)]
    public void PolynomialBoundsMatchIndependentExactCoefficients(double factor)
    {
        var random = new Random(9931);
        int accepted = 0;
        foreach (int exponent in new[] { -1022, -1000, -500, 0, 500, 1000 })
        for (int i = 0; i < 200; i++)
        {
            double a = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double b = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double c = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double d = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double expected = ExactPolynomial(a, b, c, d, factor);
            Equal(expected, ValuesBarExecution.TillsonValueState.Combine(a, b, c, d, factor));
            if (!CertifiedTillson.TryCombine(a, b, c, d, factor, out double actual)) continue;
            Equal(expected, actual); accepted++;
        }
        Assert.True(accepted > 800, $"Only {accepted} certified polynomial results");
    }

    [Fact]
    public void CancellationZerosAndUnsupportedRangesKeepExactResults()
    {
        foreach (double value in new[] { 0d, -0d, double.Epsilon, double.MaxValue, -double.MaxValue, 1d })
        foreach (double factor in new[] { 0d, .7, -1d, 2d, double.Epsilon, double.MaxValue })
        {
            double expected = ExactPolynomial(value, value, value, value, factor);
            Equal(expected, ValuesBarExecution.TillsonValueState.Combine(value, value, value, value, factor));
            if (CertifiedTillson.TryCombine(value, value, value, value, factor, out double actual)) Equal(expected, actual);
        }
        Assert.False(CertifiedTillson.TrySmooth(1, double.Epsilon, 20, out _));
        Assert.False(CertifiedTillson.TryCombine(1, -1, 1, -1, double.MaxValue, out _));
        Equal(ExactPolynomial(1, -1, 1, -1, .7), ValuesBarExecution.TillsonValueState.Combine(1, -1, 1, -1, .7));
    }

    // Independent coefficient expansion: (1+a)^3 E3 -3a(1+a)^2 E4
    // +3a^2(1+a) E5 -a^3 E6, evaluated without rounded coefficients.
    private static double ExactPolynomial(double a, double b, double c, double d, double factor)
    {
        var sum = new ExactMeanAccumulator();
        double[] values = [a, b, c, d];
        int[][] weights = [[1, 3, 3, 1], [0, -3, -6, -3], [0, 0, 3, 3], [0, 0, 0, -1]];
        for (int stage = 0; stage < 4; stage++)
        for (int power = 0; power < 4; power++)
        {
            var term = new ExactMeanAccumulator();
            term.Add(values[stage], weights[stage][power]);
            for (int j = 0; j < power; j++) term.Multiply(factor);
            sum.AddExact(term);
        }
        return sum.Mean(1);
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
