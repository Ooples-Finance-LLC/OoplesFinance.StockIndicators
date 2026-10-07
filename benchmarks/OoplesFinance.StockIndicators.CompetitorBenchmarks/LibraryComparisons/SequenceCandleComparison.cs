using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SequenceCandleComparison
{
    internal static readonly string[] Names =
    [
        "MatchingLow",
        "StickSandwich",
        "ThreeLineStrike",
        "ThreeOutside",
    ];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(name => new ComparisonPair(
            "TaLib.Candles." + name,
            name + "Candle",
            (data, _) => Competitor(name, data),
            (data, _) => Ooples(name, data),
            (data, _) => Reference(name, data)
        ))
        .ToArray();

    internal static int Lookback(string name) =>
        name switch
        {
            "MatchingLow" => 6,
            "StickSandwich" => 7,
            "ThreeLineStrike" => 8,
            _ => 3,
        };

    internal static IIndicator Create(string name, int period = 5) =>
        name switch
        {
            "MatchingLow" => new MatchingLowCandle(period),
            "StickSandwich" => new StickSandwichCandle(period),
            "ThreeLineStrike" => new ThreeLineStrikeCandle(period),
            "ThreeOutside" => new ThreeOutsideCandle(),
            _ => throw new ArgumentException("Unknown sequence candle", nameof(name)),
        };

    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        var indicator = Create(name);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(Lookback(name), data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data)
    {
        var packed = new int[data.Count];
        System.Range range;
        var code = name switch
        {
            "MatchingLow" => Candles.MatchingLow<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "StickSandwich" => Candles.StickSandwich<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "ThreeLineStrike" => Candles.ThreeLineStrike<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "ThreeOutside" => Candles.ThreeOutside<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => throw new ArgumentException("Unknown sequence candle", nameof(name)),
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(Lookback(name), data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected sequence alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++)
            values[start + i] = packed[i];
        return new(first, values);
    }

    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var result = new double[data.Count];
        var first = Math.Min(Lookback(name), data.Count);
        for (var i = first; i < data.Count; i++)
        {
            decimal O(int j) => (decimal)data.Opens[j];
            decimal C(int j) => (decimal)data.Closes[j];
            bool White(int j) => C(j) >= O(j);
            decimal Tolerance(int end, int divisor)
            {
                decimal sum = 0;
                for (var j = end - 5; j < end; j++)
                    sum += (decimal)data.Highs[j] - (decimal)data.Lows[j];
                return sum / (5 * divisor);
            }
            bool CloseTo(int prior) => Math.Abs(C(i) - C(prior)) <= Tolerance(prior, 20);
            bool Inside(int current, int prior) =>
                O(current) >= Math.Min(O(prior), C(prior)) - Tolerance(prior, 5)
                && O(current) <= Math.Max(O(prior), C(prior)) + Tolerance(prior, 5);
            if (name == "MatchingLow")
                result[i] = !White(i - 1) && !White(i) && CloseTo(i - 1) ? 100 : 0;
            else if (name == "StickSandwich")
                result[i] =
                    !White(i - 2)
                    && White(i - 1)
                    && !White(i)
                    && (decimal)data.Lows[i - 1] > C(i - 2)
                    && CloseTo(i - 2)
                        ? 100
                        : 0;
            else if (name == "ThreeOutside")
            {
                if (
                    !White(i - 2)
                    && White(i - 1)
                    && O(i - 1) < C(i - 2)
                    && C(i - 1) > O(i - 2)
                    && C(i) > C(i - 1)
                )
                    result[i] = 100;
                if (
                    White(i - 2)
                    && !White(i - 1)
                    && O(i - 1) > C(i - 2)
                    && C(i - 1) < O(i - 2)
                    && C(i) < C(i - 1)
                )
                    result[i] = -100;
            }
            else if (
                White(i - 3) == White(i - 2)
                && White(i - 2) == White(i - 1)
                && White(i) != White(i - 1)
                && Inside(i - 2, i - 3)
                && Inside(i - 1, i - 2)
            )
            {
                if (
                    White(i - 1)
                    && C(i - 1) > C(i - 2)
                    && C(i - 2) > C(i - 3)
                    && O(i) > C(i - 1)
                    && C(i) < O(i - 3)
                )
                    result[i] = 100;
                if (
                    !White(i - 1)
                    && C(i - 1) < C(i - 2)
                    && C(i - 2) < C(i - 3)
                    && O(i) < C(i - 1)
                    && C(i) > O(i - 3)
                )
                    result[i] = -100;
            }
        }
        return new(first, result);
    }

    internal sealed record GoldenCase(string Name, CompetitorData Data, double Expected);

    internal static readonly GoldenCase[] GoldenCases = BuildGolden().ToArray();

    private static IEnumerable<GoldenCase> BuildGolden()
    {
        foreach (var close in new[] { 3.375, 3.5, 4, 4.5, 4.625 })
            yield return Make(
                "MatchingLow",
                [(8, 10, 0, 4), (8, 10, 0, close)],
                close >= 3.5 && close <= 4.5 ? 100 : 0
            );
        yield return Make("MatchingLow", [(4, 10, 0, 8), (8, 10, 0, 8)], 0);
        foreach (var close in new[] { 3.375, 3.5, 4, 4.5, 4.625 })
            yield return Make(
                "StickSandwich",
                [(8, 10, 0, 4), (6, 10, 5, 8), (8, 10, 0, close)],
                close >= 3.5 && close <= 4.5 ? 100 : 0
            );
        yield return Make("StickSandwich", [(8, 10, 0, 4), (6, 10, 4, 8), (8, 10, 0, 4)], 0);
        yield return Make("StickSandwich", [(8, 10, 0, 4), (6, 10, 5, 6), (8, 10, 0, 4)], 100);
        yield return Make("ThreeOutside", [(8, 10, 0, 4), (3, 10, 0, 9), (11, 12, 0, 10)], 100);
        yield return Make("ThreeOutside", [(8, 10, 0, 4), (3, 10, 0, 9), (11, 12, 0, 9)], 0);
        yield return Make("ThreeOutside", [(8, 10, 0, 4), (4, 10, 0, 9), (11, 12, 0, 10)], 0);
        yield return Make("ThreeOutside", [(4, 10, 0, 8), (9, 10, 0, 3), (1, 10, 0, 2)], -100);
        yield return Make("ThreeOutside", [(4, 10, 0, 4), (5, 10, 0, 3), (1, 10, 0, 2)], -100);
        foreach (var secondOpen in new[] { -0.125, 0, 3 })
            yield return Make(
                "ThreeLineStrike",
                [
                    (2, 10, 0, 4),
                    (secondOpen, 10, Math.Min(0, secondOpen), 6),
                    (5, 12, 0, 10),
                    (11, 12, 0, 1),
                ],
                secondOpen >= 0 ? 100 : 0
            );
        foreach (var (thirdOpen, expected) in new[] { (8d, 100), (8.125, 0) })
            yield return Make(
                "ThreeLineStrike",
                [(2, 10, 0, 4), (3, 10, 0, 6), (thirdOpen, 12, 0, 10), (11, 12, 0, 1)],
                expected
            );
        yield return Make(
            "ThreeLineStrike",
            [(2, 10, 0, 4), (3, 10, 0, 6), (5, 12, 0, 10), (10, 12, 0, 1)],
            0
        );
        yield return Make(
            "ThreeLineStrike",
            [(2, 10, 0, 4), (3, 10, 0, 6), (5, 12, 0, 10), (11, 12, 0, 2)],
            0
        );
        yield return Make(
            "ThreeLineStrike",
            [(-2, 0, -10, -4), (-3, 0, -10, -6), (-5, 0, -12, -10), (-11, 0, -12, -1)],
            -100
        );
    }

    private static GoldenCase Make(
        string name,
        (double O, double H, double L, double C)[] tail,
        double expected
    )
    {
        var bars = Enumerable.Repeat((O: 4d, H: 10d, L: 0d, C: 6d), 5).Concat(tail).ToArray();
        return new(
            name,
            CompetitorData.FromOhlc(
                bars.Select(b => b.O).ToArray(),
                bars.Select(b => b.H).ToArray(),
                bars.Select(b => b.L).ToArray(),
                bars.Select(b => b.C).ToArray()
            ),
            expected
        );
    }

    internal static CompetitorData Fixture()
    {
        var bars = GoldenCases.SelectMany(c => c.Data.IndicatorBars).ToArray();
        return CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
    }
}
