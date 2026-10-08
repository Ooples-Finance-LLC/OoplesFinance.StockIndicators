using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SqueezeMomentumOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return SqueezeMomentumValues(bars, Integer(o, "Length", 20), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) SqueezeMomentumValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values) => RationalAverage(values, length, kind);
        var mean = Mean(bars.Select(b => R(b.Close)).ToArray());
        var residuals = new ReferenceFraction[bars.Count]; var outputs = new double[bars.Count]; var trades = new Signal[bars.Count]; var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - length + 1); var window = bars.Skip(start).Take(i - start + 1).ToArray();
            residuals[i] = R(bars[i].Close) - ((R(window.Max(b => b.High)) + R(window.Min(b => b.Low))) / R(2) + mean[i]) / R(2);
            var n = i - start + 1; var center = R(n - 1) / R(2); var sum = zero; var covariance = zero; var variance = zero;
            for (var j = start; j <= i; j++)
            { var x = R(j - start) - center; sum += residuals[j]; covariance += x * residuals[j]; variance += x * x; }
            var endpoint = sum / R(n) + (variance.Sign == 0 ? zero : covariance / variance * center);
            outputs[i] = endpoint.ToDouble(); var change = endpoint.CompareTo(previous);
            trades[i] = endpoint.Sign > 0 && change > 0 ? Signal.StrongBuy : endpoint.Sign < 0 && change < 0 ? Signal.StrongSell : endpoint.Sign > 0 ? Signal.Buy : endpoint.Sign < 0 ? Signal.Sell : Signal.None;
            previous = endpoint;
        }
        return (new() { ["Smi"] = outputs }, trades);
    }
}
