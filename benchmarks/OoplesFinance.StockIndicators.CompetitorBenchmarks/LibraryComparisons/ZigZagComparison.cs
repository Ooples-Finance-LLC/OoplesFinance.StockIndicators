using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ZigZagComparison
{
    internal static ComparisonSeries Series(IEnumerable<ZigZagValue> source)
    {
        var rows = source.ToArray();
        return RetrospectivePriceComparison.Series(
            ["ZigZag", "PointType", "RetraceHigh", "RetraceLow"],
            [
                rows.Select(r => r.ZigZag).ToArray(),
                rows.Select(r => (double?)r.PointType).ToArray(),
                rows.Select(r => r.RetraceHigh).ToArray(),
                rows.Select(r => r.RetraceLow).ToArray(),
            ]
        );
    }

    internal static ComparisonPair Pair(double percent = 5, bool highLow = false) =>
        new(
            "Skender.GetZigZag",
            nameof(ZigZagSnapshot),
            (d, _) =>
                Series(
                    d.Quotes.GetZigZag(highLow ? EndType.HighLow : EndType.Close, (decimal)percent)
                        .Select(r => new ZigZagValue(
                            (double?)r.ZigZag,
                            r.PointType == "H" ? ZigZagPointKind.High
                                : r.PointType == "L" ? ZigZagPointKind.Low
                                : null,
                            (double?)r.RetraceHigh,
                            (double?)r.RetraceLow
                        ))
                ),
            (d, _) => Series(ZigZagSnapshot.Calculate(d.IndicatorBars, percent, highLow)),
            (d, _) => Series(Reference(d, percent, highLow, false)),
            ["ZigZag", "PointType", "RetraceHigh", "RetraceLow"],
            CompetitorReference: (d, _) => Series(Reference(d, percent, highLow, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private readonly record struct Price(BigInteger U, decimal D);

    internal static ZigZagValue[] Reference(
        CompetitorData data,
        double percent,
        bool highLow,
        bool native
    )
    {
        Price P(double v) => new(Units(v), 0);
        var high = native
            ? data.Quotes.Select(q => new Price(0, highLow ? q.High : q.Close)).ToArray()
            : data.IndicatorBars.Select(b => P(highLow ? b.High : b.Close)).ToArray();
        var low = native
            ? data.Quotes.Select(q => new Price(0, highLow ? q.Low : q.Close)).ToArray()
            : data.IndicatorBars.Select(b => P(highLow ? b.Low : b.Close)).ToArray();
        int Compare(Price a, Price b) => native ? a.D.CompareTo(b.D) : a.U.CompareTo(b.U);
        (BigInteger N, BigInteger D) Fraction(decimal v)
        {
            var bits = decimal.GetBits(v);
            var n =
                (BigInteger)(uint)bits[0]
                + ((BigInteger)(uint)bits[1] << 32)
                + ((BigInteger)(uint)bits[2] << 64);
            if (bits[3] < 0)
                n = -n;
            return (n, BigInteger.Pow(10, (bits[3] >> 16) & 255));
        }
        (BigInteger N, BigInteger D)? Ratio(Price a, Price b, bool down)
        {
            if (native)
            {
                if (b.D == 0)
                    return null;
                var v = (down ? b.D - a.D : a.D - b.D) / b.D;
                return Fraction(v);
            }
            if (b.U.IsZero)
                return null;
            var numerator = down ? b.U - a.U : a.U - b.U;
            return (numerator * b.U.Sign, BigInteger.Abs(b.U));
        }
        var threshold = native ? Fraction((decimal)percent / 100m) : (Units(percent), 100 * Grid);
        bool Meets((BigInteger N, BigInteger D)? v) =>
            v.HasValue && v.Value.N * threshold.Item2 >= threshold.Item1 * v.Value.D;
        bool Greater((BigInteger N, BigInteger D)? a, (BigInteger N, BigInteger D)? b) =>
            a.HasValue && b.HasValue && a.Value.N * b.Value.D > b.Value.N * a.Value.D;
        double Line(Price a, Price b, int offset, int length) =>
            native
                ? (double)(a.D + (b.D - a.D) / length * offset)
                : Round(a.U * (length - offset) + b.U * offset, length * Grid);
        var rows = Enumerable
            .Range(0, data.Count)
            .Select(_ => new ZigZagValue(null, null, null, null))
            .ToArray();
        if (data.Count == 0)
            return rows;
        var direction = 0; // -1 low, +1 high, 0 unknown
        for (var i = 0; i < data.Count; i++)
        {
            var up = Ratio(high[i], low[0], false);
            var down = Ratio(low[i], high[0], true);
            if (Meets(up) && Greater(up, down))
            {
                direction = -1;
                break;
            }
            if (Meets(down) && Greater(down, up))
            {
                direction = 1;
                break;
            }
        }
        var last = 0;
        var price =
            direction < 0 ? low[0]
            : direction > 0 ? high[0]
            : native ? new Price(0, data.Quotes[0].Close)
            : P(data.Closes[0]);
        var highIndex = 0;
        var lowIndex = 0;
        var highPrice = high[0];
        var lowPrice = low[0];
        while (last < data.Count - 1)
        {
            var rising = direction < 0;
            var end = last;
            var extreme = price;
            var type = rising ? 1 : -1;
            for (var i = last + 1; i < data.Count; i++)
            {
                var candidate = rising ? high[i] : low[i];
                var cmp = Compare(candidate, extreme);
                if (rising ? cmp >= 0 : cmp <= 0)
                {
                    end = i;
                    extreme = candidate;
                }
                else if (Meets(Ratio(rising ? low[i] : high[i], extreme, rising)))
                    break;
                if (i == data.Count - 1)
                {
                    end = i;
                    extreme = candidate;
                    type = 0;
                }
            }
            for (var i = last + 1; i <= end; i++)
                rows[i] = rows[i] with
                {
                    ZigZag =
                        last > 0 || i == end ? Line(price, extreme, i - last, end - last) : null,
                    PointType =
                        i == end && type != 0
                            ? type > 0
                                ? ZigZagPointKind.High
                                : ZigZagPointKind.Low
                            : null,
                };
            if (direction != 0)
            {
                var prior = direction < 0 ? highIndex : lowIndex;
                var anchor = direction < 0 ? highPrice : lowPrice;
                if (prior != 0 && prior != end)
                    for (var i = prior; i <= end; i++)
                        rows[i] =
                            direction < 0
                                ? rows[i] with
                                {
                                    RetraceHigh = Line(anchor, extreme, i - prior, end - prior),
                                }
                                : rows[i] with
                                {
                                    RetraceLow = Line(anchor, extreme, i - prior, end - prior),
                                };
                if (direction < 0)
                {
                    highIndex = end;
                    highPrice = extreme;
                }
                else
                {
                    lowIndex = end;
                    lowPrice = extreme;
                }
            }
            last = end;
            price = extreme;
            direction = type;
        }
        return rows;
    }
}
