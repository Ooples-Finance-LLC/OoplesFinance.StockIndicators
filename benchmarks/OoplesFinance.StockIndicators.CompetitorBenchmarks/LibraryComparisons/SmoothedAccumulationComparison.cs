using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SmoothedAccumulationComparison
{
    internal static readonly string[] Names =
    [
        "Oscillator",
        "MoneyFlowMultiplier",
        "MoneyFlowVolume",
        "Adl",
    ];
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonSeries Series(double?[][] rows, bool first) =>
        RetrospectivePriceComparison.Series(first ? [Names[0]] : Names, first ? [rows[0]] : rows);

    internal static ComparisonPair Pair(
        bool first,
        int fast = 3,
        int slow = 10,
        int suppression = 0
    ) =>
        new(
            first ? "TaLib.Functions.AdOsc" : "Skender.GetChaikinOsc",
            nameof(SmoothedAccumulationOscillator),
            (d, _) => Native(d, first, fast, slow),
            (d, _) => Owned(d.IndicatorBars, first, fast, slow, suppression),
            (d, _) => Reference(d, first, fast, slow, suppression, false),
            first ? [Names[0]] : Names,
            MinimumInputCount: first ? 2 : 0,
            CompetitorReference: (d, _) => Reference(d, first, fast, slow, suppression, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(
        Bar[] bars,
        bool first,
        int fast,
        int slow,
        int suppression = 0
    )
    {
        var indicator = new SmoothedAccumulationOscillator(fast, slow, first, suppression, !first);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var rows = new double?[4][];
        for (var j = 0; j < 4; j++)
        {
            var values = run[indicator.Outputs[j]].ToArray();
            var flags = run[indicator.Outputs[j + 4]].ToArray();
            rows[j] = values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
        }
        return Series(rows, first);
    }

    private static ComparisonSeries Native(CompetitorData d, bool first, int fast, int slow)
    {
        if (!first)
        {
            var rows = d.Quotes.GetChaikinOsc(fast, slow).ToArray();
            return Series(
                [
                    rows.Select(r => r.Oscillator).ToArray(),
                    rows.Select(r => r.MoneyFlowMultiplier).ToArray(),
                    rows.Select(r => r.MoneyFlowVolume).ToArray(),
                    rows.Select(r => r.Adl).ToArray(),
                ],
                false
            );
        }
        var values = new double?[d.Count];
        if (d.Count > 0)
        {
            var output = new double[d.Count];
            var code = Functions.AdOsc<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                d.Volumes,
                System.Range.All,
                output,
                out var range,
                fast,
                slow
            );
            if (code != TALib.Core.RetCode.Success)
                throw new InvalidOperationException("TA ADOSC failed: " + code);
            for (var i = range.Start.Value; i < range.End.Value; i++)
                values[i] = output[i - range.Start.Value];
        }
        return Series([values], true);
    }

    // Integer-grid oracle is separate from the production state and rational validation reference.
    internal static ComparisonSeries Reference(
        CompetitorData d,
        bool first,
        int fast,
        int slow,
        int suppression,
        bool native
    )
    {
        var rows = Enumerable.Range(0, 4).Select(_ => new double?[d.Count]).ToArray();
        var line = new BigInteger[d.Count];
        BigInteger total = 0;
        double nativeTotal = 0;
        double Price(double x) => native && !first ? (double)(decimal)x : x;
        for (var i = 0; i < d.Count; i++)
        {
            var h = Price(d.Highs[i]);
            var l = Price(d.Lows[i]);
            var c = Price(d.Closes[i]);
            var v = Price(d.Volumes[i]);
            var range = Units(h) - Units(l);
            var n = 2 * Units(c) - Units(h) - Units(l);
            BigInteger flow = 0,
                multiplier = 0;
            if (first ? range.Sign > 0 : !range.IsZero)
            {
                var denominator = BigInteger.Abs(range);
                multiplier = DirectionalComparison.RoundedUnits(
                    (n * range.Sign) << 1074,
                    denominator
                );
                flow = DirectionalComparison.RoundedUnits(n * range.Sign * Units(v), denominator);
            }
            total += flow;
            line[i] = DirectionalComparison.RoundedUnits(total, 1);
            rows[1][i] = Round(multiplier, Grid);
            rows[2][i] = Round(flow, Grid);
            if (native)
            {
                var r = Subtract(h, l);
                var m =
                    (first ? r > 0 : r != 0)
                        ? Divide(Subtract(Subtract(c, l), Subtract(h, c)), r)
                        : 0;
                var f = Multiply(m, v);
                nativeTotal = Add(nativeTotal, f);
                rows[1][i] = m;
                rows[2][i] = f;
                line[i] = Units(nativeTotal);
            }
            rows[3][i] = Round(line[i], Grid);
        }
        var a = Average(line, fast, first, native);
        var b = Average(line, slow, first, native);
        for (var i = (long)Math.Max(fast, slow) - 1 + suppression; i < d.Count; i++)
            rows[0][(int)i] = Round(a[(int)i] - b[(int)i], Grid);
        return Series(rows, first);
    }

    private static BigInteger[] Average(BigInteger[] input, int p, bool first, bool native)
    {
        var result = new BigInteger[input.Length];
        var start = first ? 0 : p - 1;
        if (start >= input.Length)
            return result;
        var sum = input
            .Take(start + 1)
            .Aggregate(
                BigInteger.Zero,
                (s, v) => native ? Units(Add(Round(s, Grid), Round(v, Grid))) : s + v
            );
        result[start] = DirectionalComparison.RoundedUnits(sum, start + 1);
        var alpha = Divide(2, (double)p + 1);
        for (var i = start + 1; i < input.Length; i++)
        {
            if (!native)
                result[i] = DirectionalComparison.RoundedUnits(
                    result[i - 1] * (p - 1) + 2 * input[i],
                    (long)p + 1
                );
            else
            {
                var previous = Round(result[i - 1], Grid);
                var value = Round(input[i], Grid);
                result[i] = Units(
                    first
                        ? Add(Multiply(alpha, value), Multiply(Subtract(1, alpha), previous))
                        : Add(Multiply(Subtract(value, previous), alpha), previous)
                );
            }
        }
        return result;
    }

    internal static T[] NativePacked<T>(
        T[] high,
        T[] low,
        T[] close,
        T[] volume,
        int fast,
        int slow,
        int suppression,
        int start,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var lookback = Math.Max(fast, slow) - 1 + suppression;
        start = Math.Max(start, lookback);
        if (start > end)
            return [];
        T total = T.Zero,
            a = T.Zero,
            b = T.Zero;
        var ka = T.CreateChecked(2) / (T.CreateChecked(fast) + T.One);
        var kb = T.CreateChecked(2) / (T.CreateChecked(slow) + T.One);
        var result = new List<T>();
        for (var i = start - lookback; i <= end; i++)
        {
            var range = high[i] - low[i];
            if (range > T.Zero)
                total += ((close[i] - low[i]) - (high[i] - close[i])) / range * volume[i];
            if (i == start - lookback)
            {
                a = total;
                b = total;
            }
            else
            {
                a = ka * total + (T.One - ka) * a;
                b = kb * total + (T.One - kb) * b;
            }
            if (i >= start)
                result.Add(a - b);
        }
        return result.ToArray();
    }
}
