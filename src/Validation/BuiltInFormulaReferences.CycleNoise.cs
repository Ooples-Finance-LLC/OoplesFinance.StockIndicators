using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? CycleNoise(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name == IndicatorName.EhlersSignalToNoiseRatioV1) { var noiseSettings = indicator.CreateOptions(); return new("Esnr", new[] { "Esnr" }, bars => HilbertNoiseValues(bars, Integer(noiseSettings, "Length", 7), AverageKind(noiseSettings, 3)).Outputs); }
        if (name is IndicatorName.EhlersAlternateSignalToNoiseRatio or IndicatorName.EhlersEnhancedSignalToNoiseRatio or IndicatorName.EhlersSignalToNoiseRatioV2)
        {
            var noiseMode = name == IndicatorName.EhlersAlternateSignalToNoiseRatio ? 0 : name == IndicatorName.EhlersSignalToNoiseRatioV2 ? 1 : 2; var noiseLength = Integer(indicator.CreateOptions(), "Length", 6); return new("Esnr", noiseMode == 2 ? new[] { "Esnr", "I3", "Q3", "SmoothPeriod" } : new[] { "Esnr" }, bars => MamaNoiseValues(bars, noiseLength, noiseMode).Outputs);
        }
        if (name == IndicatorName.EhlersHilbertOscillator) return new("IQ", new[] { "I3", "IQ" }, bars => MamaDerivedValues(bars, 1).Outputs);
        if (name == IndicatorName.EhlersInstantaneousTrendlineV1) return new("Eit", new[] { "Eit", "Signal" }, bars => MamaDerivedValues(bars, 2).Outputs);
        if (name is not (IndicatorName.EhlersAlternateSignalToNoiseRatio or IndicatorName.EhlersEnhancedSignalToNoiseRatio
            or IndicatorName.EhlersSignalToNoiseRatioV1 or IndicatorName.EhlersSignalToNoiseRatioV2 or IndicatorName.EhlersInstantaneousTrendlineV1)) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 6);
        var trend = name == IndicatorName.EhlersInstantaneousTrendlineV1;
        var enhanced = name == IndicatorName.EhlersEnhancedSignalToNoiseRatio;
        return new(trend ? "Eit" : "Esnr", trend ? new[] { "Eit", "Signal" }
            : enhanced ? new[] { "Esnr", "I3", "Q3", "SmoothPeriod" } : new[] { "Esnr" }, bars =>
        {
            var mama = MamaReference(Closes(bars));
            var periods = mama["SmoothPeriod"];
            if (trend)
            {
                var mean = bars.Select((_, i) =>
                {
                    var window = (int)Math.Ceiling(periods[i] + .5);
                    return Window(bars, i, window).Sum(b => b.Close) / window;
                }).ToArray();
                var signal = mean.Select((_, i) => Enumerable.Range(0, Math.Min(4, i + 1)).Sum(lag => (4 - lag) * mean[i - lag]) / 10).ToArray();
                return Outputs(("Eit", mean), ("Signal", signal));
            }
            var ranges = bars.Select(b => b.High - b.Low).ToArray();
            if (enhanced)
            {
                var smooth = mama["Smooth"];
                var q = smooth.Select((v, i) => .5 * (v - (i < 2 ? 0 : smooth[i - 2])) * (.1759 * periods[i] + .4607)).ToArray();
                var real = q.Select((_, i) => 1.57 * Window(q, i, (int)Math.Ceiling(periods[i] / 2)).Sum() / Math.Ceiling(periods[i] / 2)).ToArray();
                var noise = Average(ranges.Select(v => v * v / 4).ToArray(), 10, 6);
                var decibels = q.Select((v, i) => noise[i] == 0 || v * v + real[i] * real[i] == 0 ? 0
                    : 10 * Math.Log10((v * v + real[i] * real[i]) / noise[i])).ToArray();
                var filtered = decibels.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => .33 * Math.Pow(.67, i - j) * decibels[j])).ToArray();
                return Outputs(("Esnr", filtered), ("I3", real), ("Q3", q), ("SmoothPeriod", periods));
            }
            var v1 = name == IndicatorName.EhlersSignalToNoiseRatioV1;
            var alternate = name == IndicatorName.EhlersAlternateSignalToNoiseRatio;
            var range = Average(ranges, v1 ? 5 : 10, 6);
            double[] energy;
            if (v1)
            {
                var hilbert = HilbertFormulas(new EhlersHilbertTransformIndicator(length, .635, .338))!.Compute(bars);
                energy = Average(hilbert["Inphase"].Select((v, i) => v * v + hilbert["Quad"][i] * hilbert["Quad"][i]).ToArray(), 5, 6);
            }
            else energy = alternate ? mama["Real"].Select((v, i) => v + mama["Imag"][i]).ToArray()
                : mama["I1"].Select((v, i) => v * v + mama["Q1"][i] * mama["Q1"][i]).ToArray();
            var result = new double[bars.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var ratio = range[i] == 0 ? 0 : energy[i] / (range[i] * range[i]);
                var level = (ratio > 0 ? 10 * Math.Log10(ratio) : 0) + (v1 ? 1.9 : length);
                result[i] = !alternate && range[i] == 0 ? 0 : .25 * level + (i == 0 ? 0 : .75 * result[i - 1]);
            }
            return Outputs(("Esnr", result));
        });
    }
}
