using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? AdaptiveCyberFormulas(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName == IndicatorName.DominantCycleTunedRelativeStrengthIndex)
            return new("DctRsi", new[] { "DctRsi" }, bars =>
            {
                var prices = Closes(bars);
                var periods = AdaptiveCyberPeriods(prices, Integer(indicator.CreateOptions(), "Length", 5), .07);
                var changes = prices.Select((v, i) => v - (i == 0 ? 0 : prices[i - 1])).ToArray();
                var gains = changes.Select(v => Math.Max(0, v)).ToArray();
                var losses = changes.Select(v => Math.Max(0, -v)).ToArray();
                double Weighted(double[] values, int end)
                {
                    double result = 0, survival = 1;
                    for (var j = end; j >= 1; j--)
                    {
                        var weight = periods[j] == 0 ? .07 : 1 / periods[j];
                        result += survival * weight * values[j];
                        survival *= 1 - weight;
                    }
                    return result + survival * values[0];
                }
                return Outputs(("DctRsi", prices.Select((_, i) =>
                {
                    var gain = Weighted(gains, i); var loss = Weighted(losses, i);
                    return loss == 0 ? 100 : gain == 0 ? 0 : Clamp(100 * gain / (gain + loss), 0, 100);
                }).ToArray()));
            });
        if (indicator.BatchName == IndicatorName.EhlersAdaptiveCyberCycle)
            return new("Eacc", new[] { "Eacc", "Period" }, bars => AdaptiveCyberValues(bars, Integer(indicator.CreateOptions(), "Length", 5), Number(indicator.CreateOptions(), .07, "Alpha")));
        if (indicator.BatchName == IndicatorName.EhlersAdaptiveCenterOfGravityOscillator)
            return new("Eacog", new[] { "Eacog" }, bars => Outputs(("Eacog", AdaptiveGravityValues(bars, Integer(indicator.CreateOptions(), "Length", 5)))));
        return null;
    }
    private static double[] AdaptiveCyberPeriods(double[] prices, int length, double alpha) => AdaptiveCyberReference(prices, length, alpha)["Period"];
}
