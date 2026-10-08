using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> EhlersSimpleWindowOutputs(IReadOnlyList<Bar> bars, int length = 20, int kind = 1)
    {
        length = Math.Max(1, length); var changes = bars.Select(b => RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open))).ToArray();
        var first = SmoothRocBankStage(changes, length, kind); var second = SmoothRocBankStage(first, length, kind); var third = SmoothRocBankStage(second, length, kind);
        var factor = ReferenceFraction.FromDouble(length / 2.0 * Math.PI);
        var roc = third.Select((v, i) => RoundRocBankStage(RoundRocBankStage(v - (i == 0 ? new ReferenceFraction(0) : third[i - 1])) * factor).ToDouble()).ToArray();
        return new() { { "Etwi", first.Select(v => v.ToDouble()).ToArray() }, { "Roc", roc } };
    }
}
