using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CycleAmplitudeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return CycleAmplitudeOutputs(bars, Integer(o, "Length", 20), Number(o, .1, "Delta")); }
    internal static IReadOnlyDictionary<string, double[]> CycleAmplitudeOutputs(IReadOnlyList<Bar> bars, int length, double delta)
    {
        length = Math.Max(1, length); var delay = (int)Math.Ceiling(length / 4d); double Clamp(double x) => x < .01 ? .01 : x > .99 ? .99 : x;
        var beta = ReferenceFraction.FromDouble(Math.Cos(Clamp(2 * Math.PI / length))); var gamma = 1 / Math.Cos(Clamp(4 * Math.PI * delta / length)); var alpha = ReferenceFraction.FromDouble(gamma - Math.Sqrt(gamma * gamma - 1)); var one = new ReferenceFraction(1); var zero = new ReferenceFraction(0);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var band = new ReferenceFraction[bars.Count]; var energy = new ReferenceFraction[bars.Count]; var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            band[i] = i < 2 ? zero : (((one - alpha) / new ReferenceFraction(2)) * (prices[i] - prices[i - 2]) + beta * (one + alpha) * band[i - 1] - alpha * band[i - 2]).RoundExtendedBinary64();
            energy[i] = band[i] * band[i]; var sum = zero;
            for (var j = Math.Max(0, i + 1 - length); j <= i; j++) sum += energy[j];
            for (var j = Math.Max(0, i - delay + 1 - length); j <= i - delay; j++) sum += energy[j];
            var root = (sum / new ReferenceFraction(length)).SqrtToDouble(); output[i] = double.IsInfinity(root) ? double.PositiveInfinity : (ReferenceFraction.FromDouble(root) * ReferenceFraction.FromDouble(2 * 1.414)).ToDouble();
        }
        return Outputs(("Eca", output));
    }
}
