using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TillsonOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 5)); var kind = AverageKind(options, 3);
        var v = ReferenceFraction.FromDouble(Number(options, .7, "VFactor")); var three = new ReferenceFraction(3); var two = new ReferenceFraction(2);
        var stages = new ReferenceFraction[6][]; var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        for (var stage = 0; stage < 6; stage++) stages[stage] = SmoothRocBankStage(stage == 0 ? prices : stages[stage - 1], length, kind);
        return Outputs(("T3", Enumerable.Range(0, bars.Count).Select(i => {
            var a = stages[2][i]; var b = stages[3][i]; var c = stages[4][i]; var d = stages[5][i];
            return (a + three * v * (a - b) + three * v * v * (a - two * b + c) + v * v * v * (a - three * b + three * c - d)).ToDouble();
        }).ToArray()));
    }
}
