using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? DominantCycleFormula(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.EhlersDualDifferentiatorDominantCycle or IndicatorName.EhlersHomodyneDominantCycle
            or IndicatorName.EhlersPhaseAccumulationDominantCycle)) return null;
        var options = indicator.CreateOptions();
        var upper = Integer(options, "Length1"); var lower = Integer(options, "Length2");
        var minimum = Integer(options, "Length3");
        var phaseAccumulation = name == IndicatorName.EhlersPhaseAccumulationDominantCycle;
        var key = phaseAccumulation ? "Epadc" : name == IndicatorName.EhlersHomodyneDominantCycle ? "Ehdc" : "Edddc";
        var mode = phaseAccumulation ? 2 : name == IndicatorName.EhlersHomodyneDominantCycle ? 1 : 0;
        return new(key, new[] { key }, bars => Outputs((key, HilbertCycleValues(bars, upper, lower, minimum, Integer(options, "Length4", 40), mode).Values)));
    }
}
