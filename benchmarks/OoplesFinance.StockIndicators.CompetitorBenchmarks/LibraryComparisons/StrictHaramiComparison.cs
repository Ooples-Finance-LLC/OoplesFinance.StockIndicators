using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class StrictHaramiComparison
{
    internal static readonly string[] Names = ["Harami", "BullishHarami", "BearishHarami"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(n => Pair(n)).ToArray();

    internal static IIndicator Create(string name, int period = 3, bool shadows = false) =>
        name switch
        {
            "Harami" => new StrictHaramiCandle(shadows),
            "BullishHarami" => new BullishHaramiPattern(period, shadows),
            "BearishHarami" => new BearishHaramiPattern(period, shadows),
            _ => throw new ArgumentException("Unknown Harami pattern", nameof(name)),
        };

    internal static ComparisonPair Pair(string name, bool shadows = false) =>
        new(
            "Trady.Candlestick." + name,
            Create(name).GetType().Name,
            (data, period) => Competitor(name, data, period, shadows),
            (data, period) => Ooples(name, data, period, name == "Harami" && shadows),
            (data, period) => Reference(name, data, period, name == "Harami" && shadows)
        );

    private static ComparisonSeries Ooples(
        string name,
        CompetitorData data,
        int period,
        bool shadows
    )
    {
        var indicator = Create(name, period, shadows);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(
            name == "Harami" ? Math.Min(1, data.Count) : 0,
            run[indicator.Outputs[0]].ToArray()
        );
    }

    private static ComparisonSeries Competitor(
        string name,
        CompetitorData data,
        int period,
        bool shadows
    )
    {
        IEnumerable<bool?> values = name switch
        {
            "Harami" => new TC.Harami(data.Candles, shadows).Compute().Select(r => r.Tick),
            "BullishHarami" => new TC.BullishHarami(data.Candles, shadows, period)
                .Compute()
                .Select(r => r.Tick),
            "BearishHarami" => new TC.BearishHarami(data.Candles, shadows, period)
                .Compute()
                .Select(r => r.Tick),
            _ => throw new ArgumentException("Unknown Harami pattern", nameof(name)),
        };
        return new(
            name == "Harami" ? Math.Min(1, data.Count) : 0,
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
        int period,
        bool shadows
    )
    {
        var values = new double[data.Count];
        for (var i = 1; i < data.Count; i++)
        {
            var p = i - 1;
            var priorBearish = data.Closes[p] < data.Opens[p];
            var bearish = data.Closes[i] < data.Opens[i];
            var match =
                priorBearish != bearish
                && Math.Min(data.Opens[i], data.Closes[i]) > Math.Min(data.Opens[p], data.Closes[p])
                && Math.Max(data.Opens[i], data.Closes[i])
                    < Math.Max(data.Opens[p], data.Closes[p]);
            if (shadows)
                match &= data.Highs[i] < data.Highs[p] && data.Lows[i] > data.Lows[p];
            if (name != "Harami")
            {
                var bullish = name == "BullishHarami";
                match &= p >= period && (bullish ? data.Closes[i] > data.Opens[i] : bearish);
                if (match)
                    for (var j = p - period + 1; j <= p; j++)
                        match &= bullish
                            ? data.Opens[j] < data.Opens[j - 1]
                                && data.Closes[j] < data.Closes[j - 1]
                            : data.Opens[j] > data.Opens[j - 1]
                                && data.Closes[j] > data.Closes[j - 1];
            }
            values[i] = match ? 1 : 0;
        }
        return new(name == "Harami" ? Math.Min(1, data.Count) : 0, values);
    }

    internal static readonly (double O, double H, double L, double C)[] Candidates =
    [
        (9, 30, -4, 13),
        (8, 30, -4, 13),
        (9, 30, -4, 14),
        (11, 30, -4, 11),
        (13, 30, -4, 9),
        (9, 13.5, 8.5, 13),
    ];

    internal static CompetitorData Fixture(int period = 3)
    {
        var bars = new List<(double O, double H, double L, double C)>();
        foreach (var candidate in Candidates)
        foreach (var mirror in new[] { false, true })
        {
            // Opens/closes trend down while highs rise, preventing accidental use of high/low trends.
            for (var i = 0; i <= period; i++)
            {
                var open = 14d + 2 * (period - i);
                var close = open - 6;
                Add((open, open + 20 + 4 * i, -i, close), mirror);
            }
            Add(candidate, mirror);
        }
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
        void Add((double O, double H, double L, double C) b, bool mirror) =>
            bars.Add(mirror ? (-b.O, -b.L, -b.H, -b.C) : b);
    }
}
