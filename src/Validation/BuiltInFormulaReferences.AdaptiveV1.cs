using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? AdaptiveV1(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.EhlersAdaptiveRelativeStrengthIndexV1 or IndicatorName.EhlersAdaptiveRsiFisherTransformV1
            or IndicatorName.EhlersAdaptiveStochasticIndicatorV1 or IndicatorName.EhlersAdaptiveCommodityChannelIndexV1)) return null;
        var stochastic = name == IndicatorName.EhlersAdaptiveStochasticIndicatorV1;
        var commodity = name == IndicatorName.EhlersAdaptiveCommodityChannelIndexV1;
        var fisher = name == IndicatorName.EhlersAdaptiveRsiFisherTransformV1;
        var options = indicator.CreateOptions();
        var fraction = Number(options, commodity ? 1 : .5, "CycPart");
        var key = stochastic ? "Easi" : commodity ? "Eacci" : fisher ? "Earsift" : "Earsi";
        return new(key, fisher ? new[] { key } : new[] { key, "Signal" }, bars =>
        {
            var periods = MamaReference(Closes(bars))["SmoothPeriod"];
            var source = bars.Select(b => commodity ? (b.High + b.Low + b.Close) / 3 : b.Close).ToArray();
            var values = new double[bars.Count];
            var gains = new double[bars.Count];
            for (var i = 0; i < values.Length; i++)
            {
                var length = (int)Math.Ceiling(fraction * periods[i]);
                var indices = Enumerable.Range(i - length + 1, length).ToArray();
                if (commodity)
                {
                    var sample = indices.Select(j => j < 0 ? 0 : source[j]).ToArray();
                    var mean = sample.Average();
                    var deviation = sample.Average(v => Math.Abs(v - mean));
                    values[i] = deviation == 0 ? 0 : (source[i] - mean) / (Number(options, .015, "Constant") * deviation);
                }
                else if (stochastic)
                {
                    var high = indices.Select(j => j < 0 ? 0 : bars[j].High).Append(bars[i].High).Max();
                    var low = indices.Select(j => j < 0 ? 0 : bars[j].Low).Append(bars[i].Low).Min();
                    values[i] = high == low ? 0 : 100 * (bars[i].Close - low) / (high - low); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                }
                else
                {
                    var changes = indices.Select(j => (j < 0 ? 0 : source[j]) - (j < 1 ? 0 : source[j - 1])).ToArray();
                    var travel = changes.Sum(v => Math.Abs(v));
                    values[i] = travel == 0 ? 0 : 100 * changes.Sum(v => Math.Max(0, v)) / travel;
                }
                gains[i] = Clamp(2d / ((stochastic ? length : (int)Math.Ceiling(periods[i])) + 1), .01, .99);
            }
            if (fisher) return Outputs((key, values.Select(v =>
            {
                var bounded = Clamp(3 * (v / 100 - .5), -.999, .999);
                return (Math.Log(1 + bounded) - Math.Log(1 - bounded)) / 2;
            }).ToArray()));
            var signal = values.Select((_, i) =>
            {
                double retained = 1, total = 0;
                for (var j = i; j >= 0; j--) { total += retained * gains[j] * values[j]; retained *= 1 - gains[j]; }
                return total;
            }).ToArray();
            return Outputs((key, values), ("Signal", signal));
        });
    }
}
