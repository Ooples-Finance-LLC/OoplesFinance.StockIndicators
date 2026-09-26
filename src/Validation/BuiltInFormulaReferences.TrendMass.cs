using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? TrendMass(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var name = indicator.BatchName;
        if (name != IndicatorName.SellGravitationIndex && name != IndicatorName.TrendAnalysisIndex &&
            name != IndicatorName.TrendAnalysisIndicator && name != IndicatorName.MassThrustIndicator &&
            name != IndicatorName.MassThrustOscillator) return null;
        var trend = name == IndicatorName.TrendAnalysisIndex || name == IndicatorName.TrendAnalysisIndicator;
        var kind = AverageKind(options, trend ? 1 : 3);
        if (kind == 0) return null;
        var key = trend ? "Tai" : name == IndicatorName.SellGravitationIndex ? "Sgi"
            : name == IndicatorName.MassThrustIndicator ? "Mti" : "Mto";
        return new(key, new[] { key, "Signal" }, bars =>
        {
            var length = Integer(options, "Length", 14);
            double[] line;
            var signalLength = length;
            if (trend)
            {
                var slow = Integer(options, "Length1");
                var fast = Integer(options, "Length2");
                var average = Average(Closes(bars), slow, kind);
                line = name == IndicatorName.TrendAnalysisIndicator
                    ? PopulationVariance(average, fast).Select(Math.Sqrt).ToArray()
                    : average.Select((_, i) => bars[i].Close == 0 ? 0
                        : 100 * (Window(average, i, fast).Max()
                            - Window(average, i, fast).Min()) / bars[i].Close).ToArray();
                signalLength = name == IndicatorName.TrendAnalysisIndicator ? slow : fast;
            }
            else if (name == IndicatorName.SellGravitationIndex)
            {
                var bodies = bars.Select(b => b.High == b.Low ? 0 : (b.Close - b.Open) / (b.High - b.Low)).ToArray(); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                line = Average(bodies, length, kind);
            }
            else
            {
                var gains = bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, b.Close - bars[i - 1].Close)).ToArray();
                var losses = bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, bars[i - 1].Close - b.Close)).ToArray();
                var advances = gains.Select((_, i) => Window(gains, i, length).Sum()).ToArray();
                var declines = losses.Select((_, i) => Window(losses, i, length).Sum()).ToArray();
                var upVolume = gains.Select((g, i) => g == 0 || advances[i] == 0 ? 0 : bars[i].Volume / advances[i]).ToArray();
                var downVolume = losses.Select((d, i) => d == 0 || declines[i] == 0 ? 0 : bars[i].Volume / declines[i]).ToArray();
                line = gains.Select((_, i) =>
                {
                    var positive = advances[i] * Window(upVolume, i, length).Sum();
                    var negative = declines[i] * Window(downVolume, i, length).Sum();
                    return name == IndicatorName.MassThrustIndicator ? (positive - negative) / 1e6
                        : positive + negative == 0 ? 0 : 100 * (positive - negative) / (positive + negative);
                }).ToArray();
            }
            return Outputs((key, line), ("Signal", Average(line, signalLength, kind)));
        });
    }
}
