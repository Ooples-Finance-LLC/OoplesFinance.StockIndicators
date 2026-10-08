using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class FixedKernelSnapshotComparison
{
    internal static ComparisonPair Pair(
        bool fibonacci,
        int taps = 21,
        SincWindow window = SincWindow.Hann
    ) =>
        new(
            fibonacci ? "QuanTAlib.Fwma" : "QuanTAlib.Afirma",
            fibonacci ? nameof(FibonacciWeightedSnapshot) : nameof(WindowedSincSnapshot),
            (d, p) =>
            {
                QuanTAlib.AbstractBase indicator = fibonacci
                    ? new QuanTAlib.Fwma(p)
                    : new QuanTAlib.Afirma(p, taps, (QuanTAlib.Afirma.WindowType)window);
                return new(
                    0,
                    d.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                        .ToArray()
                );
            },
            (d, p) =>
                new(
                    0,
                    (
                        fibonacci
                            ? FibonacciWeightedSnapshot.Calculate(d.IndicatorBars, p)
                            : WindowedSincSnapshot.Calculate(d.IndicatorBars, p, taps, window)
                    ).ToArray()
                ),
            (d, p) => new(0, Reference(d.Closes, p, fibonacci, taps, window, false)),
            MinimumInputCount: 0,
            CompetitorReference: (d, p) =>
                new(0, Reference(d.Closes, p, fibonacci, taps, window, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static (BigInteger A, BigInteger B) Fib(int n)
    {
        if (n == 0)
            return (0, 1);
        var (a, b) = Fib(n / 2);
        var c = a * (2 * b - a);
        var d = a * a + b * b;
        return (n & 1) == 0 ? (c, d) : (d, c + d);
    }

    internal static double[] SincWeights(int period, int taps, SincWindow window)
    {
        double[] factors = window switch
        {
            SincWindow.Rectangular => [1],
            SincWindow.Hann => [.5, -.5],
            SincWindow.Hamming => [.54, -.46],
            SincWindow.Blackman => [.42, -.5, .08],
            _ => [.35875, -.48829, .14128, -.01168],
        };
        var result = new double[taps];
        for (var k = 0; k < taps; k++)
        {
            var value = factors[0];
            for (var h = 1; h < factors.Length; h++)
                value = Add(
                    value,
                    Multiply(
                        factors[h],
                        Math.Cos(Divide(Multiply(Multiply(2 * h, Math.PI), k), taps - 1))
                    )
                );
            var position = Subtract(k, Divide(taps - 1, 2));
            var x = Divide(Multiply(Math.PI, position), period);
            result[k] = Multiply(value, position == 0 ? 1 : Divide(Math.Sin(x), x));
        }
        return result;
    }

    internal static double[] Reference(
        double[] prices,
        int period,
        bool fibonacci,
        int taps,
        SincWindow window,
        bool native
    )
    {
        var result = new double[prices.Length];
        if (fibonacci && !native)
        {
            var weights = Enumerable
                .Range(0, Math.Min(period, prices.Length))
                .Select(lag => Fib(period - lag).A)
                .ToArray();
            for (var i = 0; i < prices.Length; i++)
            {
                var mass = BigInteger.Zero;
                var total = BigInteger.Zero;
                for (var j = 0; j < Math.Min(period, i + 1); j++)
                {
                    mass += weights[j];
                    total += weights[j] * Units(prices[i - j]);
                }
                result[i] = Round(total, Grid * mass);
            }
            return result;
        }
        double[] kernel;
        if (fibonacci)
        {
            kernel = new double[period];
            kernel[0] = kernel[1] = 1;
            for (var j = 2; j < period; j++)
                kernel[j] = Add(kernel[j - 1], kernel[j - 2]);
            Array.Reverse(kernel);
            var mass = kernel.Aggregate(0d, Add);
            kernel = kernel.Select(v => Divide(v, mass)).ToArray();
        }
        else
            kernel = SincWeights(period, taps, window);
        var nativeMass = native ? kernel.Aggregate(0d, Add) : 0;
        for (var i = 0; i < prices.Length; i++)
        {
            if (native)
            {
                var count = fibonacci ? Math.Min(period, i + 1) : taps;
                var divisor = fibonacci ? kernel.Take(count).Aggregate(0d, Add) : nativeMass;
                var sum = 0d;
                for (var j = 0; j < count; j++)
                {
                    var price = prices[
                        fibonacci ? i - j : Math.Min(i, Math.Max(0, i - taps + 1) + j)
                    ];
                    sum = Add(
                        sum,
                        fibonacci
                            ? Multiply(price, Divide(kernel[j], divisor))
                            : Divide(Multiply(price, kernel[j]), divisor)
                    );
                }
                result[i] = sum;
            }
            else
            {
                var mass = kernel.Aggregate(BigInteger.Zero, (n, v) => n + Units(v));
                var total = BigInteger.Zero;
                for (var j = 0; j < taps; j++)
                    total +=
                        Units(kernel[j])
                        * Units(prices[Math.Min(i, Math.Max(0, i - taps + 1) + j)]);
                result[i] = Round(total, Grid * mass);
            }
        }
        return result;
    }
}
