using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    // Arithmetic stages are once-rounded; the platform transcendental receives a small relative budget.
    internal static readonly IndicatorErrorBudget RsiInverseFisherBudget = new(0, 4e-15, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> RsiInverseFisherOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 2);
        var relative = indicator.BatchName == IndicatorName.EhlersRelativeStrengthIndexInverseFisherTransform;
        var length = options is InverseFisherTransformCoreSpecOptions ? 5 : Integer(options, "Length", relative ? 14 : 5);
        var signal = relative ? Integer(options, "SignalLength", 9) : 9;
        var rsi = RoundedPriceRsi(bars, length, kind);
        var scaled = rsi.Select(v => ReferenceFraction.FromDouble((ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(v) - new ReferenceFraction(50)).ToDouble())
            * ReferenceFraction.FromDouble(.1)).ToDouble())).ToArray();
        var smoothed = SmoothStrengthStage(scaled, signal, kind);
        return Outputs((relative ? "Eiftrsi" : "Eift", smoothed.Select(v => v.TanhToDouble()).ToArray()));
    }
}
