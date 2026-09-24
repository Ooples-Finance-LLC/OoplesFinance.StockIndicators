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
        return new(key, fisher ? new[] { key } : new[] { key, "Signal" }, bars =>
        {
            var roof = HilbertRoofingTrajectory(Closes(bars), upper, lower);
            var cycles = AutocorrelationSpectrum(AutocorrelationTrajectory(roof, upper), upper, lower, lag);
            var raw = new double[bars.Count];
            var valid = new bool[bars.Count];
            var squaredResiduals = new double[bars.Count];
            for (var i = 0; i < raw.Length; i++)
            {
                var cycle = Math.Min(upper, Math.Max(lower, cycles[i])) / (rsi ? 2 : 1);
                // The contract snaps cycle estimates within relative 1e-9 of an integer.
                var nearest = Math.Round(cycle);
                var window = (int)(Math.Abs(cycle - nearest) <= 1e-9 * Math.Max(1, Math.Abs(cycle))
                    ? nearest : Math.Ceiling(cycle));
                var sample = Window(roof, i, window).ToArray();
                if (rsi)
                {
                    var changes = Enumerable.Range(Math.Max(0, i - window + 1), Math.Min(window, i + 1))
                        .Select(j => roof[j] - (j == 0 ? 0 : roof[j - 1])).ToArray();
                    var total = changes.Sum(Math.Abs);
                    valid[i] = total != 0;
                    raw[i] = total == 0 ? 0 : changes.Sum(v => Math.Max(0, v)) / total;
                }
                else if (stochastic)
                {
                    var range = sample.Max() - sample.Min();
                    raw[i] = range == 0 ? 0 : (roof[i] - sample.Min()) / range;
                }
                else
                {
                    // Library variant: RMS of historical residuals against their own adaptive means,
                    // rather than the mean absolute deviation used by conventional CCI.
                    var residual = roof[i] - sample.Average();
                    squaredResiduals[i] = residual * residual;
                    var rms = Math.Sqrt(Window(squaredResiduals, i, window).Average());
                    raw[i] = rms == 0 ? 0 : residual / (.015 * rms);
                }
            }
            var radius = Math.Exp(-1.414 * Math.PI / lower);
            var angle = Math.Min(1.414 * Math.PI / lower, .99);
            var line = HilbertLowPass(raw, radius, angle);
            if (rsi)
            {
                // Reconstruct the conjugate-section state from the last two published outputs.
                // This retains the explicit zero-output behavior at undefined adjacent ratios.
                var pole = Complex.FromPolarCoordinates(radius, angle);
                var gain = ((1 - pole) * (1 - Complex.Conjugate(pole))).Real;
                for (var i = 0; i < line.Length; i++)
                {
                    if (i == 0 || !valid[i] || !valid[i - 1]) { line[i] = 0; continue; }
                    var second = line[i - 1];
                    var first = second - Complex.Conjugate(pole) * (i < 2 ? 0 : line[i - 2]);
                    first = gain * (raw[i] + raw[i - 1]) / 2 + pole * first;
                    line[i] = (first + Complex.Conjugate(pole) * second).Real;
                }
            }
            if (fisher)
                return Outputs((key, line.Select(value =>
                {
                    var x = Math.Max(-.999, Math.Min(.999, 3 * (value - .5)));
                    return (Math.Log(1 + x) - Math.Log(1 - x)) / 2;
                }).ToArray()));
            if (inverse)
            {
                var transformed = line.Select(value => Math.Tanh(6 * (value - .5))).ToArray();
                return Outputs((key, transformed), ("Signal", transformed.Select((_, i) => i == 0 ? 0 : .9 * transformed[i - 1]).ToArray()));
            }
            return Outputs((key, line), ("Signal", Average(line, lower, kind)));
        });
    }
}
