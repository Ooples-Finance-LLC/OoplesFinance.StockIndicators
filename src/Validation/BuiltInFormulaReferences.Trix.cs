using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrixOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 15); var kind = AverageKind(options, 3);
        var values = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        for (var stage = 0; stage < 3; stage++) values = SmoothRocBankStage(values, length, kind);
        var line = values.Select((value, i) => i == 0 || values[i - 1].Sign == 0 ? 0 :
            (new ReferenceFraction(100) * (value - values[i - 1]) / values[i - 1].Abs()).ToDouble()).ToArray();
        return Outputs(("Trix", line));
    }
}
