using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) HilbertTransformerValues(IReadOnlyList<Bar> bars, int upper, int lower, int smoothing, bool smooth)
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
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / Math.Max(1, upper), .99); var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        var driveGain = pole * pole / 4;
        var lowAngle = Math.Sqrt(2) * Math.PI / Math.Max(1, lower); var radius = Math.Exp(-lowAngle);
        var feedback = R(2 * radius * Math.Cos(Math.Min(lowAngle, .99))); var decay = R(-radius * radius);
        var gap = R(2 * Math.Exp(-lowAngle / 2) * Math.Sinh(lowAngle / 2)); var sine = R(Math.Sin(Math.Min(lowAngle, .99) / 2));
        var gain = R((gap * gap + new ReferenceFraction(4) * R(radius) * sine * sine).ToDouble());
        var smoothAngle = 1.414 * Math.PI / Math.Max(1, smoothing); var smoothRadius = R(Math.Exp(-smoothAngle)); var smoothCosine = R(Math.Cos(smoothAngle)); var smoothFirst = R(2) * smoothRadius * smoothCosine; var smoothSecond = R(-1) * smoothRadius * smoothRadius; var smoothGain = R(1) - smoothFirst - smoothSecond;
        var first = new ReferenceFraction[bars.Count]; var second = new ReferenceFraction[bars.Count]; var output = new ReferenceFraction[bars.Count]; var peaks = new ReferenceFraction[bars.Count]; var quadPeaks = new ReferenceFraction[bars.Count]; var real = new double[bars.Count]; var quad = new double[bars.Count]; var imaginary = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction Price(int i) => i < 0 ? R(0) : R(bars[i].Close);
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? R(0) : values[i];
        double Previous(double[] values, int i) => i < 0 ? 0 : values[i];
        ReferenceFraction Max(ReferenceFraction left, ReferenceFraction right) => left.CompareTo(right) > 0 ? left : right;
        for (var i = 0; i < bars.Count; i++)
        {
            var drive = Round(((Price(i) - Price(i - 1)) - (Price(i - 2) - Price(i - 3))) / new ReferenceFraction(2));
            first[i] = Round(R(driveGain) * drive + R(pole) * At(first, i - 1));
            second[i] = Round(first[i] + R(pole) * At(second, i - 1));
            output[i] = Round(gain * second[i] + feedback * At(output, i - 1) + decay * At(output, i - 2));
            peaks[i] = Max(Round(R(.991) * At(peaks, i - 1)), output[i].Abs()); real[i] = peaks[i].Sign == 0 ? 0 : (output[i] / peaks[i]).ToDouble();
            var delta = Round(R(real[i]) - R(Previous(real, i - 1))); quadPeaks[i] = Max(Round(R(.991) * At(quadPeaks, i - 1)), delta.Abs()); quad[i] = quadPeaks[i].Sign == 0 ? 0 : (delta / quadPeaks[i]).ToDouble();
            imaginary[i] = smooth ? Round(smoothGain * (R(quad[i]) + R(Previous(quad, i - 1))) / R(2) + smoothFirst * At(imaginary, i - 1) + smoothSecond * At(imaginary, i - 2)) : R(quad[i]);
            var current = smooth ? imaginary[i] - R(quad[i]) : R(real[i]) - R(Previous(real, i - 1)); var previous = smooth ? At(imaginary, i - 1) - R(Previous(quad, i - 1)) : R(Previous(real, i - 1)) - R(Previous(real, i - 2));
            signals[i] = current.Sign > 0 ? current.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? current.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Real", real }, { "Imag", imaginary.Select(v => v.ToDouble()).ToArray() } }, signals);
    }
}
