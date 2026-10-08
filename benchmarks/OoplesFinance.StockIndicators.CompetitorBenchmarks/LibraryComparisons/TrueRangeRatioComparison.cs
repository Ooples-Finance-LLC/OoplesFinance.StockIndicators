using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TrueRangeRatioComparison
{
    internal static readonly ComparisonPair VortexPair = new(
        "Skender.GetVortex",
        nameof(WindowVortex),
        (d, p) =>
        {
            var r = d.Quotes.GetVortex(p).ToArray();
            return VortexSeries([r.Select(v => v.Pvi).ToArray(), r.Select(v => v.Nvi).ToArray()]);
        },
        (d, p) => OwnedVortex(d.IndicatorBars, p),
        (d, p) => VortexSeries(VortexReference(d.IndicatorBars, p, false)),
        ["Positive", "Negative"],
        CompetitorReference: (d, p) => VortexSeries(VortexReference(QuoteBars(d), p, true)),
        ErrorBudget: IndicatorErrorBudget.Exact
    );
    internal static readonly ComparisonPair[] Pairs =
    [
        VortexPair,
        UltimatePair(false),
        UltimatePair(true),
    ];

    private static Bar[] QuoteBars(CompetitorData d) =>
        d
            .Quotes.Select(q => new Bar(
                q.Date,
                (double)q.Open,
                (double)q.High,
                (double)q.Low,
                (double)q.Close,
                (double)q.Volume
            ))
            .ToArray();

    internal static ComparisonPair UltimatePair(bool ta, int? middle = null, int? longest = null) =>
        new(
            ta ? "TaLib.Functions.UltOsc" : "Skender.GetUltimate",
            nameof(WindowUltimateOscillator),
            (d, p) =>
                NativeUltimate(d, [p, middle ?? checked(2 * p), longest ?? checked(4 * p)], ta),
            (d, p) =>
                OwnedUltimate(
                    d.IndicatorBars,
                    [p, middle ?? checked(2 * p), longest ?? checked(4 * p)],
                    ta
                ),
            (d, p) =>
                VolumePriceComparison.Mask(
                    UltimateReference(
                        d.IndicatorBars,
                        [p, middle ?? checked(2 * p), longest ?? checked(4 * p)],
                        ta,
                        false
                    )
                ),
            MinimumInputCount: ta ? 2 : 1,
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(
                    UltimateReference(
                        ta ? d.IndicatorBars : QuoteBars(d),
                        [p, middle ?? checked(2 * p), longest ?? checked(4 * p)],
                        ta,
                        true
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries NativeUltimate(CompetitorData d, int[] p, bool ta)
    {
        if (!ta)
            return VolumePriceComparison.Mask(
                d.Quotes.GetUltimate(p[0], p[1], p[2]).Select(v => v.Ultimate).ToArray()
            );
        var result = new double?[d.Count];
        if (d.Count == 0)
            return VolumePriceComparison.Mask(result);
        var packed = new double[d.Count];
        var code = Functions.UltOsc<double>(
            d.Highs,
            d.Lows,
            d.Closes,
            System.Range.All,
            packed,
            out var range,
            p[0],
            p[1],
            p[2]
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA Ultimate returned " + code);
        var (start, count) = range.GetOffsetAndLength(d.Count);
        if (count != Math.Max(0, d.Count - p.Max()) || count > 0 && start != p.Max())
            throw new InvalidOperationException("Unexpected Ultimate alignment");
        for (var i = 0; i < count; i++)
            result[start + i] = packed[i];
        return VolumePriceComparison.Mask(result);
    }

    internal static ComparisonSeries VortexSeries(double?[][] values) =>
        RetrospectivePriceComparison.Series(["Positive", "Negative"], values);

    internal static ComparisonSeries OwnedVortex(Bar[] bars, int p)
    {
        var indicator = new WindowVortex(p);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = new double?[2][];
        for (var slot = 0; slot < 2; slot++)
        {
            var response = run[indicator.Outputs[slot]].ToArray();
            var mask = run[indicator.Outputs[slot + 2]].ToArray();
            values[slot] = response.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray();
        }
        return VortexSeries(values);
    }

    internal static ComparisonSeries OwnedUltimate(Bar[] bars, int[] p, bool ta)
    {
        var indicator = new WindowUltimateOscillator(p[0], p[1], p[2], ta);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var response = run[indicator.Value].ToArray();
        var mask = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            response.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[][] VortexReference(Bar[] bars, int period, bool native)
    {
        var result = new[] { new double?[bars.Length], new double?[bars.Length] };
        for (var i = period; i < bars.Length; i++)
        {
            BigInteger range = 0,
                pos = 0,
                neg = 0;
            double nr = 0,
                np = 0,
                nn = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                var b = bars[j];
                var old = bars[j - 1];
                if (native)
                {
                    nr = Add(
                        nr,
                        Math.Max(
                            Subtract(b.High, b.Low),
                            Math.Max(
                                Math.Abs(Subtract(b.High, old.Close)),
                                Math.Abs(Subtract(b.Low, old.Close))
                            )
                        )
                    );
                    np = Add(np, Math.Abs(Subtract(b.High, old.Low)));
                    nn = Add(nn, Math.Abs(Subtract(b.Low, old.High)));
                }
                else
                {
                    range += BigInteger.Max(
                        Units(b.High) - Units(b.Low),
                        BigInteger.Max(
                            BigInteger.Abs(Units(b.High) - Units(old.Close)),
                            BigInteger.Abs(Units(b.Low) - Units(old.Close))
                        )
                    );
                    pos += BigInteger.Abs(Units(b.High) - Units(old.Low));
                    neg += BigInteger.Abs(Units(b.Low) - Units(old.High));
                }
            }
            if (native)
            {
                if (nr == 0) // NOSONAR: Native Vortex tests exactly zero total range.
                    continue;
                result[0][i] = Divide(np, nr);
                result[1][i] = Divide(nn, nr);
            }
            else if (!range.IsZero)
            {
                result[0][i] = Round(pos, range);
                result[1][i] = Round(neg, range);
            }
        }
        return result;
    }

    internal static double?[] UltimateReference(Bar[] bars, int[] requested, bool ta, bool native)
    {
        var p = (int[])requested.Clone();
        if (ta)
            Array.Sort(p);
        var result = new double?[bars.Length];
        if (bars.Length <= p[2])
            return result;
        var pressures = new BigInteger[bars.Length];
        var ranges = new BigInteger[bars.Length];
        var bp = new double[bars.Length];
        var tr = new double[bars.Length];
        for (var i = 1; i < bars.Length; i++)
        {
            var b = bars[i];
            var previous = bars[i - 1].Close;
            if (native)
            {
                bp[i] = Subtract(b.Close, Math.Min(b.Low, previous));
                tr[i] = ta
                    ? Math.Max(
                        Subtract(b.High, b.Low),
                        Math.Max(
                            Math.Abs(Subtract(b.High, previous)),
                            Math.Abs(Subtract(b.Low, previous))
                        )
                    )
                    : Subtract(Math.Max(b.High, previous), Math.Min(b.Low, previous));
            }
            else
            {
                var h = Units(b.High);
                var l = Units(b.Low);
                var c = Units(previous);
                pressures[i] = Units(b.Close) - BigInteger.Min(l, c);
                ranges[i] = ta
                    ? BigInteger.Max(
                        h - l,
                        BigInteger.Max(BigInteger.Abs(h - c), BigInteger.Abs(l - c))
                    )
                    : BigInteger.Max(h, c) - BigInteger.Min(l, c);
            }
        }
        var a = new double[3];
        var r = new double[3];
        if (native && ta)
            for (var k = 0; k < 3; k++)
            for (var j = p[2] - p[k] + 1; j < p[2]; j++)
            {
                a[k] = Add(a[k], bp[j]);
                r[k] = Add(r[k], tr[j]);
            }
        for (var i = p[2]; i < bars.Length; i++)
        {
            BigInteger numerator = 0,
                denominator = 1;
            double weighted = 0;
            bool present = true;
            for (var k = 0; k < 3; k++)
            {
                if (native)
                {
                    if (ta)
                    {
                        a[k] = Add(a[k], bp[i]);
                        r[k] = Add(r[k], tr[i]);
                    }
                    else
                    {
                        a[k] = 0;
                        r[k] = 0;
                        for (var j = i - p[k] + 1; j <= i; j++)
                        {
                            a[k] = Add(a[k], bp[j]);
                            r[k] = Add(r[k], tr[j]);
                        }
                    }
                    if (r[k] == 0) // NOSONAR: Native zero-range branches are exact.
                    {
                        if (!ta)
                            present = false;
                    }
                    else
                        weighted = Add(weighted, Multiply(4 >> k, Divide(a[k], r[k])));
                    if (ta)
                    {
                        a[k] = Subtract(a[k], bp[i - p[k] + 1]);
                        r[k] = Subtract(r[k], tr[i - p[k] + 1]);
                    }
                }
                else
                {
                    var top = pressures
                        .Skip(i - p[k] + 1)
                        .Take(p[k])
                        .Aggregate(BigInteger.Zero, (s, v) => s + v);
                    var bottom = ranges
                        .Skip(i - p[k] + 1)
                        .Take(p[k])
                        .Aggregate(BigInteger.Zero, (s, v) => s + v);
                    if (bottom.IsZero)
                    {
                        if (!ta)
                            present = false;
                        continue;
                    }
                    numerator = numerator * bottom + (4 >> k) * top * denominator;
                    denominator *= bottom;
                }
            }
            if (present)
                result[i] = native
                    ? (ta ? Multiply(100, Divide(weighted, 7)) : Divide(Multiply(100, weighted), 7))
                    : Round(100 * numerator, 7 * denominator);
        }
        return result;
    }
}
