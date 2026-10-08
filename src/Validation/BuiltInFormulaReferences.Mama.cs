using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MamaFormulas(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.EhlersMotherOfAdaptiveMovingAverages) return null;
        var options = indicator.CreateOptions();
        return new("Mama", new[] { "Fama", "Mama", "I1", "Q1", "SmoothPeriod", "Smooth", "Real", "Imag" },
            bars => MamaReference(Closes(bars), Number(options, .5, "FastLimit"), Number(options, .05, "SlowLimit")));
    }

    private static IReadOnlyDictionary<string, double[]> MamaReference(double[] prices, double fast = .5, double slow = .05) => MamaValues(prices, fast, slow).Outputs;
}
