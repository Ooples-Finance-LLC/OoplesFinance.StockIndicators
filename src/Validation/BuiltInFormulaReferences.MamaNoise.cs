using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MamaNoiseValues(IReadOnlyList<Bar> bars, int length, int mode)
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
        double LogRatio(ReferenceFraction ratio)
        {
            if (ratio.Sign <= 0) return 0; var shift = 0;
            while (ratio.CompareTo(lower) < 0) { ratio *= chunk; shift -= 512; }
            while (ratio.CompareTo(upper) >= 0) { ratio /= chunk; shift += 512; }
            return Math.Log10(ratio.ToDouble()) + shift * Math.Log10(2);
        }
        length = Math.Max(1, length); var exact = new Dictionary<string, ReferenceFraction[]>(); var mama = MamaValues(bars.Select(b => b.Close).ToArray(), exact: exact).Outputs; var periods = mama["SmoothPeriod"]; var smooth = mama["Smooth"]; var quadrature = new ReferenceFraction[bars.Count]; var inphase = new ReferenceFraction[bars.Count]; var ranges = new ReferenceFraction[bars.Count]; var distances = new ReferenceFraction[bars.Count]; var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction At(ReferenceFraction[] array, int i) => i < 0 ? R(0) : array[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var range = Round(R(bars[i].High) - R(bars[i].Low)); ReferenceFraction numerator, denominator;
            if (mode == 2)
            {
                quadrature[i] = Round(Round(R(smooth[i]) - R(i < 2 ? 0 : smooth[i - 2])) * R(.5 * (.1759 * periods[i] + .4607))); var count = Math.Max(1, (int)Math.Ceiling(periods[i] / 2)); inphase[i] = Round(R(1.57) * Enumerable.Range(0, Math.Min(count, i + 1)).Select(lag => quadrature[i - lag]).Aggregate(R(0), (a, b) => a + b) / R(count)); numerator = inphase[i] * inphase[i] + quadrature[i] * quadrature[i]; ranges[i] = Round(R(.025) * range * range + R(.9) * At(ranges, i - 1)); denominator = ranges[i]; distances[i] = R(bars[i].Close) - R(smooth[i]);
            }
            else
            {
                ranges[i] = Round(R(.1) * range + R(.9) * At(ranges, i - 1)); denominator = ranges[i] * ranges[i]; numerator = mode == 0 ? exact["Real"][i] + exact["Imag"][i] : exact["I1"][i] * exact["I1"][i] + exact["Q1"][i] * exact["Q1"][i]; distances[i] = R(bars[i].Close) - exact["Mama"][i]; inphase[i] = quadrature[i] = R(0);
            }
            var log = numerator.Sign <= 0 || denominator.Sign <= 0 ? 0 : LogRatio(numerator / denominator); var level = 10 * log + (mode == 2 ? 0 : length); values[i] = mode == 1 && ranges[i].Sign <= 0 ? 0 : (R(mode == 2 ? .33 : .25) * R(level) + R(mode == 2 ? .67 : .75) * R(i == 0 ? 0 : values[i - 1])).ToDouble();
            var difference = distances[i]; var previous = At(distances, i - 1); signals[i] = values[i] < length ? Signal.None : difference.Sign > 0 ? difference.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy : difference.Sign < 0 ? difference.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        var outputs = new Dictionary<string, double[]> { { "Esnr", values } }; if (mode == 2) { outputs["I3"] = inphase.Select(v => v.ToDouble()).ToArray(); outputs["Q3"] = quadrature.Select(v => v.ToDouble()).ToArray(); outputs["SmoothPeriod"] = periods; } return (outputs, signals);
    }
}
