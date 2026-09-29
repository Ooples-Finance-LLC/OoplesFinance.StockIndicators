using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? AdaptiveEhlersV2(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.EhlersAdaptiveRelativeStrengthIndexV2 or
            IndicatorName.EhlersAdaptiveStochasticIndicatorV2 or IndicatorName.EhlersAdaptiveCommodityChannelIndexV2 or
            IndicatorName.EhlersAdaptiveRsiFisherTransformV2 or IndicatorName.EhlersAdaptiveStochasticInverseFisherTransform)) return null;
        var options = indicator.CreateOptions();
        var upper = Integer(options, "Length1", 48);
        var lower = Integer(options, "Length2", 10);
        var lag = Integer(options, "Length3", 3);
        var kind = AverageKind(options, 3);
        if (kind == 0) return null;
        var fisher = name == IndicatorName.EhlersAdaptiveRsiFisherTransformV2;
        var inverse = name == IndicatorName.EhlersAdaptiveStochasticInverseFisherTransform;
        var rsi = fisher || name == IndicatorName.EhlersAdaptiveRelativeStrengthIndexV2;
        var stochastic = inverse || name == IndicatorName.EhlersAdaptiveStochasticIndicatorV2;
        var key = fisher ? "Earsift" : inverse ? "Easift" : rsi ? "Earsi" : stochastic ? "Easi" : "Eacci";
        if (rsi) return new(key, fisher ? new[] { key } : new[] { key, "Signal" }, bars => AdaptiveRsiV2Values(bars, upper, lower, lag, kind, fisher).Outputs);
        return new(key, new[] { key, "Signal" }, bars => AdaptiveRangeV2Values(bars, upper, lower, lag, kind, inverse ? 1 : stochastic ? 0 : 2).Outputs);
    }
}
