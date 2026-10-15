using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonPolynomialExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    // Frozen four-coefficient publication used before finite-difference Horner evaluation.
    private sealed class PreviousPolynomial
    {
        private readonly BigInteger[] _coefficients;
        private readonly ExactMeanAccumulator _denominator;
        internal PreviousPolynomial(double factor)
        {
            var parts = ExactMeanAccumulator.DecomposeFinite(factor);
            BigInteger n = parts.Integer, q = BigInteger.One;
            if (parts.Exponent >= 0) n <<= parts.Exponent;
            else q <<= -parts.Exponent;
            var combined = q + n;
            _coefficients = [combined * combined * combined, -3 * n * combined * combined,
                3 * n * n * combined, -n * n * n];
            var denominator = new ExactMeanAccumulator();
            denominator.Add(1, q * q * q);
            _denominator = denominator;
        }
        internal double Combine(double a, double b, double c, double d)
        {
            var total = new ExactMeanAccumulator();
            total.Add(a, _coefficients[0]); total.Add(b, _coefficients[1]);
            total.Add(c, _coefficients[2]); total.Add(d, _coefficients[3]);
            return total.Ratio(_denominator);
        }
    }

    [Fact]
    public void ExactDifferencesPreserveCancellationUnderflowAndWideFactorRounding()
    {
        var random = new Random(82219);
        double[] special = [-0d, 0, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, -1];
        foreach (double factor in new[] { -1d, 0, .7, -.5, 1, 2, double.Epsilon, double.MaxValue, -double.MaxValue })
        {
            var previous = new PreviousPolynomial(factor);
            void Check(double a, double b, double c, double d) =>
                Assert.Equal(BitConverter.DoubleToInt64Bits(previous.Combine(a, b, c, d)),
                    BitConverter.DoubleToInt64Bits(ValuesBarExecution.TillsonValueState.Combine(a, b, c, d, factor)));
            foreach (double value in special)
            {
                Check(value, value, value, value);
                Check(value, -value, value, -value);
                Check(value, 0, 0, 0);
            }
            for (int i = 0; i < 100; i++)
            {
                double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024));
                Check(Next(), Next(), Next(), Next());
            }
        }
    }

    [Fact]
    public void OrdinaryPolynomialAllocatesLessThanFourWideCoefficientProducts()
    {
        var previous = new PreviousPolynomial(.7);
        var rows = Enumerable.Range(0, 256).Select(i =>
            (100 + i / 7d, 100 + i / 11d, 100 + i / 13d, 100 + i / 17d)).ToArray();
        long Measure(bool old)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            double sum = 0;
            foreach (var (a, b, c, d) in rows)
                sum += old ? previous.Combine(a, b, c, d) : ValuesBarExecution.TillsonValueState.Combine(a, b, c, d, .7);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(double.IsFinite(sum));
            return bytes;
        }
        Measure(true); Measure(false);
        long oldBytes = Measure(true), newBytes = Measure(false);
        log.WriteLine($"256 polynomial publications: previous {oldBytes} B; finite differences {newBytes} B.");
        Assert.True(newBytes < oldBytes, $"Previous {oldBytes}, finite differences {newBytes}");
    }
}
