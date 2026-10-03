using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CoralTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => CoralTrendValues(bars, Integer(indicator.CreateOptions(), "Length", 21), .4).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) CoralTrendValues(IReadOnlyList<Bar> bars, int length, double cd)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var gainValue = 4d / (Math.Max(1, length) + 3d); var gain = R(gainValue); var carry = R(1 - gainValue);
        var coefficient = R(cd); var one = R(1); var a = one + coefficient;
        var weights = new[] { a * a * a, R(-3) * coefficient * a * a, R(3) * coefficient * coefficient * a, R(0) - coefficient * coefficient * coefficient };
        var layers = new ReferenceFraction[6][];
        for (var layer = 0; layer < layers.Length; layer++)
        {
            layers[layer] = new ReferenceFraction[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                var current = layer == 0 ? R(bars[i].Close) : layers[layer - 1][i]; var previous = i == 0 ? R(0) : layers[layer][i - 1];
                layers[layer][i] = ((gain * current).RoundExtendedBinary64() + (carry * previous).RoundExtendedBinary64()).RoundExtendedBinary64();
            }
        }
        var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var oldSpread = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = R(0); for (var j = 0; j < 4; j++) sum += weights[j] * layers[j + 2][i];
            var line = sum.RoundExtendedBinary64(); output[i] = line.ToDouble(); var spread = R(bars[i].Close) - line;
            signals[i] = spread.Sign > 0 && spread.CompareTo(oldSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(oldSpread) < 0 ? Signal.StrongSell
                : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            oldSpread = spread;
        }
        return (new Dictionary<string, double[]> { ["Cti"] = output }, signals);
    }
}
