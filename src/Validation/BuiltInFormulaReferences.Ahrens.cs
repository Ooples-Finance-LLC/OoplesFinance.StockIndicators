using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AhrensOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Integer(indicator.CreateOptions(), "Length", 9); var previous = new ReferenceFraction(0);
        var history = new ReferenceFraction[bars.Count]; var values = new double[bars.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close); var prior = i >= length ? history[i - length] : current;
            var midpoint = RoundRocBankStage(RoundRocBankStage(previous + prior) / new ReferenceFraction(2));
            var gap = RoundRocBankStage(current - midpoint);
            previous = RoundRocBankStage(previous + RoundRocBankStage(gap / new ReferenceFraction(length)));
            history[i] = previous; values[i] = previous.ToDouble();
        }
        return Outputs(("Ahma", values));
    }
}
