using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendAnalysisIndicatorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return TrendAnalysisIndicatorValues(bars, Integer(o, "Length1", 21), Integer(o, "Length2", 4), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) TrendAnalysisIndicatorValues(IReadOnlyList<Bar> bars, int length1, int length2, MovingAvgType kind)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, kind);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var means = Mean(prices, length1); var fast = Mean(prices, length2); var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i + 1 < length2) { line[i] = zero; continue; }
            var center = zero;
            for (var j = i - length2 + 1; j <= i; j++) center += means[j];
            center /= R(length2); var squaredResiduals = zero;
            for (var j = i - length2 + 1; j <= i; j++) { var residual = means[j] - center; squaredResiduals += residual * residual; }
            line[i] = R((squaredResiduals / R(length2)).SqrtToDouble());
        }
        var threshold = Mean(line, length1); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var slope = fast[i] - means[i]; var previous = i == 0 ? zero : fast[i - 1] - means[i - 1];
            signals[i] = line[i].CompareTo(threshold[i]) < 0 ? Signal.None
                : slope.Sign > 0 && slope.CompareTo(previous) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previous) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Tai"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = threshold.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
