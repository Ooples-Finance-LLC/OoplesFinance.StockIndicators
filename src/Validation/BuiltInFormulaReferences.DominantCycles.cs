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
        return new(key, new[] { key }, bars =>
        {
            double[] Normalize(double[] input) => input.Select((value, i) =>
            {
                var peak = Enumerable.Range(0, i+1).Max(j => Math.Pow(.991, i-j)*Math.Abs(input[j]));
                return peak == 0 ? 0 : value/peak;
            }).ToArray();
            var real = Normalize(HilbertRoofingTrajectory(Closes(bars), upper, lower));
            var imaginary = Normalize(real.Select((v, i) => v-(i == 0 ? 0 : real[i-1])).ToArray());
            var vectors = real.Select((v, i) => new Complex(v, imaginary[i])).ToArray();
            var measurements = new double[bars.Count];
            double Bound(double value) => Math.Min(upper, Math.Max(minimum, value));
            if (phaseAccumulation)
            {
                var angles = vectors.Select(v => (v.Phase*180/Math.PI+360)%360).ToArray();
                var advances = angles.Select((angle, i) =>
                {
                    var previous = i == 0 ? 0 : angles[i-1];
                    return Bound(previous-angle+(previous < 90 && angle > 270 ? 360 : 0));
                }).ToArray();
                for (var i = 0; i < bars.Count; i++)
                {
                    var history = Window(advances, i, Integer(options, "Length4")).Reverse().ToArray();
                    var crossing = Enumerable.Range(1, history.Length)
                        .FirstOrDefault(count => history.Take(count).Sum() >= 360-3.6e-7);
                    measurements[i] = crossing != 0 ? crossing : i == 0 ? 0 : measurements[i-1];
                }
            }
            else for (var i = 0; i < bars.Count; i++)
            {
                var previous = i == 0 ? Complex.Zero : vectors[i-1];
                var product = Complex.Conjugate(vectors[i])*previous;
                double period;
                if (name == IndicatorName.EhlersHomodyneDominantCycle)
                {
                    var advance = Math.Abs(product.Phase);
                    period = advance == 0 ? 0 : 2*Math.PI/advance;
                }
                else
                    period = Math.Abs(product.Imaginary) <= 1e-12*(Math.Abs(real[i]*previous.Imaginary)+Math.Abs(imaginary[i]*previous.Real)) ? 0 : 2*Math.PI*(real[i]*real[i]+imaginary[i]*imaginary[i])/product.Imaginary;
                measurements[i] = Bound(period);
            }
            var angle = 1.414*Math.PI/lower;
            return Outputs((key, HilbertLowPass(measurements, Math.Exp(-angle), angle)));
        });
    }
}
