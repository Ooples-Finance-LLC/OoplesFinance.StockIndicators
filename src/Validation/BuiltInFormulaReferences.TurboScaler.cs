using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TurboScalerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return TurboScalerValues(bars, Integer(o, "Length", 50), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!, 0.5).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) TurboScalerValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind, double alpha)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, kind);
        ReferenceFraction[] Position(ReferenceFraction[] input, ReferenceFraction[] mean)
        {
            var blend = input.Select((v, i) => R(alpha) * v + (R(1) - R(alpha)) * mean[i]).ToArray(); var result = new ReferenceFraction[input.Length];
            for (var i = 0; i < input.Length; i++)
            {
                var low = blend[i]; var high = blend[i];
                for (var j = Math.Max(0, i - length + 1); j < i; j++) { if (blend[j].CompareTo(low) < 0) low = blend[j]; if (blend[j].CompareTo(high) > 0) high = blend[j]; }
                result[i] = high.CompareTo(low) == 0 ? zero : (input[i] - low) / (high - low);
            }
            return result;
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var first = Mean(prices, length); var second = Mean(first, length);
        var line = Position(prices, first); var trigger = Position(first, second); var lineMean = Mean(line, length); var triggerMean = Mean(trigger, length); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var comparison = lineMean[i] - triggerMean[i]; var previous = i == 0 ? zero : lineMean[i - 1] - triggerMean[i - 1];
            signals[i] = comparison.Sign > 0 && comparison.CompareTo(previous) > 0 ? Signal.StrongBuy : comparison.Sign < 0 && comparison.CompareTo(previous) < 0 ? Signal.StrongSell
                : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Ts"] = line.Select(v => v.ToDouble()).ToArray(), ["Trigger"] = trigger.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
