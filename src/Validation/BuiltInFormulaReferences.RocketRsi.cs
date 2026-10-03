using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RocketRsiValues(IReadOnlyList<Bar> bars, int first, int second, MovingAvgType kind, double mult)
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
        first = Math.Max(1, first); second = Math.Max(1, second); var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var momentum = new ReferenceFraction[bars.Count]; var argument = new ReferenceFraction[bars.Count]; var drive = new ReferenceFraction[bars.Count]; var smooth = new ReferenceFraction[bars.Count]; var changes = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        var angle = Math.Sqrt(2) * Math.PI / Math.Max(2, second); var radius = R(Math.Exp(-angle)); var feedback1 = two * radius * R(Math.Cos(angle)); var feedback2 = zero - radius * radius; var gain = one - feedback1 - feedback2; var ratio = 0d;
        ReferenceFraction Prior(ReferenceFraction[] values, int i) => i < 0 ? zero : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            momentum[i] = i < first - 1 ? zero : Round(R(bars[i].Close) - R(bars[i - first + 1].Close)); argument[i] = Round((momentum[i] + Prior(momentum, i - 1)) / two);
            if (kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV2)
            {
                drive[i] = Round(argument[i] - Prior(argument, i - 1)); smooth[i] = Round(gain * (drive[i] + Prior(drive, i - 1)) / two + feedback1 * Prior(smooth, i - 1) + feedback2 * Prior(smooth, i - 2)); changes[i] = smooth[i];
            }
            else
            {
                var sum = zero; for (var j = 0; j < Math.Min(second, i + 1); j++) sum += new ReferenceFraction(second - j) * argument[i - j]; smooth[i] = Round(sum / new ReferenceFraction((long)second * (second + 1L) / 2)); changes[i] = Round(smooth[i] - Prior(smooth, i - 1));
            }
            var signed = zero; var absolute = zero; for (var j = Math.Max(0, i - first + 1); j <= i; j++) { signed += changes[j]; absolute += changes[j].Abs(); }
            if (absolute.Sign != 0) ratio = Math.Max(-.999, Math.Min(.999, (signed / absolute).ToDouble()));
            // A short atanh series avoids cancellation for small ratios independently of the production logarithm correction.
            var fisher = Math.Abs(ratio) < .01 ? ratio * (1 + ratio * ratio * (1d / 3 + ratio * ratio * (1d / 5 + ratio * ratio * (1d / 7 + ratio * ratio / 9)))) : .5 * (Math.Log(1 + ratio) - Math.Log(1 - ratio));
            result[i] = fisher * mult;
        }
        return result;
    }
}
