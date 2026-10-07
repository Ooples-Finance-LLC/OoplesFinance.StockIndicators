using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ZeroLagTripleOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 14); var kind = AverageKind(options, 5);
        ReferenceFraction[] Stage(ReferenceFraction[] input)
        {
            if (kind != 5) return SmoothRocBankStage(input, length, kind);
            var first = SmoothRocBankStage(input, length, 3); var second = SmoothRocBankStage(first, length, 3); var third = SmoothRocBankStage(second, length, 3);
            return first.Select((v, i) => RoundRocBankStage(new ReferenceFraction(3) * (v - second[i]) + third[i])).ToArray();
        }
        var first = Stage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray()); var second = Stage(first);
        return Outputs(("Ztema", first.Select((v, i) => (new ReferenceFraction(2) * v - second[i]).ToDouble()).ToArray()));
    }
}
