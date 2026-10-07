using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RenkoComparison
{
    internal static readonly string[] Names =
    [
        "DateHigh",
        "DateLow",
        "Open",
        "High",
        "Low",
        "Close",
        "Volume",
        "IsUp",
    ];

    internal static ComparisonSeries Series(IEnumerable<RenkoBrick> source)
    {
        var rows = source.ToArray();
        return new(
            Names
                .Select(
                    (name, k) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                rows.Select(r =>
                                        k switch
                                        {
                                            0 => (double)(r.Date.Ticks >> 32),
                                            1 => (double)(r.Date.Ticks & uint.MaxValue),
                                            2 => r.Open,
                                            3 => r.High,
                                            4 => r.Low,
                                            5 => r.Close,
                                            6 => r.Volume,
                                            _ => r.IsUp ? 1d : 0d,
                                        }
                                    )
                                    .ToArray()
                            )
                        )
                )
                .ToDictionary(x => x.Key, x => x.Value)
        );
    }

    internal static ComparisonPair Pair(bool atr = false, double size = 1, bool highLow = false) =>
        new(
            atr ? "Skender.GetRenkoAtr" : "Skender.GetRenko",
            nameof(RenkoSnapshot),
            (d, p) =>
                Series(
                    (
                        atr
                            ? d.Quotes.GetRenkoAtr(p, highLow ? EndType.HighLow : EndType.Close)
                            : d.Quotes.GetRenko(
                                (decimal)size,
                                highLow ? EndType.HighLow : EndType.Close
                            )
                    ).Select(r => new RenkoBrick(
                        r.Date,
                        (double)r.Open,
                        (double)r.High,
                        (double)r.Low,
                        (double)r.Close,
                        (double)r.Volume,
                        r.IsUp
                    ))
                ),
            (d, p) =>
                Series(
                    atr
                        ? RenkoSnapshot.CalculateAtr(d.IndicatorBars, p, highLow)
                        : RenkoSnapshot.Calculate(d.IndicatorBars, size, highLow)
                ),
            (d, p) => Series(Reference(d, p, atr, size, highLow, false)),
            Names,
            CompetitorReference: (d, p) => Series(Reference(d, p, atr, size, highLow, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static IEnumerable<RenkoBrick> Reference(
        CompetitorData data,
        int period,
        bool atr,
        double size,
        bool highLow,
        bool native
    )
    {
        if (atr)
        {
            if (data.Count <= period)
                yield break;
            size = SeededAtrComparison
                .Reference(data, period, false, native ? 1 : 0)
                .Outputs.Values.Single()
                .Values[^1];
            if (size == 0)
                yield break;
        }
        if (data.Count == 0)
            yield break;
        if (native)
        {
            decimal brick = (decimal)size;
            if (brick == 0)
                yield break;
            var fraction = brick % 1;
            var digits = 0;
            while (fraction != 0)
            {
                fraction = fraction * 10 % 1;
                digits++;
            }
            var open = Math.Round(data.Quotes[0].Close, Math.Max(0, digits - 1));
            var close = open;
            var start = 1;
            for (var i = 1; i < data.Count; i++)
            {
                var q = data.Quotes[i];
                var top = Math.Max(open, close);
                var bottom = Math.Min(open, close);
                var up = (q.High - top) / brick;
                var down = (bottom - q.Low) / brick;
                var qty =
                    highLow ? (int)(up >= down ? up : -down)
                    : q.Close > top ? (int)((q.Close - top) / brick)
                    : q.Close < bottom ? (int)((q.Close - bottom) / brick)
                    : 0;
                if (qty == 0)
                    continue;
                var segment = data.Quotes.Skip(start).Take(i - start + 1).ToArray();
                var h = segment.Max(q => q.High);
                var l = segment.Min(q => q.Low);
                var volume = segment.Sum(q => q.Volume) / Math.Abs(qty);
                for (var j = 0; j < Math.Abs(qty); j++)
                {
                    open = qty > 0 ? Math.Max(open, close) : Math.Min(open, close);
                    close = open + Math.Sign(qty) * brick;
                    yield return new(
                        q.Date,
                        (double)open,
                        (double)h,
                        (double)l,
                        (double)close,
                        (double)volume,
                        qty > 0
                    );
                }
                start = i + 1;
            }
            yield break;
        }
        var spelling = size.ToString("R", CultureInfo.InvariantCulture)
            .ToLowerInvariant()
            .Split('e');
        var fractional = spelling[0].Contains('.') ? spelling[0].Split('.')[1].Length : 0;
        var exponent =
            spelling.Length == 1 ? 0 : int.Parse(spelling[1], CultureInfo.InvariantCulture);
        var precision = Math.Max(0, fractional - exponent - 1);
        var scale = BigInteger.Pow(10, precision);
        var n = Units(data.Closes[0]) * scale;
        var sign = n.Sign;
        var absolute = BigInteger.Abs(n);
        var integer = absolute / Grid;
        var remainder = absolute % Grid;
        if (remainder * 2 > Grid || remainder * 2 == Grid && !integer.IsEven)
            integer++;
        var previousOpen = integer * sign * Grid;
        var previousClose = previousOpen;
        var step = Units(size) * scale;
        var first = 1;
        for (var i = 1; i < data.Count; i++)
        {
            var top = BigInteger.Max(previousOpen, previousClose);
            var bottom = BigInteger.Min(previousOpen, previousClose);
            var upper = Units(data.Highs[i]) * scale - top;
            var lower = bottom - Units(data.Lows[i]) * scale;
            var current = Units(data.Closes[i]) * scale;
            var qty =
                highLow ? (upper >= lower ? upper : -lower) / step
                : current > top ? (current - top) / step
                : current < bottom ? (current - bottom) / step
                : 0;
            if (qty.IsZero)
                continue;
            var segment = data.IndicatorBars.Skip(first).Take(i - first + 1).ToArray();
            var h = segment.Max(b => b.High);
            var l = segment.Min(b => b.Low);
            var volume = Round(
                segment.Aggregate(BigInteger.Zero, (sum, b) => sum + Units(b.Volume)),
                Grid * BigInteger.Abs(qty)
            );
            for (BigInteger j = 0; j < BigInteger.Abs(qty); j++)
            {
                var start =
                    qty.Sign > 0
                        ? BigInteger.Max(previousOpen, previousClose)
                        : BigInteger.Min(previousOpen, previousClose);
                var end = start + qty.Sign * step;
                yield return new(
                    data.IndicatorBars[i].Time,
                    Round(start, Grid * scale),
                    h,
                    l,
                    Round(end, Grid * scale),
                    volume,
                    qty.Sign > 0
                );
                previousOpen = start;
                previousClose = end;
            }
            first = i + 1;
        }
    }
}
