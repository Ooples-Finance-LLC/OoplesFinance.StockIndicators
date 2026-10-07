using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] AlphaDecreasingOutput(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count]; var previous = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var alpha = 2d / (i + 1d);
            var current = RoundRocBankStage(ReferenceFraction.FromDouble(alpha) * ReferenceFraction.FromDouble(bars[i].Close));
            var retained = RoundRocBankStage(ReferenceFraction.FromDouble(1 - alpha) * previous);
            previous = RoundRocBankStage(current + retained); values[i] = previous.ToDouble();
        }
        return values;
    }
}
