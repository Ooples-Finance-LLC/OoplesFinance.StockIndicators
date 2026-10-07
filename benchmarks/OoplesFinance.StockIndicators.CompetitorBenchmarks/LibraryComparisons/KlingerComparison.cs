using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class KlingerComparison
{
    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(["Oscillator", "Signal"], rows);

    internal static ComparisonPair Pair(int fast = 34, int slow = 55, int signal = 13) =>
        new(
            "Skender.GetKvo",
            nameof(KlingerVolumeSnapshot),
            (d, _) =>
            {
                var r = d.Quotes.GetKvo(fast, slow, signal).ToArray();
                return Series([
                    r.Select(v => v.Oscillator).ToArray(),
                    r.Select(v => v.Signal).ToArray(),
                ]);
            },
            (d, _) => Owned(d.IndicatorBars, fast, slow, signal),
            (d, _) => Series(Reference(d.IndicatorBars, fast, slow, signal)),
            ["Oscillator", "Signal"],
            MinimumInputCount: 0,
            CompetitorReference: (d, _) =>
                Series(
                    NativeReference(WindowStochasticComparison.QuoteBars(d), fast, slow, signal)
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(Bar[] bars, int fast, int slow, int signal)
    {
        var r = KlingerVolumeSnapshot.Calculate(bars, fast, slow, signal);
        return Series([r.Select(v => v.Oscillator).ToArray(), r.Select(v => v.Signal).ToArray()]);
    }

    private static BigInteger?[] Average(BigInteger?[] values, int period)
    {
        var result = new BigInteger?[values.Length];
        var seed = new List<BigInteger>();
        BigInteger? prior = null;
        for (var i = 0; i < values.Length; i++)
        {
            if (!values[i].HasValue)
                continue;
            if (!prior.HasValue)
            {
                seed.Add(values[i]!.Value);
                if (seed.Count < period)
                    continue;
                prior = DirectionalComparison.RoundedUnits(
                    seed.Aggregate(BigInteger.Zero, (a, b) => a + b),
                    period
                );
            }
            else
                prior = DirectionalComparison.RoundedUnits(
                    ((long)period + 1) * prior.Value + 2 * (values[i]!.Value - prior.Value),
                    (long)period + 1
                );
            result[i] = prior;
        }
        return result;
    }

    internal static double?[][] Reference(Bar[] bars, int fast, int slow, int signal)
    {
        var ranges = bars.Select(b => Units(b.High) - Units(b.Low)).ToArray();
        var basis = bars.Select(b => Units(b.High) + Units(b.Low) + Units(b.Close)).ToArray();
        var direction = Enumerable
            .Range(0, bars.Length)
            .Select(i => i > 0 && basis[i] > basis[i - 1] ? 1 : -1)
            .ToArray();
        var force = new BigInteger?[bars.Length];
        var cumulative = BigInteger.Zero;
        for (var i = 2; i < bars.Length; i++)
        {
            cumulative =
                direction[i] == direction[i - 1]
                    ? cumulative + ranges[i]
                    : ranges[i - 1] + ranges[i];
            var v = Units(bars[i].Volume);
            force[i] =
                ranges[i] == cumulative || v.IsZero ? BigInteger.Zero
                : ranges[i].IsZero ? DirectionalComparison.RoundedUnits(200 * direction[i] * v, 1)
                : cumulative.IsZero ? force[i - 1] ?? BigInteger.Zero
                : DirectionalComparison.RoundedUnits(
                    v * direction[i] * BigInteger.Abs(2 * ranges[i] - 2 * cumulative) * 100,
                    BigInteger.Abs(cumulative)
                );
        }
        var f = Average(force, fast);
        var s = Average(force, slow);
        var oscillator = new BigInteger?[bars.Length];
        for (var i = 0; i < bars.Length; i++)
            if (f[i].HasValue && s[i].HasValue)
                oscillator[i] = DirectionalComparison.RoundedUnits(f[i]!.Value - s[i]!.Value, 1);
        var a = Average(oscillator, signal);
        return
        [
            oscillator.Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null).ToArray(),
            a.Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null).ToArray(),
        ];
    }

    internal static double?[][] NativeReference(Bar[] bars, int fast, int slow, int signal)
    {
        var force = new double?[bars.Length];
        var ranges = bars.Select(b => Subtract(b.High, b.Low)).ToArray();
        var basis = bars.Select(b => Add(Add(b.High, b.Low), b.Close)).ToArray();
        var cumulative = 0d;
        for (var i = 2; i < bars.Length; i++)
        {
            var up = basis[i] > basis[i - 1];
            var priorUp = basis[i - 1] > basis[i - 2];
            var direction = up ? 1 : -1;
            cumulative = up == priorUp ? Add(cumulative, ranges[i]) : Add(ranges[i - 1], ranges[i]);
            var v = bars[i].Volume;
            force[i] =
                ranges[i] == cumulative || v == 0 ? 0 // NOSONAR: S1244 - The native cancellation branch requires exact equality.
                : ranges[i] == 0 ? Multiply(Multiply(Multiply(v, 2), direction), 100)
                : cumulative == 0 ? force[i - 1] ?? 0
                : Multiply(
                    Multiply(
                        Multiply(
                            v,
                            Math.Abs(Multiply(2, Subtract(Divide(ranges[i], cumulative), 1)))
                        ),
                        direction
                    ),
                    100
                );
        }
        double?[] Ema(double?[] input, int period)
        {
            var result = new double?[input.Length];
            var values = new List<double>();
            double? last = null;
            var alpha = Divide(2, period + 1d);
            for (var i = 0; i < input.Length; i++)
            {
                if (!input[i].HasValue)
                    continue;
                if (!last.HasValue)
                {
                    values.Add(input[i]!.Value);
                    if (values.Count < period)
                        continue;
                    last = Divide(values.Aggregate(0d, Add), period);
                }
                else
                    last = Add(
                        Multiply(input[i]!.Value, alpha),
                        Multiply(last.Value, Subtract(1, alpha))
                    );
                result[i] = last;
            }
            return result;
        }
        var f = Ema(force, fast);
        var s = Ema(force, slow);
        var oscillator = new double?[bars.Length];
        for (var i = 0; i < bars.Length; i++)
            if (f[i].HasValue && s[i].HasValue)
                oscillator[i] = Subtract(f[i]!.Value, s[i]!.Value);
        return [oscillator, Ema(oscillator, signal)];
    }
}
