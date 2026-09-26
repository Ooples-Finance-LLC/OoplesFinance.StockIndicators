using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DoubleSmoothingOutputs(IReadOnlyList<Bar> bars, double alpha = .01, double gamma = .9)
    {
        var a = ReferenceFraction.FromDouble(alpha); var g = ReferenceFraction.FromDouble(gamma);
        var retainedLevel = ReferenceFraction.FromDouble(1 - alpha); var retainedTrend = ReferenceFraction.FromDouble(1 - gamma);
        var previous = new ReferenceFraction(0); var prior = previous; var line = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var change = RoundRocBankStage(previous - prior);
            var trend = RoundRocBankStage(g * RoundRocBankStage(change + RoundRocBankStage(retainedTrend * change)));
            var level = RoundRocBankStage(retainedLevel * RoundRocBankStage(previous + trend));
            var value = RoundRocBankStage(RoundRocBankStage(a * ReferenceFraction.FromDouble(bars[i].Close)) + level);
            line[i] = value.ToDouble(); prior = previous; previous = value;
        }
        return Outputs(("Des", line));
    }
}
