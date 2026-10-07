using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] EarlyOnsetValues(IReadOnlyList<Bar> bars, int low, int high, double k)
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
        var period = Math.Max(1, high) * Math.Sqrt(2); var tangent = Math.Tan(Math.PI / period); var alpha = R(period <= 2 ? 2 : 2 * tangent / (1 + tangent));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0); var pole = one - alpha; var gain = (one - alpha / two) * (one - alpha / two);
        var angle = Math.Max(.01, Math.Min(.99, Math.Sqrt(2) * Math.PI / Math.Max(1, low))); var radius = Math.Exp(-angle);
        var c2 = 2 * radius * Math.Cos(angle); var c3 = -radius * radius; var c1 = R(1 - c2 - c3);
        var hp = new ReferenceFraction[bars.Count]; var filtered = new ReferenceFraction[bars.Count]; var peaks = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        ReferenceFraction Price(int i) => i < 0 ? zero : R(bars[i].Close);
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? zero : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            hp[i] = Round(gain * (Price(i) - two * Price(i - 1) + Price(i - 2)) + two * pole * At(hp, i - 1) - pole * pole * At(hp, i - 2));
            filtered[i] = Round(c1 * Round((hp[i] + At(hp, i - 1)) / two) + R(c2) * At(filtered, i - 1) + R(c3) * At(filtered, i - 2));
            var decayed = Round(R(.991) * At(peaks, i - 1)); peaks[i] = filtered[i].Abs().CompareTo(decayed) > 0 ? filtered[i].Abs() : decayed;
            var ratio = peaks[i].Sign == 0 ? zero : R((filtered[i] / peaks[i]).ToDouble()); var denominator = one + R(k) * ratio;
            result[i] = denominator.Sign == 0 ? 0 : ((ratio + R(k)) / denominator).ToDouble();
        }
        return result;
    }
}
