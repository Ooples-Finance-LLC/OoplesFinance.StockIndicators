using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class KickingRickshawComparison
{
    internal static readonly string[] Names = ["Kicking", "KickingByLength", "RickshawMan"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n + "Candle",
            (d, _) => Competitor(n, d),
            (d, _) => Ooples(n, d),
            (d, _) => Reference(n, d)
        ))
        .ToArray();

    internal static IIndicator Create(string name, int firstPeriod = 10, int secondPeriod = 10) =>
        name switch
        {
            "Kicking" => new KickingCandle(firstPeriod, secondPeriod),
            "KickingByLength" => new KickingByLengthCandle(firstPeriod, secondPeriod),
            _ => new RickshawManCandle(firstPeriod, secondPeriod),
        };

    private static int First(string name, int count) =>
        Math.Min(name == "RickshawMan" ? 10 : 11, count);

    private static ComparisonSeries Ooples(string name, CompetitorData d)
    {
        return CandleComparisonExecution.Run(
            Create(name, 10, name == "RickshawMan" ? 5 : 10),
            d,
            First(name, d.Count)
        );
    }

    private static ComparisonSeries Competitor(string name, CompetitorData d)
    {
        var packed = new int[d.Count];
        System.Range range;
        var code = name switch
        {
            "Kicking" => Candles.Kicking<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "KickingByLength" => Candles.KickingByLength<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.RickshawMan<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
        };
        return CandleComparisonExecution.Unpack(
            code,
            packed,
            range,
            d.Count,
            First(name, d.Count),
            name
        );
    }

    private static ComparisonSeries Reference(string name, CompetitorData d)
    {
        var values = new double[d.Count];
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Range(int i) => (decimal)d.Highs[i] - (decimal)d.Lows[i];
        decimal Sum(int end, int count, Func<int, decimal> term) =>
            Enumerable.Range(end - count, count).Sum(term);
        bool Qualified(int i) =>
            Body(i) > Sum(i, 10, Body) / 10
            && (decimal)d.Highs[i] - (decimal)Math.Max(d.Opens[i], d.Closes[i])
                < Sum(i, 10, Range) / 100
            && (decimal)Math.Min(d.Opens[i], d.Closes[i]) - (decimal)d.Lows[i]
                < Sum(i, 10, Range) / 100;
        for (var i = First(name, d.Count); i < d.Count; i++)
        {
            if (name == "RickshawMan")
            {
                var top = (decimal)Math.Max(d.Opens[i], d.Closes[i]);
                var bottom = (decimal)Math.Min(d.Opens[i], d.Closes[i]);
                var mid = ((decimal)d.Highs[i] + (decimal)d.Lows[i]) / 2;
                var near = Sum(i, 5, Range) / 25;
                if (
                    Body(i) <= Sum(i, 10, Range) / 100
                    && (decimal)d.Highs[i] - top > Body(i)
                    && bottom - (decimal)d.Lows[i] > Body(i)
                    && bottom <= mid + near
                    && top >= mid - near
                )
                    values[i] = 100;
            }
            else if (
                (d.Closes[i] < d.Opens[i]) != (d.Closes[i - 1] < d.Opens[i - 1])
                && (
                    d.Closes[i - 1] < d.Opens[i - 1]
                        ? d.Lows[i] > d.Highs[i - 1]
                        : d.Highs[i] < d.Lows[i - 1]
                )
                && Qualified(i - 1)
                && Qualified(i)
            )
            {
                var chosen = name == "KickingByLength" && Body(i) <= Body(i - 1) ? i - 1 : i;
                values[i] = d.Closes[chosen] >= d.Opens[chosen] ? 100 : -100;
            }
        }
        return new(First(name, d.Count), values);
    }

    internal static readonly (double O, double H, double L, double C)[] KickingCandidates =
    [
        (12, 22, 12, 22),
        (12, 23, 12, 23),
        (12, 21, 12, 21),
        (10, 20, 10, 20),
        (12, 23, 12, 22),
        (12, 22, 11, 22),
        (22, 22, 12, 12),
    ];
    internal static readonly (
        double O,
        double H,
        double L,
        double C,
        double Expected
    )[] RickshawCandidates =
    [
        (2, 10, 0, 3, 100),
        (1.875, 10, 0, 2.875, 0),
        (7, 10, 0, 8, 100),
        (7.125, 10, 0, 8.125, 0),
        (4, 10, 0, 5, 100),
        (4, 10, 0, 5.125, 0),
        (1, 10, 0, 2, 0),
        (8, 10, 0, 9, 0),
        (5, 10, 0, 5, 100),
        (0, 0, 0, 0, 0),
        (3, 10, 0, 3, 100),
        (7, 10, 0, 7, 100),
    ];

    internal static Bar[] KickingBars(int index, bool mirror = false)
    {
        var c = KickingCandidates[index];
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 4, 10, 0, 6))
            .Append(Make(10, 10, 10, 0, 0))
            .Append(Make(11, c.O, c.H, c.L, c.C))
            .ToArray();
        return mirror
            ? bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)).ToArray()
            : bars;
    }

    internal static Bar[] RickshawBars(int index, bool reverse = false)
    {
        var c = RickshawCandidates[index];
        return Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 4, 10, 0, 6))
            .Append(Make(10, reverse ? c.C : c.O, c.H, c.L, reverse ? c.O : c.C))
            .ToArray();
    }

    internal static CompetitorData Fixture()
    {
        var bars = Enumerable
            .Range(0, KickingCandidates.Length)
            .SelectMany(i => KickingBars(i).Concat(KickingBars(i, true)))
            .Concat(
                Enumerable
                    .Range(0, RickshawCandidates.Length)
                    .SelectMany(i => RickshawBars(i).Concat(RickshawBars(i, true)))
            )
            .ToArray();
        return FromBars(bars);
    }

    internal static CompetitorData FromBars(Bar[] bars) =>
        CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );

    private static Bar Make(int i, double o, double h, double l, double c) =>
        new(DateTime.UnixEpoch.AddDays(i), o, h, l, c, 1);
}
