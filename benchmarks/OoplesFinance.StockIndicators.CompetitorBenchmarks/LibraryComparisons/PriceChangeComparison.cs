using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PriceChangeComparison
{
    internal static readonly (string Name, PriceChangeKind Kind)[] Forms =
    [
        ("Mom", PriceChangeKind.Difference),
        ("RocP", PriceChangeKind.Fraction),
        ("Roc", PriceChangeKind.Percent),
        ("RocR", PriceChangeKind.Ratio),
        ("RocR100", PriceChangeKind.RatioPercent),
    ];
    internal static readonly ComparisonPair[] Pairs = Forms
        .Select(form => new ComparisonPair(
            "TaLib.Functions." + form.Name,
            "LaggedPriceChange(" + form.Kind + ")",
            (data, period) => Competitor(form.Kind, data, period),
            (data, period) => Ooples(form.Kind, data, period),
            (data, period) => Reference(form.Kind, data, period)
        ))
        .ToArray();

    private static ComparisonSeries Ooples(PriceChangeKind kind, CompetitorData data, int period)
    {
        var indicator = new LaggedPriceChange(period, kind);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(period, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(
        PriceChangeKind kind,
        CompetitorData data,
        int period
    )
    {
        var packed = new double[data.Count];
        System.Range range;
        var code = kind switch
        {
            PriceChangeKind.Difference => Functions.Mom<double>(
                data.Closes,
                System.Range.All,
                packed,
                out range,
                period
            ),
            PriceChangeKind.Fraction => Functions.RocP<double>(
                data.Closes,
                System.Range.All,
                packed,
                out range,
                period
            ),
            PriceChangeKind.Percent => Functions.Roc<double>(
                data.Closes,
                System.Range.All,
                packed,
                out range,
                period
            ),
            PriceChangeKind.Ratio => Functions.RocR<double>(
                data.Closes,
                System.Range.All,
                packed,
                out range,
                period
            ),
            _ => Functions.RocR100<double>(
                data.Closes,
                System.Range.All,
                packed,
                out range,
                period
            ),
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(period, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected price-change alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(PriceChangeKind kind, CompetitorData data, int period)
    {
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        for (var i = period; i < data.Count; i++)
        {
            var current = (decimal)data.Closes[i];
            var previous = (decimal)data.Closes[i - period];
            var value = current - previous;
            if (kind != PriceChangeKind.Difference)
            {
                value = previous == 0 ? 0 : current / previous;
                if (previous != 0 && kind is PriceChangeKind.Fraction or PriceChangeKind.Percent)
                    value -= 1;
                if (kind is PriceChangeKind.Percent or PriceChangeKind.RatioPercent)
                    value *= 100;
            }
            values[i] = (double)value;
        }
        return new(Math.Min(period, data.Count), values);
    }
}
