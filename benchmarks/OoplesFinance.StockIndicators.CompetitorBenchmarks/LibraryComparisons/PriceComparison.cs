using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PriceComparison
{
    internal static readonly string[] Transforms = ["AvgPrice", "MedPrice", "TypPrice", "WclPrice"];
    internal static readonly ComparisonPair[] Pairs = [
        .. Transforms.Select(name => new ComparisonPair("TaLib.Functions." + name, IndicatorName(name),
            (data, _) => TaLib(name, data), (data, _) => Ooples(name, data), (data, _) => Reference(name, data), MinimumInputCount: 2)),
        new("TaLib.Functions.TRange", "TrueRange", (data, _) => TaLib("TRange", data),
            (data, _) => Ooples("TRange", data), (data, _) => Reference("TRange", data)),
        new("Skender.GetTr", "TrueRange", (data, _) => new(Math.Min(1, data.Count), data.Quotes.GetTr().Select(r => r.Tr ?? double.NaN).ToArray()),
            (data, _) => Ooples("TRange", data), (data, _) => Reference("TRange", data)),
        new("Trady.Indicator.TrueRange", "TrueRange", (data, _) => new(Math.Min(1, data.Count), new Trady.Analysis.Indicator.TrueRange(data.Candles)
            .Compute().Select(r => (double?)r.Tick ?? double.NaN).ToArray()),
            (data, _) => Ooples("TRange", data), (data, _) => Reference("TRange", data))
    ];
    private static string IndicatorName(string name) => name switch
    {
        "AvgPrice" => "FullTypicalPrice", "MedPrice" => "MedianPrice", "TypPrice" => "TypicalPrice", "WclPrice" => "WeightedClose", _ => "TrueRange"
    };
    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        IIndicator indicator = name switch
        {
            "AvgPrice" => new FullTypicalPrice(1), "MedPrice" => new MedianPrice(1), "TypPrice" => new TypicalPrice(1),
            "WclPrice" => new WeightedClose(1), _ => new TrueRange(1)
        };
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        // Ooples also publishes the first candle's range. These three competitor contracts
        // start at the second candle because a previous close is required.
        return new(name == "TRange" ? Math.Min(1, data.Count) : 0, run[indicator.Outputs[0]].ToArray());
    }
    private static ComparisonSeries TaLib(string name, CompetitorData data)
    {
        var packed = new double[data.Count];
        System.Range range;
        var code = name switch
        {
            "AvgPrice" => Functions.AvgPrice<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "MedPrice" => Functions.MedPrice<double>(data.Highs, data.Lows, System.Range.All, packed, out range),
            "TypPrice" => Functions.TypPrice<double>(data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "WclPrice" => Functions.WclPrice<double>(data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            _ => Functions.TRange<double>(data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range)
        };
        var first = name == "TRange" ? Math.Min(1, data.Count) : 0;
        if (name == "TRange" && data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam) return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (start != first || count != data.Count - first) throw new InvalidOperationException("Unexpected price output alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }
    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var first = name == "TRange" ? Math.Min(1, data.Count) : 0;
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        for (var i = first; i < data.Count; i++)
        {
            var o = (decimal)data.Opens[i]; var h = (decimal)data.Highs[i];
            var l = (decimal)data.Lows[i]; var c = (decimal)data.Closes[i];
            values[i] = (double)(name switch
            {
                "AvgPrice" => (o + h + l + c) / 4,
                "MedPrice" => (h + l) / 2,
                "TypPrice" => (h + l + c) / 3,
                "WclPrice" => (h + l + 2*c) / 4,
                _ => Math.Max(h - l, Math.Max(Math.Abs(h - (decimal)data.Closes[i-1]), Math.Abs(l - (decimal)data.Closes[i-1])))
            });
        }
        return new(first, values);
    }
    internal static CompetitorData Fixture() => CompetitorData.FromOhlc(
        [4, 20, -8, 3, 6], [10, 25, -2, 10, 6], [0, 18, -12, -5, 6], [6, 22, -4, 7, 6]);
}
