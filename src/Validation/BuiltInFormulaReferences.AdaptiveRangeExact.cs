using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveRangeV1Values(IReadOnlyList<Bar> bars, double fraction, bool commodity, double constant = .015, bool selected = false)
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
        var periods = MamaValues(bars.Select(b => b.Close).ToArray()).Outputs["SmoothPeriod"]; var source = bars.Select(b => commodity && !selected ? ExactPriceMean(b.High, b.Low, b.Close) : b.Close).ToArray(); var values = new ReferenceFraction[bars.Count]; var averages = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction Prior(int i) => i < 0 ? R(0) : averages[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var desired = Math.Ceiling(fraction * periods[i]); var length = desired <= 0 ? 0 : desired >= int.MaxValue - 1d ? int.MaxValue - 1 : (int)desired;
            if (commodity)
            {
                length = Math.Max(1, length); var count = Math.Min(length, i + 1); var sample = Enumerable.Range(i - count + 1, count).Select(j => R(source[j])).ToArray(); var mean = sample.Aggregate(R(0), (a, b) => a + b) / R(length); var deviation = (sample.Select(v => (v - mean).Abs()).Aggregate(R(0), (a, b) => a + b) + R(length - count) * mean.Abs()) / R(length); values[i] = deviation.Sign == 0 ? R(0) : Round((R(source[i]) - mean) / (R(constant) * deviation));
            }
            else
            {
                var sample = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(length, i + 1)).Select(j => bars[j]).ToArray(); var high = sample.Select(b => b.High).Append(bars[i].High).Max(); var low = sample.Select(b => b.Low).Append(bars[i].Low).Min(); if (length > i + 1) { high = Math.Max(0, high); low = Math.Min(0, low); } var range = R(high) - R(low); values[i] = range.Sign == 0 ? R(0) : Round(R(100) * (R(source[i]) - R(low)) / range);
            }
            var alpha = Math.Max(.01, Math.Min(.99, 2d / ((commodity ? Math.Ceiling(periods[i]) : length) + 1))); averages[i] = Round(R(alpha) * values[i] + R(1 - alpha) * Prior(i - 1)); var slope = averages[i] - Prior(i - 1); var before = Prior(i - 1) - Prior(i - 2); var lowerBound = R(commodity ? -100 : 30); var upperBound = R(commodity ? 100 : 70);
            signals[i] = slope.Sign > 0 && slope.CompareTo(before) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(before) < 0 ? Signal.StrongSell : slope.Sign > 0 || (Prior(i - 1).CompareTo(lowerBound) < 0 && averages[i].CompareTo(lowerBound) > 0) ? Signal.Buy : slope.Sign < 0 || (Prior(i - 1).CompareTo(upperBound) > 0 && averages[i].CompareTo(upperBound) < 0) ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { commodity ? "Eacci" : "Easi", values.Select(v => v.ToDouble()).ToArray() }, { "Signal", averages.Select(v => v.ToDouble()).ToArray() } }, signals);
    }
}
