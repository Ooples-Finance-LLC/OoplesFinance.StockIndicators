using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TrendStarComparison
{
    internal static readonly string[] Names =
    [
        "MorningStar",
        "EveningStar",
        "MorningDojiStar",
        "EveningDojiStar",
        "MoringinDojiStar",
    ];
    internal static readonly ComparisonPair[] Pairs = Names.Select(n => Pair(n)).ToArray();

    internal static TrendStarKind Kind(string name) =>
        name switch
        {
            "MorningStar" => TrendStarKind.Morning,
            "EveningStar" => TrendStarKind.Evening,
            "MorningDojiStar" or "MoringinDojiStar" => TrendStarKind.MorningDoji,
            "EveningDojiStar" => TrendStarKind.EveningDoji,
            _ => throw new ArgumentException("Unknown star", nameof(name)),
        };

    internal static TrendStarPattern Create(
        string name,
        int trend = 3,
        int period = 20,
        decimal shortQ = .25m,
        decimal longQ = .75m,
        decimal doji = .25m,
        decimal tolerance = .1m
    ) => new(Kind(name), trend, period, shortQ, longQ, doji, tolerance);

    internal static ComparisonPair Pair(
        string name,
        int period = 20,
        decimal shortQ = .25m,
        decimal longQ = .75m,
        decimal doji = .25m,
        decimal tolerance = .1m
    ) =>
        new(
            "Trady.Candlestick." + name,
            "TrendStarPattern(" + Kind(name) + ")",
            (data, trend) => Competitor(name, data, trend, period, shortQ, longQ, doji, tolerance),
            (data, trend) =>
                CandleComparisonExecution.Run(
                    Create(name, trend, period, shortQ, longQ, doji, tolerance),
                    data,
                    Math.Min(2, data.Count)
                ),
            (data, trend) =>
                Reference(name, data, trend, period, shortQ, longQ, doji, tolerance, false),
            CompetitorReference: (data, trend) =>
                Reference(name, data, trend, period, shortQ, longQ, doji, tolerance, true)
        );

    private static ComparisonSeries Competitor(
        string name,
        CompetitorData data,
        int trend,
        int period,
        decimal shortQ,
        decimal longQ,
        decimal doji,
        decimal tolerance
    )
    {
        IEnumerable<bool?> values = name switch
        {
            "MorningStar" => new TC.MorningStar(
                data.Candles,
                trend,
                period,
                shortQ,
                longQ,
                tolerance
            )
                .Compute()
                .Select(t => t.Tick),
            "EveningStar" => new TC.EveningStar(
                data.Candles,
                trend,
                period,
                shortQ,
                longQ,
                tolerance
            )
                .Compute()
                .Select(t => t.Tick),
            "MorningDojiStar" => new TC.MorningDojiStar(
                data.Candles,
                trend,
                period,
                longQ,
                doji,
                tolerance
            )
                .Compute()
                .Select(t => t.Tick),
            "EveningDojiStar" => new TC.EveningDojiStar(
                data.Candles,
                trend,
                period,
                longQ,
                doji,
                tolerance
            )
                .Compute()
                .Select(t => t.Tick),
            "MoringinDojiStar" => new TC.MoringinDojiStarByTuple(
                data.Candles.Select(b => (b.Open, b.High, b.Low, b.Close)),
                trend,
                period,
                longQ,
                doji,
                tolerance
            ).Compute(),
            _ => throw new ArgumentException("Unknown star", nameof(name)),
        };
        return new(
            Math.Min(2, data.Count),
            values
                .Select(v =>
                    v.HasValue
                        ? v.Value
                            ? 1d
                            : 0
                        : double.NaN
                )
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(
        string name,
        CompetitorData data,
        int trend,
        int period,
        decimal shortQ,
        decimal longQ,
        decimal doji,
        decimal tolerance,
        bool native
    )
    {
        var morning = Kind(name) is TrendStarKind.Morning or TrendStarKind.MorningDoji;
        var dojiKind = Kind(name) is TrendStarKind.MorningDoji or TrendStarKind.EveningDoji;
        var o = data.Opens.Select(v => (decimal)v).ToArray();
        var c = data.Closes.Select(v => (decimal)v).ToArray();
        var h = data.Highs.Select(v => (decimal)v).ToArray();
        var l = data.Lows.Select(v => (decimal)v).ToArray();
        var lengths = o.Select((v, i) => Math.Abs(v - c[i])).ToArray();
        bool Body(int i, int color)
        {
            var p = color < 0 ? 20 : period;
            var q =
                color < 0 ? .75m
                : color > 0 ? longQ
                : shortQ;
            if (i < p - 1)
                return false;
            var w = lengths.Skip(i - p + 1).Take(p).Order().ToArray();
            var rank = q * (p - 1);
            var lo = (int)rank;
            var threshold = w[lo];
            if (lo + 1 < p)
                threshold += (w[lo + 1] - threshold) * (rank - lo);
            return color == 0
                ? lengths[i] < threshold
                : lengths[i] >= threshold && Math.Sign(c[i] - o[i]) == color;
        }
        var result = new double[data.Count];
        for (var i = 2; i < data.Count; i++)
        {
            if (i - 1 < trend)
                continue;
            var trending = Enumerable
                .Range(i - trend, trend)
                .All(j =>
                    morning
                        ? h[j] < h[j - 1] && l[j] < l[j - 1]
                        : h[j] > h[j - 1] && l[j] > l[j - 1]
                );
            var middle = dojiKind ? (o[i - 1] + c[i - 1]) / 2 : c[i - 1];
            var small = dojiKind ? lengths[i - 1] < doji * (h[i - 1] - l[i - 1]) : Body(i - 1, 0);
            var structure = morning
                ? middle < c[i - 2] && o[i] > Math.Max(o[i - 1], c[i - 1])
                : middle > c[i - 2] && o[i] < Math.Min(o[i - 1], c[i - 1]);
            if (
                !trending
                || !small
                || !structure
                || !Body(i - 2, morning ? -1 : 1)
                || !Body(i, morning ? 1 : -1)
            )
                continue;
            result[i] = Near(
                data.Opens[i - 2],
                data.Closes[i - 2],
                data.Closes[i],
                tolerance,
                native
            )
                ? 1
                : 0;
        }
        return new(Math.Min(2, data.Count), result);
    }

    internal static bool Near(
        double open,
        double close,
        double finalClose,
        decimal tolerance,
        bool native
    )
    {
        if (native)
        {
            var midpoint = ((decimal)open + (decimal)close) / 2;
            return Math.Abs(((decimal)finalClose - midpoint) / midpoint) < tolerance;
        }
        var (n, d) = Fraction(tolerance);
        var sum =
            PenetrationReferenceArithmetic.Units(open)
            + PenetrationReferenceArithmetic.Units(close);
        return BigInteger.Abs(2 * PenetrationReferenceArithmetic.Units(finalClose) - sum) * d
            < BigInteger.Abs(sum) * n;
    }

    private static (BigInteger N, BigInteger D) Fraction(decimal value)
    {
        var s = value.ToString(CultureInfo.InvariantCulture);
        var point = s.IndexOf('.');
        return (
            BigInteger.Parse(s.Replace(".", ""), CultureInfo.InvariantCulture),
            BigInteger.Pow(10, point < 0 ? 0 : s.Length - point - 1)
        );
    }

    internal sealed record Case(
        string Name,
        double Plain,
        double Doji,
        (double O, double H, double L, double C) First,
        (double O, double H, double L, double C) Middle,
        (double O, double H, double L, double C) Last
    );

    private static readonly (double O, double H, double L, double C) Anchor = (6, 6.5, 3.75, 4),
        Middle = (3, 3.25, 2.75, 3),
        Final = (3.5, 5.25, 3.375, 5);
    internal static readonly Case[] Cases =
    [
        new("doji middle uses midpoint", 0, 1, Anchor, (3, 6, -2, 4), (4.25, 5.25, 4.125, 5)),
        new("signal", 1, 1, Anchor, Middle, Final),
        Last("upper midpoint boundary", 0, (3.5, 5.75, 3.375, 5.5)),
        Last("lower midpoint boundary", 0, (3.5, 4.75, 3.375, 4.5)),
        Last("inside upper boundary", 1, (3.5, 5.75, 3.375, 5.375)),
        Last("inside lower boundary", 1, (3.5, 4.75, 3.375, 4.625)),
        Last("final open equality", 0, (3, 5.25, 2.875, 5)),
        new("middle position equality", 0, 0, Anchor, (4, 4.25, 3.5, 4), (4.5, 5.25, 4.375, 5)),
        new("zero range middle", 1, 0, Anchor, (3, 3, 3, 3), Final),
        new("wrong first color", 0, 0, (4, 6.5, 3.75, 6), Middle, Final),
        Last("wrong last color", 0, (5.125, 5.25, 3.375, 5)),
        new("middle body at doji threshold", 0, 0, Anchor, (3, 3.75, 2.75, 3.25), Final),
        new("middle body below both thresholds", 1, 1, Anchor, (3, 3.75, 2.75, 3.125), Final),
    ];

    private static Case Last(
        string name,
        double expected,
        (double O, double H, double L, double C) candle
    ) => new(name, expected, expected, Anchor, Middle, candle);

    internal static CompetitorData Golden(
        Case item,
        string name,
        int trend = 3,
        int period = 20,
        double shift = 0
    )
    {
        var bars = new List<(double O, double H, double L, double C)>();
        var count = Math.Max(Math.Max(period, 20), trend);
        for (var i = count - 1; i >= 0; i--)
            bars.Add((6.75 + .5 * i, 7 + .5 * i, 6.25 + .5 * i, 6.5 + .5 * i));
        bars.Add(item.First);
        bars.Add(item.Middle);
        bars.Add(item.Last);
        bars = bars.Select(b => (b.O + shift, b.H + shift, b.L + shift, b.C + shift)).ToList();
        if (Kind(name) is TrendStarKind.Evening or TrendStarKind.EveningDoji)
            bars = bars.Select(b => (-b.O, -b.L, -b.H, -b.C)).ToList();
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
    }

    internal static CompetitorData Fixture(int trend)
    {
        var bars = Cases
            .SelectMany(c =>
                new[] { "MorningStar", "EveningStar" }.SelectMany(n =>
                    Golden(c, n, trend).IndicatorBars
                )
            )
            .ToArray();
        return CrowSoldierComparison.FromBars(bars);
    }
}
