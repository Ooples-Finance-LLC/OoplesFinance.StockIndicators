using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveRsiV1Values(IReadOnlyList<Bar> bars, double fraction = .5, bool fisher = false)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var periods = MamaValues(bars.Select(b => b.Close).ToArray()).Outputs["SmoothPeriod"]; var values = new double[bars.Count]; var averages = new double[bars.Count]; var signals = new Signal[bars.Count];
        double Prior(double[] array, int i) => i < 0 ? 0 : array[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var desired = Math.Ceiling(fraction * periods[i]); var count = desired <= 0 ? 0 : desired >= int.MaxValue - 1d ? int.MaxValue - 1 : (int)desired; var gains = R(0); var losses = R(0);
            for (var j = Math.Max(0, i - count + 1); j <= i && count != 0; j++) { var change = R(bars[j].Close) - R(j == 0 ? 0 : bars[j - 1].Close); if (change.Sign > 0) gains += change; else losses -= change; }
            var travel = gains + losses; var rsi = travel.Sign == 0 ? 0 : (R(100) * gains / travel).ToDouble(); var alpha = Math.Max(.01, Math.Min(.99, 2 / (Math.Ceiling(periods[i]) + 1))); averages[i] = (R(alpha) * R(rsi) + R(1 - alpha) * R(Prior(averages, i - 1))).ToDouble();
            var argument = Math.Max(-.999, Math.Min(.999, 1.5 * (2 * (rsi / 100 - .5)))); values[i] = fisher ? .5 * Math.Log((1 + argument) / (1 - argument)) : rsi;
            var current = fisher ? values[i] : averages[i]; var previous = Prior(fisher ? values : averages, i - 1); var older = Prior(fisher ? values : averages, i - 2); var slope = current - previous; var priorSlope = previous - older;
            signals[i] = slope > 0 && slope > priorSlope ? Signal.StrongBuy : slope < 0 && slope < priorSlope ? Signal.StrongSell : slope > 0 || (!fisher && previous < 30 && current > 30) ? Signal.Buy : slope < 0 || (!fisher && previous > 70 && current < 70) ? Signal.Sell : Signal.None;
        }
        return (fisher ? new Dictionary<string, double[]> { { "Earsift", values } } : new Dictionary<string, double[]> { { "Earsi", values }, { "Signal", averages } }, signals);
    }
}
