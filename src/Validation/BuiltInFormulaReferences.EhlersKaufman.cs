using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EhlersKaufmanOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 20));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var zero = new ReferenceFraction(0);
        var changes = prices.Select((p, i) => (p - (i == 0 ? zero : prices[i - 1])).Abs()).ToArray();
        // Prefix totals are independent of the production sliding queue and avoid rescanning each window.
        var prefix = new ReferenceFraction[bars.Count + 1]; prefix[0] = zero;
        for (var i = 0; i < bars.Count; i++) prefix[i + 1] = prefix[i] + changes[i];
        var output = new double[bars.Count]; var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var travel = prefix[i + 1] - prefix[Math.Max(0, i - length + 1)];
            var displacement = (prices[i] - (i < length - 1 ? zero : prices[i - length + 1])).Abs();
            var efficiency = travel.Sign == 0 ? 0 : Math.Min(1, (displacement / travel).ToDouble());
            var scaled = (ReferenceFraction.FromDouble(.6667) * ReferenceFraction.FromDouble(efficiency)).ToDouble();
            var root = (ReferenceFraction.FromDouble(scaled) + ReferenceFraction.FromDouble(.0645)).ToDouble();
            var gain = ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(root) * ReferenceFraction.FromDouble(root)).ToDouble());
            previous = ReferenceFraction.FromDouble((previous + gain * (prices[i] - previous)).ToDouble()); output[i] = previous.ToDouble();
        }
        return Outputs(("Ekama", output));
    }
}
