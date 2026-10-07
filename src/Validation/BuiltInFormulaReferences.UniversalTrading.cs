using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> UniversalTradingValues(IReadOnlyList<Bar> bars, int length, int rmsLength, double parameter, MovingAvgType kind, bool snake)
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
        length = Math.Max(1, length); rmsLength = Math.Max(1, rmsLength); var zero = new ReferenceFraction(0); var lag = Math.Ceiling(parameter * length);
        var cosine = Math.Cos(Math.Max(.01, Math.Min(.99, parameter * Math.PI / length))); var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        var feedback = R(Math.Cos(Math.Max(.01, Math.Min(.99, Math.PI / length))) * (1 + decay)); var drive = R(.5 * (1 - decay));
        var raw = new ReferenceFraction[bars.Count]; var filtered = new ReferenceFraction[bars.Count]; var upperBand = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            raw[i] = snake ? i < 3 ? zero : Round(drive * (R(bars[i].Close) - R(bars[i - 2].Close)) + feedback * raw[i - 1] - R(decay) * raw[i - 2])
                : Round(R(bars[i].Close) - (i < lag ? zero : R(bars[i - (int)lag].Close)));
            var sum = zero;
            for (var j = 0; j <= i && j < length; j++)
            {
                var sine = Math.Sin(Math.PI * ((j + 1d) / (length + 1d))); var weight = kind == MovingAvgType.EhlersHannMovingAverage ? R(2 * sine * sine) : new ReferenceFraction(length - j);
                sum += weight * raw[i - j];
            }
            filtered[i] = Round(sum / new ReferenceFraction(kind == MovingAvgType.EhlersHannMovingAverage ? length + 1L : (long)length * (length + 1L) / 2));
            var energy = zero; var count = Math.Min(rmsLength, i + 1); for (var j = 0; j < count; j++) energy += filtered[i - j] * filtered[i - j];
            upperBand[i] = (energy / new ReferenceFraction(count)).SqrtToDouble();
        }
        return new Dictionary<string, double[]> { { snake ? "Erf" : "Eutf", filtered.Select(v => v.ToDouble()).ToArray() }, { "UpperBand", upperBand }, { "LowerBand", upperBand.Select(v => -v).ToArray() } };
    }
}
