using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExtrapolatedAverageComparison
{
    internal static readonly ComparisonPair[] Pairs = new[] { 2, 3 }
        .SelectMany(order =>
            new[] { false, true }.Select(ta => new ComparisonPair(
                (ta ? "TaLib.Functions." : "Skender.Get") + (order == 2 ? "Dema" : "Tema"),
                "SeededExponentialAverage(" + order + ", " + (ta ? "Cascaded" : "Shared") + ")",
                (data, period) => Competitor(data, period, order, ta),
                (data, period) => Ooples(data, period, order, ta),
                (data, period) => Reference(data, period, order, ta)
            ))
        )
        .ToArray();

    private static int First(int period, int order, bool cascaded, int count) =>
        (int)Math.Min(count, (long)(period - 1) * (cascaded ? order : 1));

    private static ComparisonSeries Ooples(
        CompetitorData data,
        int period,
        int order,
        bool cascaded
    )
    {
        var indicator = new SeededExponentialAverage(
            period,
            order,
            cascaded ? ExponentialSeedMode.Cascaded : ExponentialSeedMode.Shared
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(First(period, order, cascaded, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(CompetitorData data, int period, int order, bool ta)
    {
        var first = First(period, order, ta, data.Count);
        if (!ta)
            return new(
                first,
                order == 2
                    ? data.Quotes.GetDema(period).Select(r => r.Dema ?? double.NaN).ToArray()
                    : data.Quotes.GetTema(period).Select(r => r.Tema ?? double.NaN).ToArray()
            );
        var packed = new double[data.Count];
        System.Range range;
        var code =
            order == 2
                ? Functions.Dema<double>(data.Closes, System.Range.All, packed, out range, period)
                : Functions.Tema<double>(data.Closes, System.Range.All, packed, out range, period);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected DEMA/TEMA alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        int order,
        bool cascaded
    )
    {
        var layers = new decimal[order][];
        for (var stage = 0; stage < order; stage++)
        {
            layers[stage] = new decimal[data.Count];
            var first = First(period, stage + 1, cascaded, data.Count);
            if (first == data.Count)
                continue;
            decimal sum = 0;
            for (var j = first - period + 1; j <= first; j++)
                sum += stage == 0 || !cascaded ? (decimal)data.Closes[j] : layers[stage - 1][j];
            layers[stage][first] = sum / period;
            for (var i = first + 1; i < data.Count; i++)
                layers[stage][i] =
                    (
                        2 * (stage == 0 ? (decimal)data.Closes[i] : layers[stage - 1][i])
                        + (period - 1m) * layers[stage][i - 1]
                    ) / (period + 1m);
        }
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var start = First(period, order, cascaded, data.Count);
        for (var i = start; i < data.Count; i++)
            values[i] = (double)(
                order == 2
                    ? 2 * layers[0][i] - layers[1][i]
                    : 3 * (layers[0][i] - layers[1][i]) + layers[2][i]
            );
        return new(start, values);
    }
}
