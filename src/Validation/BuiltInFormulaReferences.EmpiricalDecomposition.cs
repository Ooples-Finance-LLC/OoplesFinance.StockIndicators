using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> EmpiricalDecompositionValues(IReadOnlyList<Bar> bars, int length, int extremaLength, double delta, double fraction, MovingAvgType kind)
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
        length = Math.Max(1, length); var period = 2L * length; var zero = new ReferenceFraction(0);
        var cosine = Math.Cos(Math.Max(.01, Math.Min(.99, 4 * Math.PI * delta / length))); var reciprocal = 1 / cosine;
        var decay = Math.Max(.01, Math.Min(.99, reciprocal - Math.Sqrt(reciprocal * reciprocal - 1)));
        var feedback = R(Math.Cos(Math.Max(.01, Math.Min(.99, 2 * Math.PI / length))) * (1 + decay)); var drive = R(.5 * (1 - decay));
        extremaLength = Math.Max(1, extremaLength); var band = new ReferenceFraction[bars.Count]; var peaks = new ReferenceFraction[bars.Count]; var valleys = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            band[i] = i < 2 ? zero : Round(drive * (R(bars[i].Close) - R(bars[i - 2].Close)) + feedback * band[i - 1] - R(decay) * band[i - 2]);
            var previous = i == 0 ? zero : band[i - 1]; var older = i < 2 ? zero : band[i - 2];
            peaks[i] = previous.CompareTo(band[i]) > 0 && previous.CompareTo(older) > 0 ? previous : i == 0 ? zero : peaks[i - 1];
            valleys[i] = previous.CompareTo(band[i]) < 0 && previous.CompareTo(older) < 0 ? previous : i == 0 ? zero : valleys[i - 1];
        }
        double[] Average(ReferenceFraction[] values, long count, double factor)
        {
            var result = new double[values.Length]; var mass = kind == MovingAvgType.WeightedMovingAverage ? count % 2 == 0 ? (count / 2) * (count + 1) : count * ((count + 1) / 2) : count;
            for (var i = 0; i < values.Length; i++)
            {
                if (kind == MovingAvgType.SimpleMovingAverage && i + 1L < count) continue;
                var sum = zero; for (var lag = 0; lag <= i && lag < count; lag++) sum += values[i - lag] * new ReferenceFraction(kind == MovingAvgType.WeightedMovingAverage ? count - lag : 1);
                result[i] = (sum * R(factor) / new ReferenceFraction(mass)).ToDouble();
            }
            return result;
        }
        return new Dictionary<string, double[]> { { "Trend", Average(band, period, 1) }, { "Peak", Average(peaks, extremaLength, fraction) }, { "Valley", Average(valleys, extremaLength, fraction) } };
    }
}
