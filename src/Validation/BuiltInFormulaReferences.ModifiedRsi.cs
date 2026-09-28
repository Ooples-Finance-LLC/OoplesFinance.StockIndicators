using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> ModifiedRsiValues(IReadOnlyList<Bar> bars, int upperPeriod, int lowerPeriod, int length)
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
        upperPeriod = Math.Max(1, upperPeriod); lowerPeriod = Math.Max(1, lowerPeriod); length = Math.Max(1, length); var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / upperPeriod, .99); var pole = R(Math.Cos(angle) / (1 + Math.Sin(angle))); var highGain = pole * pole / new ReferenceFraction(4);
        var lowAngle = Math.Sqrt(2) * Math.PI / lowerPeriod; var radius = R(Math.Exp(-lowAngle)); var feedback1 = two * radius * R(Math.Cos(Math.Min(lowAngle, .99))); var feedback2 = zero - radius * radius; var gain = one - feedback1 - feedback2;
        var high1 = new ReferenceFraction[bars.Count]; var high2 = new ReferenceFraction[bars.Count]; var roof = new ReferenceFraction[bars.Count]; var changes = new ReferenceFraction[bars.Count]; var line = new ReferenceFraction[bars.Count]; var signal = new ReferenceFraction[bars.Count]; var ratios = new ReferenceFraction[bars.Count]; var valid = new bool[bars.Count];
        ReferenceFraction Price(int i) => i < 0 ? zero : R(bars[i].Close);
        ReferenceFraction Prior(ReferenceFraction[] values, int i) => i < 0 ? zero : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var drive = Round((Price(i) - Price(i - 1) - Price(i - 2) + Price(i - 3)) / two);
            high1[i] = Round(highGain * drive + pole * Prior(high1, i - 1)); high2[i] = Round(high1[i] + pole * Prior(high2, i - 1));
            roof[i] = Round(gain * high2[i] + feedback1 * Prior(roof, i - 1) + feedback2 * Prior(roof, i - 2)); changes[i] = Round(roof[i] - Prior(roof, i - 1));
            var gains = zero; var total = zero; for (var j = Math.Max(0, i - length + 1); j <= i; j++) { if (changes[j].Sign > 0) gains += changes[j]; total += changes[j].Abs(); }
            valid[i] = total.Sign != 0; ratios[i] = valid[i] ? R((gains / total).ToDouble()) : zero;
            line[i] = i > 0 && valid[i] && valid[i - 1] ? Round(gain * (ratios[i] + ratios[i - 1]) / two + feedback1 * line[i - 1] + feedback2 * Prior(line, i - 2)) : zero;
            signal[i] = Round(gain * (line[i] + Prior(line, i - 1)) / two + feedback1 * Prior(signal, i - 1) + feedback2 * Prior(signal, i - 2));
        }
        return new Dictionary<string, double[]> { { "Emrsi", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
