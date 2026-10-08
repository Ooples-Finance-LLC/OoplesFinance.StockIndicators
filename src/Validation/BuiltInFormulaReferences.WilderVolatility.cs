using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WilderVolatilityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return WilderVolatilityValues(bars, Integer(options, "Length1", 63), Integer(options, "Length2", 21), AverageKind(options, 3), Number(options, 3, "Factor")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) WilderVolatilityValues(IReadOnlyList<Bar> bars, int trendLength, int rangeLength, int kind, double factor)
    {
        trendLength = Math.Max(1, trendLength); rangeLength = Math.Max(1, rangeLength); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var ranges = bars.Select((b, i) => new[] { R(b.High) - R(b.Low), (R(b.High) - prices[i == 0 ? 0 : i - 1]).Abs(), (R(b.Low) - prices[i == 0 ? 0 : i - 1]).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var atr = SmoothRocBankStage(ranges, rangeLength, kind); var trend = SmoothRocBankStage(prices, trendLength, kind); var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var up = prices[i].CompareTo(trend[i]) > 0; var retained = Window(bars, i, Math.Max(2, rangeLength)); var extreme = up ? retained.Max(b => b.Close) : retained.Min(b => b.Close);
            var stop = (R(extreme) + R(up ? -1 : 1) * R(factor) * atr[i]).RoundExtendedBinary64(); values[i] = stop.ToDouble(); var difference = prices[i] - stop;
            signals[i] = difference.Sign > 0 && difference.CompareTo(previous) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(previous) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; previous = difference;
        }
        return (new Dictionary<string, double[]> { { "Wwvs", values } }, signals);
    }
}
