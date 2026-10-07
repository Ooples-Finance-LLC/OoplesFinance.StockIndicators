using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] FramaValues(IReadOnlyList<Bar> bars, int length)
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
        length = Math.Max(2, length); length = checked(length + length % 2); var half = length / 2;
        var filtered = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var gain = 1d; var one = new ReferenceFraction(1);
        ReferenceFraction Range(int start, int count)
        { var sample = bars.Skip(start).Take(count).ToArray(); return R(sample.Max(b => b.High)) - R(sample.Min(b => b.Low)); }
        for (var i = 0; i < bars.Count; i++)
        {
            if (i >= length - 1)
            {
                var oldRange = Range(i - length + 1, half); var newRange = Range(i - half + 1, half); var full = Range(i - length + 1, length);
                if (oldRange.Sign > 0 && newRange.Sign > 0 && full.Sign > 0)
                {
                    var ratio = (new ReferenceFraction(2) * (oldRange + newRange) / full).ToDouble();
                    gain = ratio <= 1 ? 1 : Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (Math.Log(ratio) / Math.Log(2) - 1))));
                }
            }
            filtered[i] = i < length ? R(bars[i].Close) : Round(R(gain) * R(bars[i].Close) + (one - R(gain)) * filtered[i - 1]);
            output[i] = filtered[i].ToDouble();
        }
        return output;
    }
}
