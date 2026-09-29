using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MamaValues(double[] prices, double fast = .5, double slow = .05)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        ReferenceFraction Round(ReferenceFraction value)
        {
            if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
            while (value.Abs().CompareTo(lower) < 0) { value *= chunk; scale /= chunk; }
            while (value.Abs().CompareTo(upper) >= 0) { value /= chunk; scale *= chunk; }
            return R(value.ToDouble()) * scale;
        }
        var n = prices.Length; var smooth = new ReferenceFraction[n]; var detrended = new ReferenceFraction[n]; var inphase = new ReferenceFraction[n]; var quadrature = new ReferenceFraction[n]; var phasorReal = new ReferenceFraction[n]; var phasorImaginary = new ReferenceFraction[n]; var real = new ReferenceFraction[n]; var imaginary = new ReferenceFraction[n]; var mama = new ReferenceFraction[n]; var fama = new ReferenceFraction[n]; var periods = new double[n]; var smoothedPeriods = new double[n]; var phases = new double[n]; var signals = new Signal[n];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? R(0) : values[i];
        double Previous(double[] values, int i) => i < 0 ? 0 : values[i];
        ReferenceFraction Fir(ReferenceFraction[] values, int i, double correction) => Round(Round(R(.0962) * At(values, i) + R(.5769) * At(values, i - 2) - R(.5769) * At(values, i - 4) - R(.0962) * At(values, i - 6)) * R(correction));
        for (var i = 0; i < n; i++)
        {
            smooth[i] = Round(Enumerable.Range(0, Math.Min(4, i + 1)).Select(lag => R(4 - lag) * R(prices[i - lag])).Aggregate(R(0), (sum, value) => sum + value) / R(10));
            var previousPeriod = Previous(periods, i - 1); var correction = .075 * previousPeriod + .54; detrended[i] = Fir(smooth, i, correction); inphase[i] = At(detrended, i - 3); quadrature[i] = Fir(detrended, i, correction);
            var ji = Fir(inphase, i, correction); var jq = Fir(quadrature, i, correction); phasorReal[i] = Round(R(.2) * (inphase[i] - jq) + R(.8) * At(phasorReal, i - 1)); phasorImaginary[i] = Round(R(.2) * (quadrature[i] + ji) + R(.8) * At(phasorImaginary, i - 1));
            real[i] = Round(R(.2) * (phasorReal[i] * At(phasorReal, i - 1) + phasorImaginary[i] * At(phasorImaginary, i - 1)) + R(.8) * At(real, i - 1)); imaginary[i] = Round(R(.2) * (phasorReal[i] * At(phasorImaginary, i - 1) - phasorImaginary[i] * At(phasorReal, i - 1)) + R(.8) * At(imaginary, i - 1));
            var advance = real[i].Sign == 0 ? 0 : Math.Atan((imaginary[i] / real[i]).ToDouble()); var measured = advance == 0 ? 0 : 2 * Math.PI / advance; if (previousPeriod != 0) measured = Math.Min(1.5 * previousPeriod, Math.Max(.67 * previousPeriod, measured)); measured = Math.Min(50, Math.Max(6, measured)); periods[i] = (R(.2) * R(measured) + R(.8) * R(previousPeriod)).ToDouble(); smoothedPeriods[i] = (R(.33) * R(periods[i]) + R(.67) * R(Previous(smoothedPeriods, i - 1))).ToDouble();
            phases[i] = inphase[i].Sign == 0 ? 0 : Math.Atan((quadrature[i] / inphase[i]).ToDouble()) * (180 / Math.PI); var alpha = Math.Max(slow, fast / Math.Max(1, Previous(phases, i - 1) - phases[i])); mama[i] = Round(R(alpha) * R(prices[i]) + (R(1) - R(alpha)) * At(mama, i - 1)); fama[i] = Round(R(alpha) / R(2) * mama[i] + (R(1) - R(alpha) / R(2)) * At(fama, i - 1));
            var current = mama[i] - fama[i]; var previous = At(mama, i - 1) - At(fama, i - 1); signals[i] = current.Sign > 0 ? current.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? current.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        double[] Values(ReferenceFraction[] values) => values.Select(v => v.ToDouble()).ToArray();
        return (new Dictionary<string, double[]> { { "Fama", Values(fama) }, { "Mama", Values(mama) }, { "I1", Values(inphase) }, { "Q1", Values(quadrature) }, { "SmoothPeriod", smoothedPeriods }, { "Smooth", Values(smooth) }, { "Real", Values(real) }, { "Imag", Values(imaginary) } }, signals);
    }
}
