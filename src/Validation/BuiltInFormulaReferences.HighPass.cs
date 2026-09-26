using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HighPassOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double multiplier = 1) =>
        Outputs(("Hp", HighPassStates(bars, indicator, multiplier).Select(value => value.ToDouble()).ToArray()));

    private static ReferenceFraction[] HighPassStates(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double multiplier = 1) =>
        HighPassStates(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), Math.Max(1, Integer(indicator.CreateOptions(), "Length", 125)), multiplier);

    private static ReferenceFraction[] HighPassStates(IReadOnlyList<ReferenceFraction> prices, int length, double multiplier = 1)
    {
        var cutoff = multiplier * length * Math.Sqrt(2); var tangent = cutoff <= 2 ? 0 : Math.Tan(Math.PI / cutoff);
        var alpha = ReferenceFraction.FromDouble(cutoff <= 2 ? 2 : 2 * tangent / (1 + tangent));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0);
        var pole = one - alpha; var gain = (one - alpha / two) * (one - alpha / two);
        var states = new ReferenceFraction[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        {
            var price = prices[i];
            var previous = i > 0 ? prices[i - 1] : zero;
            var older = i > 1 ? prices[i - 2] : zero;
            var difference = (price - previous) - (previous - older);
            var next = gain * difference + two * pole * (i > 0 ? states[i - 1] : zero) - pole * pole * (i > 1 ? states[i - 2] : zero);
            states[i] = next.RoundExtendedBinary64();
        }
        return states;
    }
}
