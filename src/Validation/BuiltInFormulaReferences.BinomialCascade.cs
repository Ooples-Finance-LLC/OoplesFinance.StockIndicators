using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BinomialCascadeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var pentuple = indicator.BatchName == IndicatorName.PentupleExponentialMovingAverage;
        var order = pentuple ? 8 : 5;
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 20);
        var values = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var totals = Enumerable.Repeat(new ReferenceFraction(0), bars.Count).ToArray();
        var coefficient = 1;
        for (var stage = 1; stage <= order; stage++)
        {
            coefficient = coefficient * (order - stage + 1) / stage;
            values = SmoothRocBankStage(values, length, AverageKind(options, 3));
            for (var i = 0; i < bars.Count; i++) totals[i] += new ReferenceFraction(stage % 2 == 1 ? coefficient : -coefficient) * values[i];
        }
        return Outputs((pentuple ? "Pema" : "Qema", totals.Select(v => v.ToDouble()).ToArray()));
    }
}
