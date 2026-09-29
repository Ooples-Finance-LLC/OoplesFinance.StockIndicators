using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BryantOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return BryantValues(bars, Integer(options, "Length", 14), Integer(options, "MaxLength", 100), Number(options, -1, "Trend")).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Efficiency, double[] Alpha) BryantValues(IReadOnlyList<Bar> bars, int length, int maximum, double trend)
    {
        length = Math.Max(1, length); maximum = Math.Max(1, maximum); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var steps = prices.Select((v, i) => i == 0 ? R(0) : (v - prices[i - 1]).Abs()).ToArray();
        var output = new double[bars.Count]; var efficiency = new double[bars.Count]; var alpha = new double[bars.Count]; var signals = new Signal[bars.Count]; var previous = R(0); var previousSpread = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var travel = Window(steps, i, length).Aggregate(R(0), (a, b) => a + b);
            efficiency[i] = i < length || travel.Sign == 0 ? 0 : ((prices[i] - prices[i - length]).Abs() / travel).ToDouble();
            var shape = R(1) + R(trend) * (R(efficiency[i]) - R(.5));
            var gain = shape.Sign == 0 ? R(1) : R(2) * shape * shape / new ReferenceFraction(length + 1L); var floor = R(2) / new ReferenceFraction(maximum + 1L);
            if (gain.CompareTo(floor) < 0) gain = floor; if (gain.CompareTo(R(1)) > 0) gain = R(1); alpha[i] = gain.ToDouble();
            output[i] = (previous + R(alpha[i]) * (prices[i] - previous)).ToDouble(); var current = R(output[i]); var spread = prices[i] - current;
            signals[i] = spread.Sign > 0 && spread.CompareTo(previousSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previousSpread) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            previous = current; previousSpread = spread;
        }
        return (new Dictionary<string, double[]> { ["Bama"] = output }, signals, efficiency, alpha);
    }
}
