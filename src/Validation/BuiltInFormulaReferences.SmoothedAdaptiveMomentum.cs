using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> SmoothedAdaptiveMomentumValues(IReadOnlyList<Bar> bars, int first, int second, MovingAvgType kind)
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
        second = Math.Max(2, second); var periods = AdaptiveCyberValues(bars, first, .07)["Period"]; var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        var radius = R(Math.Exp(-Math.PI / second)); var pair = new ReferenceFraction(2) * radius * R(Math.Cos(1.738 * Math.PI / second)); var real = radius * radius;
        var coefficients = new[] { pair + real, zero - (real + pair * real), real * real }; var gain = one - coefficients[0] - coefficients[1] - coefficients[2];
        var line = new ReferenceFraction[bars.Count]; var signal = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var lag = (int)Math.Ceiling(Math.Abs(periods[i] - 1)); var change = i < lag ? zero : Round(R(bars[i].Close) - R(bars[i - lag].Close)); var value = gain * change;
            for (var j = 0; j < Math.Min(3, i); j++) value += coefficients[j] * line[i - j - 1]; line[i] = Round(value);
            var total = zero;
            if (kind == MovingAvgType.WeightedMovingAverage)
            {
                for (var j = 0; j < Math.Min(second, i + 1); j++) total += new ReferenceFraction(second - j) * line[i - j];
                signal[i] = Round(total / new ReferenceFraction((long)second * (second + 1L) / 2));
            }
            else if (kind == MovingAvgType.SimpleMovingAverage)
            {
                for (var j = Math.Max(0, i - second + 1); j <= i; j++) total += line[j];
                signal[i] = i < second - 1 ? zero : Round(total / new ReferenceFraction(second));
            }
            else if (i < second) { for (var j = 0; j <= i; j++) total += line[j]; signal[i] = Round(total / new ReferenceFraction(i + 1)); }
            else signal[i] = Round((new ReferenceFraction(second - 1) * signal[i - 1] + new ReferenceFraction(2) * line[i]) / new ReferenceFraction(second + 1L));
        }
        return new Dictionary<string, double[]> { { "Esam", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
