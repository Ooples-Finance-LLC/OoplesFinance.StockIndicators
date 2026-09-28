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
        if (indicator.BatchName is not (IndicatorName.EhlersAdaptiveCyberCycle or IndicatorName.EhlersAdaptiveCenterOfGravityOscillator)) return null;
        if (indicator.BatchName == IndicatorName.EhlersAdaptiveCyberCycle)
            return new("Eacc", new[] { "Eacc", "Period" }, bars => AdaptiveCyberValues(bars, Integer(indicator.CreateOptions(), "Length", 5), Number(indicator.CreateOptions(), .07, "Alpha")));
        var options = indicator.CreateOptions();
        var gravity = indicator.BatchName == IndicatorName.EhlersAdaptiveCenterOfGravityOscillator;
        return new(gravity ? "Eacog" : "Eacc", gravity ? new[] { "Eacog" } : new[] { "Eacc", "Period" }, bars =>
        {
            var prices = Closes(bars);
            var periods = AdaptiveCyberPeriods(prices, Integer(options, "Length", 5), Number(options, .07, "Alpha"));
            if (gravity)
            {
                var center = prices.Select((_, i) =>
                {
                    var window = (int)Math.Ceiling(periods[i] / 2);
                    var values = Window(prices, i, window).Reverse().ToArray();
                    var mass = values.Sum();
                    // This published variant uses the integer midpoint of its adaptive window.
                    return mass == 0 ? 0 : (window + 1) / 2 - values.Select((v, j) => (j + 1) * v).Sum() / mass;
                }).ToArray();
                return Outputs(("Eacog", center));
            }
            double Price(int i) => i < 0 ? 0 : prices[i];
            var smooth = prices.Select((_, i) => (Price(i) + 2 * Price(i - 1) + 2 * Price(i - 2) + Price(i - 3)) / 6).ToArray();
            var line = new double[prices.Length];
            for (var i = 0; i < line.Length; i++)
            {
                if (i < 7) { line[i] = (Price(i) - 2 * Price(i - 1) + Price(i - 2)) / 4; continue; }
                var pole = (periods[i] - 1) / (periods[i] + 1);
                line[i] = Math.Pow((1 + pole) / 2, 2) * (smooth[i] - 2 * smooth[i - 1] + smooth[i - 2])
                    + pole * (2 * line[i - 1] - pole * line[i - 2]);
            }
            return Outputs(("Eacc", line), ("Period", periods));
        });
    }

    private static double[] AdaptiveCyberPeriods(double[] prices, int length, double alpha) => AdaptiveCyberReference(prices, length, alpha)["Period"];
}
