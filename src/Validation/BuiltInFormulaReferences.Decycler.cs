using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DecyclerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 60));
        var tangent = length <= 2 ? 0 : Math.Tan(Math.PI / length);
        var alpha = ReferenceFraction.FromDouble(length <= 2 ? 2 : 2 * tangent / (1 + tangent));
        var previous = new ReferenceFraction(0); var price = previous; var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close);
            previous = (alpha * (current + price) / new ReferenceFraction(2) + (new ReferenceFraction(1) - alpha) * previous).RoundExtendedBinary64();
            price = current; output[i] = previous.ToDouble();
        }
        return Outputs(("Ed", output));
    }
}
