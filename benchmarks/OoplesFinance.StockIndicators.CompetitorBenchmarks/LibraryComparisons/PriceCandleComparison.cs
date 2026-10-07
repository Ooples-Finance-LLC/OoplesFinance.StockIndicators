using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PriceCandleComparison
{
    internal static readonly string[] Outputs =
    [
        "Match",
        "Price",
        "Size",
        "Body",
        "UpperWick",
        "LowerWick",
        "BodyFraction",
        "UpperWickFraction",
        "LowerWickFraction",
        "IsBullish",
        "IsBearish",
    ];
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static ComparisonPair Create(bool marubozu, double? percent = null)
    {
        var threshold = percent ?? (marubozu ? 95 : .1);
        return new(
            marubozu ? "Skender.GetMarubozu" : "Skender.GetDoji",
            "PriceCandlePattern(" + Kind(marubozu) + ")",
            (data, _) => Competitor(data, marubozu, threshold),
            (data, _) => Ooples(data, marubozu, threshold),
            (data, _) => Reference(data, marubozu, threshold, false),
            Outputs,
            CompetitorReference: (data, _) => Reference(data, marubozu, threshold, true)
        );
    }

    internal static PriceCandleKind Kind(bool marubozu) =>
        marubozu ? PriceCandleKind.Marubozu : PriceCandleKind.RelativeDoji;

    private static ComparisonSeries Ooples(CompetitorData data, bool marubozu, double threshold)
    {
        var indicator = new PriceCandlePattern(Kind(marubozu), threshold);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = indicator.Outputs.Select(output => run[output].ToArray()).ToArray();
        var result = new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal);
        for (var slot = 0; slot < Outputs.Length; slot++)
        {
            var output = values[slot < 2 ? slot : slot + 1];
            bool[]? present = slot == 1 ? values[2].Select(x => x > 0).ToArray() : null;
            if (present is not null)
                for (var i = 0; i < output.Length; i++)
                    if (!present[i])
                        output[i] = double.NaN;
            result[Outputs[slot]] = new(0, output, present);
        }
        return new(result);
    }

    private static ComparisonSeries Competitor(CompetitorData data, bool marubozu, double threshold)
    {
        var rows = (
            marubozu ? data.Quotes.GetMarubozu(threshold) : data.Quotes.GetDoji(threshold)
        ).ToArray();
        double[] Values(Func<CandleResult, double> select) => rows.Select(select).ToArray();
        double[][] values =
        [
            Values(r => (double)r.Match),
            Values(r => (double?)r.Price ?? double.NaN),
            Values(r => (double)r.Candle.Size!),
            Values(r => (double)r.Candle.Body!),
            Values(r => (double)r.Candle.UpperWick!),
            Values(r => (double)r.Candle.LowerWick!),
            Values(r => r.Candle.BodyPct!.Value),
            Values(r => r.Candle.UpperWickPct!.Value),
            Values(r => r.Candle.LowerWickPct!.Value),
            Values(r => r.Candle.IsBullish ? 1 : 0),
            Values(r => r.Candle.IsBearish ? 1 : 0),
        ];
        return new(
            Outputs
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot],
                                slot == 1 ? rows.Select(r => r.Price.HasValue).ToArray() : null
                            )
                        )
                )
                .ToDictionary()
        );
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        bool marubozu,
        double threshold,
        bool native
    )
    {
        var columns = Outputs.Select(_ => new double[data.Count]).ToArray();
        var present = new bool[data.Count];
        var scale = BigInteger.One << 1074;
        var percent = PenetrationReferenceArithmetic.Units(threshold);
        for (var i = 0; i < data.Count; i++)
        {
            var o = (decimal)data.Opens[i];
            var c = (decimal)data.Closes[i];
            var h = (decimal)data.Highs[i];
            var l = (decimal)data.Lows[i];
            var size = h - l;
            var body = Math.Abs(c - o);
            var upper = h - Math.Max(o, c);
            var lower = Math.Min(o, c) - l;
            var bodyPct = size == 0 ? 1 : (double)(body / size);
            bool match;
            if (native)
                match = marubozu
                    ? bodyPct >= threshold / 100
                    : o != 0 && Math.Abs((double)(c / o) - 1) <= threshold / 100;
            else
            {
                var ou = PenetrationReferenceArithmetic.Units(data.Opens[i]);
                var cu = PenetrationReferenceArithmetic.Units(data.Closes[i]);
                var range =
                    PenetrationReferenceArithmetic.Units(data.Highs[i])
                    - PenetrationReferenceArithmetic.Units(data.Lows[i]);
                var weightedBody = BigInteger.Abs(cu - ou) * 100 * scale;
                match = marubozu
                    ? range.IsZero || weightedBody >= range * percent
                    : !ou.IsZero && weightedBody <= BigInteger.Abs(ou) * percent;
            }
            columns[0][i] =
                !match ? 0
                : !marubozu ? 1
                : c > o ? 100
                : -100;
            columns[1][i] = match ? data.Closes[i] : double.NaN;
            present[i] = match;
            columns[2][i] = (double)size;
            columns[3][i] = (double)body;
            columns[4][i] = (double)upper;
            columns[5][i] = (double)lower;
            columns[6][i] = bodyPct;
            columns[7][i] = size == 0 ? 1 : (double)(upper / size);
            columns[8][i] = size == 0 ? 1 : (double)(lower / size);
            columns[9][i] = c > o ? 1 : 0;
            columns[10][i] = c < o ? 1 : 0;
        }
        return new(
            Outputs
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(0, columns[slot], slot == 1 ? present : null)
                        )
                )
                .ToDictionary()
        );
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [0, 0, 1000, 1000, -1000, 1000, 1, 19, 0, 20, 5],
            [0, 2, 1002, 1002, -999, 1002, 20, 20, 20, 20, 5],
            [0, 0, 999, 999, -1002, 998, 0, 0, 0, 0, 5],
            [0, 1, 1001, 1000.5, -1001, 999, 20, 0, 20, 0, 5]
        );
}
