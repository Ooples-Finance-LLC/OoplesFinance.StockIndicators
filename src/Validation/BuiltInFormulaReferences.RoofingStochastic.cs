using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoofingStochasticValues(IReadOnlyList<Bar> bars, int high, int low, int length, MovingAvgType kind, bool modified)
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
        high = Math.Max(1, high); low = Math.Max(1, low); length = Math.Max(1, length); var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        ReferenceFraction Prior(ReferenceFraction[] values, int i) => i < 0 ? zero : values[i];
        ReferenceFraction Price(int i) => i < 0 ? zero : R(bars[i].Close);
        ReferenceFraction[] Average(ReferenceFraction[] input, int period)
        {
            var values = new ReferenceFraction[input.Length]; var angle = Math.Sqrt(2) * Math.PI / Math.Max(2, period); var radius = R(Math.Exp(-angle)); var feedback1 = two * radius * R(Math.Cos(angle)); var feedback2 = zero - radius * radius; var gain = one - feedback1 - feedback2;
            for (var i = 0; i < values.Length; i++)
            {
                if (kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV1) values[i] = i < 3 ? input[i] : Round(gain * input[i] + feedback1 * values[i - 1] + feedback2 * values[i - 2]);
                else { var sum = zero; for (var j = 0; j < Math.Min(period, i + 1); j++) sum += new ReferenceFraction(period - j) * input[i - j]; values[i] = Round(sum / new ReferenceFraction((long)period * (period + 1L) / 2)); }
            }
            return values;
        }
        var highPeriod = high * Math.Sqrt(2); var tangent = Math.Tan(Math.PI / highPeriod); var alpha = highPeriod <= 2 ? 2 : 2 * tangent / (1 + tangent); var pole = R(1 - alpha); var highGain = R(Math.Pow(1 - alpha / 2, 2));
        var first = new ReferenceFraction[bars.Count]; var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++) { var drive = Round((Price(i) - Price(i - 1) - Price(i - 2) + Price(i - 3)) / two); first[i] = Round(highGain * drive + pole * Prior(first, i - 1)); raw[i] = Round(first[i] + pole * Prior(raw, i - 1)); }
        var roof = Average(raw, low); var ranks = new ReferenceFraction[bars.Count]; var arguments = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = roof.Skip(Math.Max(0, i - Math.Max(2, length) + 1)).Take(Math.Min(i + 1, Math.Max(2, length))).OrderBy(v => v).ToArray(); var range = window[window.Length - 1] - window[0];
            ranks[i] = range.Sign == 0 ? zero : R((new ReferenceFraction(modified ? 100 : 1) * (roof[i] - window[0]) / range).ToDouble()); arguments[i] = Round((ranks[i] + Prior(ranks, i - 1)) / two);
        }
        if (!modified) return Average(arguments, length).Select(v => v.ToDouble()).ToArray();
        var finalAngle = Math.Sqrt(2) * Math.PI / high; var finalRadius = R(Math.Exp(-finalAngle)); var finalFirst = two * finalRadius * R(Math.Cos(Math.Min(finalAngle, .99))); var finalSecond = zero - finalRadius * finalRadius; var finalGain = one - finalFirst - finalSecond; var result = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++) result[i] = Round(finalGain * arguments[i] + finalFirst * Prior(result, i - 1) + finalSecond * Prior(result, i - 2));
        return result.Select(v => v.ToDouble()).ToArray();
    }
}
