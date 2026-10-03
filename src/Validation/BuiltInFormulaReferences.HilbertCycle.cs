using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (double[] Values, Signal[] Signals) HilbertCycleValues(IReadOnlyList<Bar> bars, int upper, int lower, int minimum, int horizon, int mode)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upperBound = new ReferenceFraction(BigInteger.One << 256); var lowerBound = new ReferenceFraction(1) / upperBound;
        ReferenceFraction Round(ReferenceFraction value)
        {
            if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
            while (value.Abs().CompareTo(lowerBound) < 0) { value *= chunk; scale /= chunk; }
            while (value.Abs().CompareTo(upperBound) >= 0) { value /= chunk; scale *= chunk; }
            return R(value.ToDouble()) * scale;
        }
        upper = Math.Max(1, upper); lower = Math.Max(1, lower); minimum = Math.Max(1, minimum); horizon = Math.Max(1, horizon);
        var normalized = HilbertTransformerValues(bars, upper, lower, 1, false); var real = normalized.Outputs["Real"]; var imaginary = normalized.Outputs["Imag"]; var phases = new double[bars.Count]; var advances = new double[bars.Count]; var periods = new double[bars.Count]; var output = new ReferenceFraction[bars.Count];
        var angle = 1.414 * Math.PI / lower; var radius = R(Math.Exp(-angle)); var cosine = R(Math.Cos(angle)); var feedback1 = R(2) * radius * cosine; var feedback2 = R(-1) * radius * radius; var gain = R(1) - feedback1 - feedback2;
        double Previous(double[] values, int i) => i < 0 ? 0 : values[i];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? R(0) : values[i];
        double Bound(double value) => Math.Min(upper, Math.Max(minimum, value));
        for (var i = 0; i < bars.Count; i++)
        {
            if (mode == 2)
            {
                phases[i] = Math.Atan2(imaginary[i], real[i]) * (180 / Math.PI); if (phases[i] < 0) phases[i] += 360;
                var previousPhase = Previous(phases, i - 1); advances[i] = Bound(previousPhase < 90 && phases[i] > 270 ? 360 + previousPhase - phases[i] : previousPhase - phases[i]);
                double total = 0; for (var count = 1; count <= Math.Min(horizon, i + 1); count++) { total += advances[i - count + 1]; if (total >= 360 - 3.6e-7) { periods[i] = count; break; } }
                if (periods[i] == 0) periods[i] = Previous(periods, i - 1);
            }
            else
            {
                var first = R(real[i]) * R(Previous(imaginary, i - 1)); var second = R(imaginary[i]) * R(Previous(real, i - 1)); var determinant = first - second;
                if (mode == 1) { var dot = R(real[i]) * R(Previous(real, i - 1)) + R(imaginary[i]) * R(Previous(imaginary, i - 1)); var advance = Math.Abs(Math.Atan2(-determinant.ToDouble(), dot.ToDouble())); periods[i] = Bound(advance == 0 ? 0 : 2 * Math.PI / advance); }
                else { var magnitude = first.Abs() + second.Abs(); var resolved = magnitude.Sign != 0 && Math.Abs((determinant / magnitude).ToDouble()) > 1e-12; var energy = R(real[i]) * R(real[i]) + R(imaginary[i]) * R(imaginary[i]); periods[i] = Bound(resolved ? (2 * Math.PI) * (energy / determinant).ToDouble() : 0); }
            }
            output[i] = Round(gain * (R(periods[i]) + R(Previous(periods, i - 1))) / R(2) + feedback1 * At(output, i - 1) + feedback2 * At(output, i - 2));
        }
        return (output.Select(v => v.ToDouble()).ToArray(), normalized.Signals);
    }
}
