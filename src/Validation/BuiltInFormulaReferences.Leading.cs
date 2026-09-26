using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> LeadingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double alpha1 = .25, double alpha2 = .33)
    {
        var a = ReferenceFraction.FromDouble(alpha1); var b = ReferenceFraction.FromDouble(alpha2);
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var lead = new ReferenceFraction(0); var previous = lead; var price = lead; var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close);
            lead = (two * current + (a - two) * price + (one - a) * lead).RoundExtendedBinary64();
            previous = (b * lead + (one - b) * previous).RoundExtendedBinary64();
            price = current; output[i] = previous.ToDouble();
        }
        return Outputs(("Eli", output));
    }
}
