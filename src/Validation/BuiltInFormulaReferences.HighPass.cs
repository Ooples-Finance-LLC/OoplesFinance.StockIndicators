using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HighPassOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double multiplier = 1) =>
        Outputs(("Hp", HighPassStates(bars, indicator, multiplier).Select(value => value.ToDouble()).ToArray()));

    private static ReferenceFraction[] HighPassStates(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double multiplier = 1)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 125));
        var cutoff = multiplier * length * Math.Sqrt(2); var tangent = cutoff <= 2 ? 0 : Math.Tan(Math.PI / cutoff);
        var alpha = ReferenceFraction.FromDouble(cutoff <= 2 ? 2 : 2 * tangent / (1 + tangent));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0);
        var pole = one - alpha; var gain = (one - alpha / two) * (one - alpha / two);
        var states = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var previous = i > 0 ? ReferenceFraction.FromDouble(bars[i - 1].Close) : zero;
            var older = i > 1 ? ReferenceFraction.FromDouble(bars[i - 2].Close) : zero;
            var difference = (price - previous) - (previous - older);
            var next = gain * difference + two * pole * (i > 0 ? states[i - 1] : zero) - pole * pole * (i > 1 ? states[i - 2] : zero);
            states[i] = next.RoundExtendedBinary64();
        }
        return states;
    }
}
