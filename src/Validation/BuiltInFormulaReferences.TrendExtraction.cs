using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> TrendExtractionValues(IReadOnlyList<Bar> bars, int length, double delta, MovingAvgType kind)
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
        var band = new ReferenceFraction[bars.Count]; var trend = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            band[i] = i < 2 ? zero : Round(drive * (R(bars[i].Close) - R(bars[i - 2].Close)) + feedback * band[i - 1] - R(decay) * band[i - 2]);
            var sum = zero; for (var lag = 0; lag <= i && lag < period; lag++) sum += band[i - lag] * new ReferenceFraction(kind == MovingAvgType.WeightedMovingAverage ? period - lag : 1);
            trend[i] = kind == MovingAvgType.SimpleMovingAverage && i + 1L < period ? 0 : (sum / new ReferenceFraction(kind == MovingAvgType.WeightedMovingAverage ? (period / 2) * (period + 1) : period)).ToDouble();
        }
        return new Dictionary<string, double[]> { { "Trend", trend }, { "Bp", band.Select(v => v.ToDouble()).ToArray() } };
    }
}
