using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? HilbertFormulas(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        var options = indicator.CreateOptions();
        if (name is IndicatorName.EhlersHilbertTransformIndicator or IndicatorName.EhlersInstantaneousPhaseIndicator or IndicatorName.EhlersSquelchIndicator)
        {
            var squelch = name == IndicatorName.EhlersSquelchIndicator;
            var phase = squelch || name == IndicatorName.EhlersInstantaneousPhaseIndicator;
            var length = Integer(options, phase ? "Length1" : "Length", 7);
            var horizon = Integer(options, squelch ? "Length3" : "Length2", 50);
            var inGain = Number(options, .635, "IMult");
            var quadGain = Number(options, .338, "QMult");
            var phaseKey = squelch ? "Esi" : "Eipi";
            return new(phase ? phaseKey : "Quad", phase ? new[] { phaseKey } : new[] { "Quad", "Inphase" }, bars =>
            {
                var prices = Closes(bars);
                double Difference(int i) => i < length ? 0 : prices[i] - prices[i - length];
                // Expand the lag-three and lag-two feedback denominators as geometric series.
                var real = prices.Select((_, i) => Enumerable.Range(0, i / 3 + 1).Sum(k =>
                    1.25 * Math.Pow(inGain, k) * (Difference(i - 3 * k - 4) - inGain * Difference(i - 3 * k - 2)))).ToArray();
                var quad = prices.Select((_, i) => Enumerable.Range(0, i / 2 + 1).Sum(k =>
                    Math.Pow(quadGain, k) * (Difference(i - 2 * k - 2) - quadGain * Difference(i - 2 * k)))).ToArray();
                if (squelch)
                {
                    real = prices.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => .33 * Math.Pow(.67, i - j) * Difference(j - 3))).ToArray();
                    quad = prices.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => .2 * Math.Pow(.8, i - j) * (.75 * (Difference(j) - Difference(j - length))
                            + .25 * (Difference(j - 2) - Difference(j - 4))))).ToArray();
                }
                if (!phase) return Outputs(("Quad", quad), ("Inphase", real));
                var angles = prices.Select((_, i) =>
                {
                    var denominator = real[i] + (i == 0 ? 0 : real[i - 1]);
                    var numerator = quad[i] + (i == 0 ? 0 : quad[i - 1]);
                    var angle = denominator == 0 ? 0 : Math.Atan(Math.Abs(numerator / denominator)) * 180 / Math.PI;
                    return real[i] < 0 ? (quad[i] > 0 ? 180 - angle : quad[i] < 0 ? 180 + angle : angle)
                        : real[i] > 0 && quad[i] < 0 ? 360 - angle : angle;
                }).ToArray();
                var advances = angles.Select((a, i) =>
                {
                    var previous = i == 0 ? 0 : angles[i - 1];
                    return Clamp(previous - a + (previous < 90 && a > 270 ? 360 : 0), 1, 60);
                }).ToArray();
                var periods = advances.Select((_, i) =>
                {
                    double accumulated = 0;
                    for (var lag = 0; lag <= Math.Min(horizon, i); lag++)
                    {
                        accumulated += advances[i - lag];
                        if (accumulated > 360) return (double)lag;
                    }
                    return 0;
                }).ToArray();
                var cycle = periods.Select((_, i) => Enumerable.Range(0, i + 1)
                    .Sum(j => .25 * Math.Pow(.75, i - j) * periods[j])).ToArray();
                return Outputs((phaseKey, squelch ? cycle.Select(v => v < Integer(options, "Length2", 20) ? 0d : 1d).ToArray() : cycle));
            });
        }
        if (name is not (IndicatorName.EhlersHilbertTransformer or IndicatorName.EhlersHilbertTransformerIndicator
            or IndicatorName.EhlersClassicHilbertTransformer or IndicatorName.EhlersRoofingFilterV2)) return null;
        var roofingOnly = name == IndicatorName.EhlersRoofingFilterV2;
        var upper = Integer(options, roofingOnly ? "UpperLength" : "Length1", 48);
        var lower = Integer(options, roofingOnly ? "LowerLength" : "Length2", 20);
        return new(roofingOnly ? "Erf" : "Real", roofingOnly ? new[] { "Erf" } : new[] { "Real", "Imag" }, bars =>
        {
            var roof = HilbertRoofingTrajectory(Closes(bars), upper, lower);
            if (roofingOnly) return Outputs(("Erf", roof));
            double[] Normalize(double[] values) => values.Select((value, i) =>
            {
                var peak = Enumerable.Range(0, i + 1).Max(j => Math.Pow(.991, i - j) * Math.Abs(values[j]));
                return peak == 0 ? 0 : value / peak;
            }).ToArray();
            var real = Normalize(roof);
            double[] imag;
            if (name == IndicatorName.EhlersClassicHilbertTransformer)
            {
                double[] weights = [ .091, .111, .143, .2, .333, 1, -1, -.333, -.2, -.143, -.111, -.091 ];
                imag = real.Select((_, i) => Enumerable.Range(0, Math.Min(weights.Length, i / 2 + 1))
                    .Sum(tap => weights[tap] * real[i - 2 * tap]) / 1.865).ToArray();
            }
            else
            {
                imag = Normalize(real.Select((v, i) => v - (i == 0 ? 0 : real[i - 1])).ToArray());
                if (name == IndicatorName.EhlersHilbertTransformerIndicator)
                {
                    var angle = 1.414 * Math.PI / Integer(options, "Length3", 10);
                    imag = HilbertLowPass(imag, Math.Exp(-angle), angle);
                }
            }
            return Outputs(("Real", real), ("Imag", imag));
        });
    }

    private static double[] HilbertRoofingTrajectory(double[] prices, int upper, int lower)
    {
        // Preserve V2's published gain and clamps. Expand the repeated pole's impulse
        // weights after applying the numerator's DC and Nyquist zeros.
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / upper, .99);
        var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        var kernel = Enumerable.Range(0, prices.Length).Select(age => (age + 1) * Math.Pow(pole, age) * pole * pole / 4).ToArray();
        var drive = prices.Select((v, i) => ((v - (i < 1 ? 0 : prices[i - 1]))
            - ((i < 2 ? 0 : prices[i - 2]) - (i < 3 ? 0 : prices[i - 3]))) / 2).ToArray();
        var highPass = prices.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => drive[j] * kernel[i - j])).ToArray();
        var lowAngle = Math.Sqrt(2) * Math.PI / lower;
        var lowPole = Complex.FromPolarCoordinates(Math.Exp(-lowAngle), Math.Min(lowAngle, .99));
        var gain = ((1 - lowPole) * (1 - Complex.Conjugate(lowPole))).Real;
        Complex first = 0, second = 0;
        var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            first = gain * highPass[i] + lowPole * first;
            second = first + Complex.Conjugate(lowPole) * second;
            result[i] = second.Real;
        }
        return result;
    }

    private static double[] HilbertLowPass(double[] values, double radius, double angle, bool averageInput = true)
    {
        // Factor the real second-order denominator into conjugate first-order sections.
        var pole = Complex.FromPolarCoordinates(radius, angle);
        var gain = ((1 - pole) * (1 - Complex.Conjugate(pole))).Real;
        Complex first = 0, second = 0;
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            first = gain * (averageInput ? (values[i] + (i == 0 ? 0 : values[i - 1])) / 2 : values[i]) + pole * first;
            second = first + Complex.Conjugate(pole) * second;
            result[i] = second.Real;
        }
        return result;
    }
}
