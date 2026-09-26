using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HoltOutputs(IReadOnlyList<Bar> bars, int alphaLength, int? gammaLength = null)
    {
        var alpha = 2d / (Math.Max(1, alphaLength) + 1d); var gamma = 2d / (Math.Max(1, gammaLength ?? alphaLength) + 1d);
        var a = ReferenceFraction.FromDouble(alpha); var g = ReferenceFraction.FromDouble(gamma);
        var retainedLevel = ReferenceFraction.FromDouble(1 - alpha); var retainedTrend = ReferenceFraction.FromDouble(1 - gamma);
        var level = new ReferenceFraction(0); var trend = level; var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var previousTrend = i == 0 ? price : trend;
            var nextLevel = RoundRocBankStage(RoundRocBankStage(retainedLevel * RoundRocBankStage(level + previousTrend)) + RoundRocBankStage(a * price));
            trend = RoundRocBankStage(RoundRocBankStage(retainedTrend * previousTrend) + RoundRocBankStage(g * RoundRocBankStage(nextLevel - level)));
            values[i] = nextLevel.ToDouble(); level = nextLevel;
        }
        return Outputs(("Hema", values));
    }
}
