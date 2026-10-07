using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SeededMomentumComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(bool pmo, int? smooth = null, int? signal = null)
    {
        var s = smooth ?? (pmo ? 20 : 13);
        var g = signal ?? (pmo ? 10 : 7);
        return new(
            pmo ? "Skender.GetPmo" : "Skender.GetTsi",
            pmo ? nameof(SeededPriceMomentum) : nameof(SeededTrueStrength),
            (d, p) => Native(d, p, s, g, pmo),
            (d, p) => Owned(d.IndicatorBars, p, s, g, pmo),
            (d, p) => Series(GridReference(d.Closes, p, s, g, pmo), pmo),
            [pmo ? "Pmo" : "Tsi", "Signal"],
            CompetitorReference: (d, p) =>
                Series(
                    NativeReference(d.Quotes.Select(q => (double)q.Close).ToArray(), p, s, g, pmo),
                    pmo
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );
    }

    internal static ComparisonSeries Series(double?[][] rows, bool pmo) =>
        RetrospectivePriceComparison.Series([pmo ? "Pmo" : "Tsi", "Signal"], rows);

    private static ComparisonSeries Native(CompetitorData d, int p, int s, int g, bool pmo)
    {
        if (pmo)
        {
            var rows = d.Quotes.GetPmo(p, s, g).ToArray();
            return Series(
                [rows.Select(v => v.Pmo).ToArray(), rows.Select(v => v.Signal).ToArray()],
                true
            );
        }
        var tsi = d.Quotes.GetTsi(p, s, g).ToArray();
        return Series(
            [tsi.Select(v => v.Tsi).ToArray(), tsi.Select(v => v.Signal).ToArray()],
            false
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int p, int s, int g, bool pmo)
    {
        IIndicator indicator = pmo
            ? new SeededPriceMomentum(p, s, g)
            : new SeededTrueStrength(p, s, g);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var result = new double?[2][];
        for (var j = 0; j < 2; j++)
        {
            var values = run[indicator.Outputs[j]].ToArray();
            var present = run[indicator.Outputs[j + 2]].ToArray();
            result[j] = values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray();
        }
        return Series(result, pmo);
    }

    private static BigInteger?[] GridSmooth(
        BigInteger?[] values,
        long start,
        int period,
        long denominator
    )
    {
        var result = new BigInteger?[values.Length];
        var first = start + period - 1;
        if (first >= values.Length)
            return result;
        var seed = values.Skip((int)start).Take(period).ToArray();
        if (seed.All(v => v.HasValue))
            result[(int)first] = DirectionalComparison.RoundedUnits(
                seed.Aggregate(BigInteger.Zero, (sum, v) => sum + v!.Value),
                period
            );
        for (var i = (int)first + 1; i < values.Length; i++)
            if (result[i - 1].HasValue && values[i].HasValue)
                result[i] = DirectionalComparison.RoundedUnits(
                    result[i - 1]!.Value * (denominator - 2) + 2 * values[i]!.Value,
                    denominator
                );
        return result;
    }

    internal static double?[][] GridReference(double[] prices, int p, int s, int g, bool pmo)
    {
        var input = new BigInteger?[prices.Length];
        for (var i = 1; i < prices.Length; i++)
        {
            var delta = Units(prices[i]) - Units(prices[i - 1]);
            if (!pmo)
                input[i] = DirectionalComparison.RoundedUnits(delta, 1);
            else if (prices[i - 1] != 0) // NOSONAR: Exact zero defines the missing-value boundary.
                input[i] = DirectionalComparison.RoundedUnits(
                    100 * delta * Grid * Math.Sign(prices[i - 1]),
                    BigInteger.Abs(Units(prices[i - 1]))
                );
        }
        var first = GridSmooth(input, 1, p, pmo ? p : (long)p + 1);
        if (pmo)
        {
            var scaled = first
                .Select(v =>
                    v.HasValue
                        ? (BigInteger?)DirectionalComparison.RoundedUnits(10 * v.Value, 1)
                        : null
                )
                .ToArray();
            var values = GridSmooth(scaled, p, s, s);
            var signals = GridSmooth(values, (long)p + s - 1, g, (long)g + 1);
            return
            [
                values.Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null).ToArray(),
                signals.Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null).ToArray(),
            ];
        }
        var absolute = GridSmooth(
            input.Select(v => v.HasValue ? (BigInteger?)BigInteger.Abs(v.Value) : null).ToArray(),
            1,
            p,
            (long)p + 1
        );
        var signed2 = GridSmooth(first, s == 1 ? (long)p + 1 : p, s, (long)s + 1);
        var abs2 = GridSmooth(absolute, s == 1 ? (long)p + 1 : p, s, (long)s + 1);
        var tsi = new double?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
            if (signed2[i].HasValue && abs2[i].HasValue && !abs2[i]!.Value.IsZero)
                tsi[i] = Round(100 * signed2[i]!.Value, abs2[i]!.Value);
        var signal = new double?[prices.Length];
        if (g <= 1)
            return [tsi, signal];
        var start = (long)p + s - 1;
        var seedIndex = start + g - 1;
        if (seedIndex >= prices.Length)
            return [tsi, signal];
        var window = Enumerable
            .Range((int)start, g)
            .Select(i => s == 1 && i == start ? (double?)0 : tsi[i])
            .ToArray();
        if (window.All(v => v.HasValue))
            signal[(int)seedIndex] = Round(
                window.Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v!.Value)),
                Grid * g
            );
        for (var i = (int)seedIndex + 1; i < prices.Length; i++)
            if (signal[i - 1].HasValue && tsi[i].HasValue)
                signal[i] = Round(
                    Units(signal[i - 1]!.Value) * (g - 1) + 2 * Units(tsi[i]!.Value),
                    Grid * ((long)g + 1)
                );
        return [tsi, signal];
    }

    private static double?[] NativeSmooth(
        double?[] values,
        int start,
        int period,
        double alpha,
        bool products
    )
    {
        var result = new double?[values.Length];
        var first = checked(start + period - 1);
        if (first >= values.Length)
            return result;
        double? seed = 0;
        foreach (var v in values.Skip(start).Take(period))
            seed += v;
        result[first] = seed / period;
        for (var i = first + 1; i < values.Length; i++)
            result[i] = products
                ? values[i] * alpha + result[i - 1] * (1 - alpha)
                : (values[i] - result[i - 1]) * alpha + result[i - 1];
        return result;
    }

    internal static double?[][] NativeReference(double[] prices, int p, int s, int g, bool pmo)
    {
        if (pmo)
        {
            var roc = new double?[prices.Length];
            for (var i = 1; i < prices.Length; i++)
                if (prices[i - 1] != 0) // NOSONAR: Exact zero defines the missing-value boundary.
                    roc[i] = 100 * (prices[i] - prices[i - 1]) / prices[i - 1];
            var firstPmo = NativeSmooth(roc, 1, p, 2d / p, true);
            var scaled = firstPmo.Select(v => v * 10).ToArray();
            var second = NativeSmooth(scaled, p, s, 2d / s, false);
            var signaled = NativeSmooth(second, checked(p + s - 1), g, 2d / checked(g + 1), false);
            return [second, signaled];
        }
        var k = 2d / checked(p + 1);
        var k2 = 2d / checked(s + 1);
        var kg = 2d / checked(g + 1);
        var delta = new double?[prices.Length];
        var absolute = new double?[prices.Length];
        for (var i = 1; i < prices.Length; i++)
        {
            delta[i] = prices[i] - prices[i - 1];
            absolute[i] = Math.Abs(delta[i]!.Value);
        }
        var firstSigned = NativeSmooth(delta, 1, p, k, false);
        var firstAbsolute = NativeSmooth(absolute, 1, p, k, false);
        double?[] UnseededSecond(double?[] input)
        {
            var r = new double?[prices.Length];
            var previous = 0d;
            for (var i = p + 1; i < prices.Length; i++)
            {
                previous = (input[i]!.Value - previous) * k2 + previous;
                r[i] = previous;
            }
            return r;
        }
        var first = checked(p + s - 1);
        var numerator =
            s == 1 ? UnseededSecond(firstSigned) : NativeSmooth(firstSigned, p, s, k2, false);
        var denominator =
            s == 1 ? UnseededSecond(firstAbsolute) : NativeSmooth(firstAbsolute, p, s, k2, false);
        var result = new[] { new double?[prices.Length], new double?[prices.Length] };
        double seedSignal = 0;
        for (var i = s == 1 ? p + 1 : first; i < prices.Length; i++)
        {
            var initial = s > 1 && i == first;
            var num = numerator[i]!.Value;
            var den = denominator[i]!.Value;
            var tsi =
                den == 0 ? double.NaN // NOSONAR: Exact zero defines the missing-value boundary.
                : initial ? 100 * num / den
                : 100 * (num / den);
            result[0][i] =
                initial ? tsi
                : double.IsNaN(tsi) ? null
                : tsi;
            if (initial)
            {
                seedSignal = tsi;
                continue;
            }
            if (g == 0)
                continue;
            var signalIndex = checked(p + s + g - 2);
            if (i > signalIndex)
            {
                var previous = result[1][i - 1];
                var term = (tsi - previous) * kg;
                if (term.HasValue && double.IsNaN(term.Value))
                    term = null;
                result[1][i] = term + previous;
            }
            else if (i == signalIndex)
            {
                seedSignal += tsi;
                result[1][i] = seedSignal / g;
            }
            else
                seedSignal += tsi;
        }
        return result;
    }

    // This dedicated boundary verifier retains present NaN/Infinity values from
    // the native API. The ordinary correctness gate still rejects nonfinite math.
    internal static int Check(ComparisonPair pair, CompetitorData data, int period)
    {
        var expected = pair.CompetitorReference!(data, period);
        var invalid = expected.Outputs.Values.Any(o =>
            o.Values.Where((_, i) => o.Present![i]).Any(v => !double.IsFinite(v))
        );
        if (!invalid)
            return ComparisonVerifier.Check(pair, data, period);
        ComparisonVerifier.Compare(
            pair.Reference!(data, period),
            pair.Ooples(data, period),
            pair.Id + " owned boundary",
            IndicatorErrorBudget.Exact
        );
        var actual = pair.Competitor(data, period);
        var checkedValues = 0;
        if (
            !expected
                .Outputs.Keys.OrderBy(v => v)
                .SequenceEqual(actual.Outputs.Keys.OrderBy(v => v))
        )
            throw new InvalidOperationException("Native boundary output names differ");
        foreach (var name in expected.Outputs.Keys)
        {
            var wanted = expected.Outputs[name];
            var got = actual.Outputs[name];
            if (
                wanted.FirstValid != got.FirstValid
                || wanted.Values.Length != got.Values.Length
                || wanted.Present!.Length != got.Present!.Length
            )
                throw new InvalidOperationException("Native boundary shape differs");
            for (var i = 0; i < wanted.Values.Length; i++)
            {
                if (wanted.Present[i] != got.Present[i])
                    throw new InvalidOperationException("Native boundary presence differs");
                var target = wanted.Values[i];
                var value = got.Values[i];
                if (
                    double.IsNaN(target) ? !double.IsNaN(value)
                    : double.IsInfinity(target) ? !target.Equals(value) // NOSONAR: Signed infinities must match exactly.
                    : !double.IsFinite(value) || !IndicatorErrorBudget.Exact.Accepts(target, value)
                )
                    throw new InvalidOperationException($"Native boundary {name}[{i}] differs");
                if (wanted.Present[i])
                    checkedValues++;
            }
        }
        return checkedValues;
    }
}
