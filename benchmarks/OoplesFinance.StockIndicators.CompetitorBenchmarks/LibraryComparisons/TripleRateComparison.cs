using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TripleRateComparison
{
    internal static readonly string[] Names = ["Trix", "Ema3", "Signal"];
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(
        bool ta,
        int? signal = null,
        bool firstPrice = false,
        int suppression = 0
    ) =>
        new(
            ta ? "TaLib.Functions.Trix" : "Skender.GetTrix",
            nameof(TripleExponentialRate),
            (d, p) => Native(d, p, ta, signal, firstPrice, suppression),
            (d, p) =>
                Owned(
                    d.IndicatorBars,
                    p,
                    ta
                        ? firstPrice
                            ? TripleRateSeed.CascadedFirstPrice
                            : TripleRateSeed.CascadedMeans
                        : TripleRateSeed.SharedMean,
                    signal,
                    suppression,
                    ta
                ),
            (d, p) =>
                Series(
                    Reference(
                        d.Closes,
                        p,
                        ta
                            ? firstPrice
                                ? TripleRateSeed.CascadedFirstPrice
                                : TripleRateSeed.CascadedMeans
                            : TripleRateSeed.SharedMean,
                        signal,
                        suppression
                    ),
                    ta
                ),
            ta ? ["Value"] : Names,
            MinimumInputCount: ta ? 2 : 1,
            CompetitorReference: (d, p) =>
                ta
                    ? VolumePriceComparison.Mask(NativeTa(d.Closes, p, firstPrice, suppression))
                    : Series(
                        NativeSkender(d.Quotes.Select(q => (double)q.Close).ToArray(), p, signal),
                        false
                    ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows, bool ta) =>
        ta ? VolumePriceComparison.Mask(rows[0]) : RetrospectivePriceComparison.Series(Names, rows);

    internal sealed class Settings : IDisposable
    {
        private readonly TaCore.CompatibilityMode _old = TaCore.CompatibilitySettings.Get();
        private readonly int _unstable = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Ema);

        internal Settings(bool firstPrice, int suppression)
        {
            TaCore.CompatibilitySettings.Set(
                firstPrice ? TaCore.CompatibilityMode.Metastock : TaCore.CompatibilityMode.Default
            );
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Ema, suppression);
        }

        public void Dispose()
        {
            TaCore.CompatibilitySettings.Set(_old);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Ema, _unstable);
        }
    }

    private static ComparisonSeries Native(
        CompetitorData d,
        int p,
        bool ta,
        int? signal,
        bool firstPrice,
        int suppression
    )
    {
        if (!ta)
        {
            var r = d.Quotes.GetTrix(p, signal).ToArray();
            return Series(
                [
                    r.Select(v => v.Trix).ToArray(),
                    r.Select(v => v.Ema3).ToArray(),
                    r.Select(v => v.Signal).ToArray(),
                ],
                false
            );
        }
        if (
            TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Ema) != suppression
            || (TaCore.CompatibilitySettings.Get() == TaCore.CompatibilityMode.Metastock)
                != firstPrice
        )
            throw new InvalidOperationException("TRIX EMA settings mismatch");
        var rix = new double?[d.Count];
        if (d.Count == 0)
            return VolumePriceComparison.Mask(rix);
        var packed = new double[d.Count];
        var code = Functions.Trix<double>(d.Closes, System.Range.All, packed, out var range, p);
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("Native TRIX returned " + code);
        var first = 3L * (p - 1L + suppression) + 1;
        var count = (int)Math.Max(0, d.Count - first);
        var start = count > 0 ? (int)first : 0;
        if (!range.Equals(new System.Range(start, start + count)))
            throw new InvalidOperationException("Unexpected TRIX range");
        for (var i = 0; i < count; i++)
            rix[start + i] = packed[i];
        return VolumePriceComparison.Mask(rix);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        TripleRateSeed mode,
        int? signal = null,
        int suppression = 0,
        bool rateOnly = false
    )
    {
        var indicator = new TripleExponentialRate(p, mode, signal, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var r = new double?[3][];
        for (var j = 0; j < 3; j++)
        {
            var values = run[indicator.Outputs[j]].ToArray();
            var flags = run[indicator.Outputs[j + 3]].ToArray();
            r[j] = values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
        }
        return Series(r, rateOnly);
    }

    internal static double?[][] Reference(
        double[] input,
        int p,
        TripleRateSeed mode,
        int? signal = null,
        int suppression = 0
    )
    {
        var n = input.Length;
        var stages = Enumerable.Range(0, 3).Select(_ => new BigInteger[n]).ToArray();
        var shared = mode == TripleRateSeed.SharedMean;
        var lb = (long)p - 1 + suppression;
        for (var j = 0; j < 3; j++)
        {
            var start = shared ? 0 : j * lb;
            var initial = start + (mode == TripleRateSeed.CascadedFirstPrice ? 0 : p - 1L);
            if (initial >= n)
                continue;
            var source = j == 0 ? input.Select(Units).ToArray() : stages[j - 1];
            var seed =
                mode == TripleRateSeed.CascadedFirstPrice
                    ? source[(int)start]
                    : DirectionalComparison.RoundedUnits(
                        (shared ? input.Select(Units).ToArray() : source)
                            .Skip((int)start)
                            .Take(p)
                            .Aggregate(BigInteger.Zero, (a, b) => a + b),
                        p
                    );
            stages[j][(int)initial] = seed;
            for (var i = (int)initial + 1; i < n; i++)
                stages[j][i] = DirectionalComparison.RoundedUnits(
                    stages[j][i - 1] * (p - 1) + 2 * source[i],
                    (long)p + 1
                );
        }
        var result = Enumerable.Range(0, 3).Select(_ => new double?[n]).ToArray();
        var first = shared ? p : 3 * lb + 1;
        for (var i = 0; i < n; i++)
        {
            if (i >= (shared ? p : 3 * lb))
                result[1][i] = Round(stages[2][i], Grid);
            if (i < first)
                continue;
            var prior = stages[2][i - 1];
            var now = stages[2][i];
            result[0][i] = prior.IsZero
                ? shared
                    ? now.IsZero
                        ? null
                        : now.Sign > 0
                            ? double.PositiveInfinity
                            : double.NegativeInfinity
                    : 0
                : Round(100 * (now - prior), prior);
            if (!signal.HasValue || i < first + signal.Value - 1)
                continue;
            var window = result[0].Skip(i - signal.Value + 1).Take(signal.Value).ToArray();
            if (window.Any(v => !v.HasValue))
                continue;
            result[2][i] = window.Any(v => !double.IsFinite(v!.Value))
                ? window.Sum(v => v!.Value) / signal.Value
                : Round(
                    window.Aggregate(BigInteger.Zero, (s, v) => s + Units(v!.Value)),
                    Grid * signal.Value
                );
        }
        return result;
    }

    internal static double?[][] NativeSkender(double[] prices, int p, int? signal)
    {
        var r = Enumerable.Range(0, 3).Select(_ => new double?[prices.Length]).ToArray();
        var k = Divide(2, checked(p + 1));
        if (prices.Length < p)
            return r;
        var initial = Divide(prices.Take(p).Aggregate(0d, Add), p);
        var stage = new[] { initial, initial, initial };
        for (var i = p; i < prices.Length; i++)
        {
            var previous = stage[2];
            var value = prices[i];
            for (var j = 0; j < 3; j++)
            {
                stage[j] = Add(stage[j], Multiply(k, Subtract(value, stage[j])));
                value = stage[j];
            }
            r[1][i] = value;
            r[0][i] =
                previous == 0 // NOSONAR: Native exact zero denominator.
                    ? value == 0 // NOSONAR: Native zero/zero becomes absent.
                        ? null
                        : value > 0
                            ? double.PositiveInfinity
                            : double.NegativeInfinity
                    : Divide(Multiply(100, Subtract(value, previous)), previous);
            if (!signal.HasValue || i < (long)p + signal.Value - 1)
                continue;
            var window = r[0].Skip(i - signal.Value + 1).Take(signal.Value).ToArray();
            if (window.Any(v => !v.HasValue))
                continue;
            r[2][i] = window.Any(v => !double.IsFinite(v!.Value))
                ? window.Sum(v => v!.Value) / signal.Value
                : Divide(window.Aggregate(0d, (s, v) => Add(s, v!.Value)), signal.Value);
        }
        return r;
    }

    internal static double?[] NativeTa(double[] prices, int p, bool firstPrice, int suppression)
    {
        var r = new double?[prices.Length];
        var first = 3L * (p - 1L + suppression) + 1;
        if (first >= prices.Length)
            return r;
        var packed = NativeTaPacked(prices, p, firstPrice, suppression, 0, prices.Length - 1);
        for (var i = 0; i < packed.Length; i++)
            r[(int)first + i] = packed[i];
        return r;
    }

    // Evaluate the native packed pipeline independently. Subranges reseed its
    // later stages from a truncated first-stage buffer, not full-history TRIX.
    internal static double[] NativeTaPacked(
        double[] prices,
        int p,
        bool firstPrice,
        int suppression,
        int start,
        int end
    )
    {
        var lb = p - 1 + suppression;
        var total = 3 * lb + 1;
        start = Math.Max(start, total);
        if (start > end)
            return [];
        var count = end - start + 1 + total;
        var first = NativeEma(
            prices.Take(end + 1).ToArray(),
            p,
            firstPrice,
            suppression,
            start - total
        );
        var second = NativeEma(first.Take(count - lb).ToArray(), p, firstPrice, suppression, 0);
        var third = NativeEma(second, p, firstPrice, suppression, 0);
        var output = new double[third.Length - 1];
        for (var i = 1; i < third.Length; i++)
            output[i - 1] =
                third[i - 1] == 0 ? 0 : Multiply(Subtract(Divide(third[i], third[i - 1]), 1), 100); // NOSONAR: Native ROC exact zero branch.
        return output;
    }

    private static double[] NativeEma(
        double[] values,
        int p,
        bool firstPrice,
        int suppression,
        int requestedStart
    )
    {
        var lb = p - 1 + suppression;
        var start = Math.Max(requestedStart, lb);
        if (start >= values.Length)
            return [];
        var state = firstPrice
            ? values[0]
            : Divide(values.Skip(start - lb).Take(p).Aggregate(0d, Add), p);
        var index = firstPrice ? 1 : start - lb + p;
        var alpha = Divide(2, (long)p + 1);
        for (; index <= start; index++)
            state = Add(Multiply(Subtract(values[index], state), alpha), state);
        var r = new double[values.Length - start];
        r[0] = state;
        for (var i = start + 1; i < values.Length; i++)
        {
            state = Add(Multiply(Subtract(values[i], state), alpha), state);
            r[i - start] = state;
        }
        return r;
    }
}
