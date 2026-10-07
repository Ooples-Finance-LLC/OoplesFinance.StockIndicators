using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class NormalizedAtrComparison
{
    internal static readonly ComparisonPair Pair = new(
        "TaLib.Functions.Natr",
        "NormalizedSeededAverageTrueRange",
        Competitor,
        Ooples,
        (d, p) => Reference(d, p, false),
        CompetitorReference: (d, p) => Reference(d, p, true)
    );

    private static ComparisonSeries Ooples(CompetitorData data, int period)
    {
        var indicator = new NormalizedSeededAverageTrueRange(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(period, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(CompetitorData data, int period)
    {
        // Zero initialization is explicit: the pinned package fails to write the current
        // slot when a later close is zero. A sentinel regression documents that defect.
        var packed = new double[data.Count];
        var code = Functions.Natr<double>(
            data.Highs,
            data.Lows,
            data.Closes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        var first = Math.Min(period, data.Count);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(first, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib NATR returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected normalized ATR alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool native)
    {
        var ordinary = SeededAtrComparison.Pairs.Single(p => p.Id == "TaLib.Functions.Atr");
        var series = (native ? ordinary.CompetitorReference! : ordinary.Reference!)(data, period);
        if (native && period == 1)
            return series;
        var values = new double[data.Count];
        for (var i = period; i < data.Count; i++)
        {
            var close = data.Closes[i];
            if (close == 0)
            {
                if (native)
                    values[period] = 0;
                continue;
            }
            var average = series.Outputs["Value"].Values[i];
            values[i] = native
                ? Multiply(Divide(average, close), 100)
                : Round(Units(average) * 100, Units(close));
        }
        return new(Math.Min(period, data.Count), values);
    }
}
