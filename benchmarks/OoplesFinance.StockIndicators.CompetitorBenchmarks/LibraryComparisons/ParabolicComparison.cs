using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal sealed record SarParameters(
    double Step = .02,
    double Maximum = .2,
    double Initial = .02,
    double Start = 0,
    double Offset = 0,
    double ShortInitial = .02,
    double ShortStep = .02,
    double ShortMaximum = .2
);

internal static class ParabolicComparison
{
    internal static readonly string[] Ids =
    [
        "TaLib.Functions.Sar",
        "TaLib.Functions.SarExt",
        "Skender.GetParabolicSar",
        "Trady.Indicator.ParabolicStopAndReverse",
    ];

    internal static ComparisonSeries Series(double?[][] rows, int kind) =>
        RetrospectivePriceComparison.Series(kind == 2 ? ["Sar", "IsReversal"] : ["Sar"], rows);

    internal static ComparisonPair Pair(int kind, SarParameters? parameters = null)
    {
        var p = parameters ?? new();
        return new(
            Ids[kind],
            nameof(ParabolicStopSnapshots),
            (d, _) => Native(d, kind, p),
            (d, _) => Owned(d.IndicatorBars, kind, p),
            (d, _) => Series(Model(d, kind, p, false), kind),
            kind == 2 ? ["Sar", "IsReversal"] : ["Sar"],
            MinimumInputCount: kind < 2 ? 2 : 0,
            CompetitorReference: (d, _) => Series(Model(d, kind, p, true), kind),
            ErrorBudget: IndicatorErrorBudget.Exact
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int kind, SarParameters p)
    {
        if (kind == 2)
        {
            var rows = ParabolicStopSnapshots.Confirmed(bars, p.Step, p.Maximum, p.Initial);
            return Series(
                [
                    rows.Select(r => r.Sar).ToArray(),
                    rows.Select(r =>
                            r.IsReversal.HasValue
                                ? r.IsReversal.Value
                                    ? (double?)1
                                    : 0
                                : null
                        )
                        .ToArray(),
                ],
                kind
            );
        }
        return Series(
            [
                (
                    kind == 0 ? ParabolicStopSnapshots.Classic(bars, p.Step, p.Maximum)
                    : kind == 1
                        ? ParabolicStopSnapshots.Extended(
                            bars,
                            p.Start,
                            p.Offset,
                            p.Initial,
                            p.Step,
                            p.Maximum,
                            p.ShortInitial,
                            p.ShortStep,
                            p.ShortMaximum
                        )
                    : ParabolicStopSnapshots.FourBar(bars, p.Step, p.Maximum)
                ).ToArray(),
            ],
            kind
        );
    }

    private static ComparisonSeries Native(CompetitorData d, int kind, SarParameters p)
    {
        if (kind < 2)
        {
            var output = new double[d.Count];
            System.Range range;
            var code =
                kind == 0
                    ? Functions.Sar<double>(
                        d.Highs,
                        d.Lows,
                        System.Range.All,
                        output,
                        out range,
                        p.Step,
                        p.Maximum
                    )
                    : Functions.SarExt<double>(
                        d.Highs,
                        d.Lows,
                        System.Range.All,
                        output,
                        out range,
                        p.Start,
                        p.Offset,
                        p.Initial,
                        p.Step,
                        p.Maximum,
                        p.ShortInitial,
                        p.ShortStep,
                        p.ShortMaximum
                    );
            if (code != TALib.Core.RetCode.Success)
                throw new InvalidOperationException("Native SAR failed: " + code);
            var values = new double?[d.Count];
            for (var i = range.Start.Value; i < range.End.Value; i++)
                values[i] = output[i - range.Start.Value];
            return Series([values], kind);
        }
        if (kind == 2)
        {
            var rows = d.Quotes.GetParabolicSar(p.Step, p.Maximum, p.Initial).ToArray();
            return Series(
                [
                    rows.Select(r => r.Sar).ToArray(),
                    rows.Select(r =>
                            r.IsReversal.HasValue
                                ? r.IsReversal.Value
                                    ? (double?)1
                                    : 0
                                : null
                        )
                        .ToArray(),
                ],
                kind
            );
        }
        return Series(
            [
                new Trady.Analysis.Indicator.ParabolicStopAndReverse(
                    d.Candles,
                    (decimal)p.Step,
                    (decimal)p.Maximum
                )
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray(),
            ],
            kind
        );
    }

    private readonly record struct Number(BigInteger Units, decimal Decimal = 0);

    private sealed class Arithmetic(int mode)
    {
        internal Number Price(double v) =>
            mode == 2 ? Decimal((decimal)v) : new(MoneyFlowReferenceArithmetic.Units(v));

        internal Number Decimal(decimal v) => new(BigInteger.Zero, v);

        internal int Compare(Number a, Number b) =>
            mode == 2 ? a.Decimal.CompareTo(b.Decimal) : a.Units.CompareTo(b.Units);

        internal Number Min(Number a, Number b) => Compare(a, b) < 0 ? a : b;

        internal Number Max(Number a, Number b) => Compare(a, b) > 0 ? a : b;

        internal Number Add(Number a, Number b) =>
            mode == 2 ? Decimal(a.Decimal + b.Decimal) : Finish(a.Units + b.Units, 1);

        internal Number Sub(Number a, Number b) =>
            mode == 2 ? Decimal(a.Decimal - b.Decimal) : Finish(a.Units - b.Units, 1);

        internal Number Multiply(Number a, Number b) =>
            mode == 2 ? Decimal(a.Decimal * b.Decimal) : Finish(a.Units * b.Units, Grid);

        private Number Finish(BigInteger n, BigInteger d) =>
            new(mode == 0 ? n / d : Units(Round(n, Grid * d)));

        internal Number Advance(Number s, Number e, Number f, bool subtract = false) =>
            mode == 0
                ? new(
                    DirectionalComparison.RoundedUnits(
                        (Grid - f.Units) * s.Units + f.Units * e.Units,
                        Grid
                    )
                )
            : subtract ? Sub(s, Multiply(f, Sub(s, e)))
            : Add(s, Multiply(f, Sub(e, s)));

        internal Number Offset(Number s, Number offset, bool down) =>
            mode == 0
                ? new(
                    DirectionalComparison.RoundedUnits(
                        s.Units * (Grid + (down ? offset.Units : -offset.Units)),
                        Grid
                    )
                )
            : down ? Add(s, Multiply(s, offset))
            : Sub(s, Multiply(s, offset));

        internal Number Increment(Number f, Number step, Number maximum)
        {
            if (mode == 1)
                return Price(Math.Min(Value(f) + Value(step), Value(maximum)));
            var value = Min(Add(f, step), maximum);
            return mode == 0 ? new(DirectionalComparison.RoundedUnits(value.Units, 1)) : value;
        }

        internal double Value(Number n) => mode == 2 ? (double)n.Decimal : Round(n.Units, Grid);
    }

    internal static double?[][] Model(CompetitorData data, int kind, SarParameters p, bool native)
    {
        var a = new Arithmetic(
            native
                ? kind == 3
                    ? 2
                    : 1
                : 0
        );
        var rows = Enumerable
            .Range(0, kind == 2 ? 2 : 1)
            .Select(_ => new double?[data.Count])
            .ToArray();
        var h = data.IndicatorBars.Select(b => a.Price(b.High)).ToArray();
        var l = data.IndicatorBars.Select(b => a.Price(b.Low)).ToArray();
        if (native && kind == 2)
        {
            h = data.Quotes.Select(q => a.Price((double)q.High)).ToArray();
            l = data.Quotes.Select(q => a.Price((double)q.Low)).ToArray();
        }
        if (native && kind == 3)
        {
            h = data.Candles.Select(q => a.Decimal(q.High)).ToArray();
            l = data.Candles.Select(q => a.Decimal(q.Low)).ToArray();
        }
        bool Less(Number x, Number y) => a.Compare(x, y) < 0;
        bool More(Number x, Number y) => a.Compare(x, y) > 0;
        var zero = a.Price(0);
        var step = a.Price(p.Step);
        var maximum = a.Price(p.Maximum);
        if (data.Count < (kind == 3 ? 5 : 2))
            return rows;
        bool rising;
        Number stop,
            extreme,
            factor;
        if (kind == 3)
        {
            rising = More(h[4], h[3]) && More(l[4], l[3]);
            if (!rising && !(Less(h[4], h[3]) && Less(l[4], l[3])))
                rising = More(a.Add(h[4], l[4]), a.Add(h[3], l[3]));
            var highest = h.Take(5).Aggregate(a.Max);
            var lowest = l.Take(5).Aggregate(a.Min);
            stop = rising ? lowest : highest;
            extreme = rising ? highest : lowest;
            factor = step;
            rows[0][4] = a.Value(stop);
            void AdvanceFour(int i)
            {
                var same = rising ? More(l[i - 1], stop) : Less(h[i - 1], stop);
                if (same)
                {
                    stop = a.Advance(stop, extreme, factor);
                    stop = rising
                        ? a.Min(stop, a.Min(l[i - 1], l[i - 2]))
                        : a.Max(stop, a.Max(h[i - 1], h[i - 2]));
                    var fresh = rising ? More(h[i], extreme) : Less(l[i], extreme);
                    extreme = rising ? a.Max(h[i], extreme) : a.Min(l[i], extreme);
                    if (fresh && (rising ? More(l[i], stop) : Less(h[i], stop)))
                        factor = native
                            ? a.Add(factor, a.Min(step, a.Sub(maximum, factor)))
                            : a.Increment(factor, step, maximum);
                }
                else
                {
                    stop = extreme;
                    rising = !rising;
                    extreme = rising ? a.Max(extreme, h[i]) : a.Min(extreme, l[i]);
                    factor = step;
                }
            }
            var cached = new Number[data.Count];
            cached[4] = stop;
            for (var i = 5; i < data.Count; i++)
            {
                // Trady 3.2.8 leaves LastCacheIndex one behind. Sequential Compute
                // replays the previous transition with its already advanced fields.
                if (native && i > 5)
                {
                    stop = cached[i - 2];
                    AdvanceFour(i - 1);
                    cached[i - 1] = stop;
                }
                stop = cached[i - 1];
                AdvanceFour(i);
                cached[i] = stop;
                rows[0][i] = a.Value(stop);
            }
            return rows;
        }
        if (kind == 2)
        {
            rising = true;
            stop = l[0];
            extreme = h[0];
            factor = a.Price(p.Initial);
            var first = -1;
            for (var i = 1; i < data.Count; i++)
            {
                stop = a.Advance(stop, extreme, factor, !rising);
                if (i > 1)
                    stop = rising
                        ? a.Min(stop, a.Min(l[i - 1], l[i - 2]))
                        : a.Max(stop, a.Max(h[i - 1], h[i - 2]));
                var reverse = rising ? Less(l[i], stop) : More(h[i], stop);
                if (reverse)
                {
                    stop = extreme;
                    rising = !rising;
                    extreme = rising ? h[i] : l[i];
                    factor = a.Price(p.Initial);
                    if (first < 0)
                        first = i;
                }
                else if (rising ? More(h[i], extreme) : Less(l[i], extreme))
                {
                    extreme = rising ? h[i] : l[i];
                    factor = a.Increment(factor, step, maximum);
                }
                rows[0][i] = a.Value(stop);
                rows[1][i] = reverse ? 1 : 0;
            }
            for (var i = 0; i <= (first < 0 ? data.Count - 1 : first); i++)
                rows[0][i] = rows[1][i] = null;
            return rows;
        }
        var down = a.Sub(l[0], l[1]);
        var up = a.Sub(h[1], h[0]);
        rising = kind == 1 && p.Start != 0 ? p.Start > 0 : !(More(down, zero) && More(down, up));
        stop =
            kind == 1 && p.Start != 0 ? a.Price(Math.Abs(p.Start))
            : rising ? l[0]
            : h[0];
        extreme = rising ? h[1] : l[1];
        Number Initial(bool direction) =>
            a.Price(
                Math.Min(
                    kind == 0 ? p.Step
                        : direction ? p.Initial
                        : p.ShortInitial,
                    kind == 0 || direction ? p.Maximum : p.ShortMaximum
                )
            );
        factor = Initial(rising);
        for (var i = 1; i < data.Count; i++)
        {
            var prior = i == 1 ? 1 : i - 1;
            var reverse = rising ? !More(l[i], stop) : !Less(h[i], stop);
            if (reverse)
            {
                rising = !rising;
                stop = rising
                    ? a.Min(extreme, a.Min(l[prior], l[i]))
                    : a.Max(extreme, a.Max(h[prior], h[i]));
                if (kind == 1 && p.Offset > 0)
                    stop = a.Offset(stop, a.Price(p.Offset), !rising);
                extreme = rising ? h[i] : l[i];
                factor = Initial(rising);
            }
            rows[0][i] = a.Value(stop) * (kind == 1 && !rising ? -1 : 1);
            if (!reverse && (rising ? More(h[i], extreme) : Less(l[i], extreme)))
            {
                extreme = rising ? h[i] : l[i];
                var cap = a.Price(kind == 0 || rising ? p.Maximum : p.ShortMaximum);
                var delta = a.Min(a.Price(kind == 0 || rising ? p.Step : p.ShortStep), cap);
                factor = a.Increment(factor, delta, cap);
            }
            stop = a.Advance(stop, extreme, factor);
            stop = rising ? a.Min(stop, a.Min(l[prior], l[i])) : a.Max(stop, a.Max(h[prior], h[i]));
        }
        return rows;
    }
}
