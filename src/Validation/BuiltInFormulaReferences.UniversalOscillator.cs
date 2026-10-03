using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> UniversalOscillatorOutputs(IReadOnlyList<Bar> bars, int length, int kind, int signalLength = 9)
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
        var angle = 1.414 * Math.PI / Math.Max(1, length); var radius = Math.Exp(-Math.Max(.01, Math.Min(.99, angle)));
        var feedback1 = 2 * radius * Math.Cos(angle); var feedback2 = -radius * radius; var gain = R(1 - feedback1 - feedback2);
        var noise = new ReferenceFraction[bars.Count]; var filtered = new ReferenceFraction[bars.Count]; var peaks = new ReferenceFraction[bars.Count]; var line = new double[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            noise[i] = i < 2 ? zero : Round((R(bars[i].Close) - R(bars[i - 2].Close)) / new ReferenceFraction(2));
            var mean = Round((noise[i] + (i == 0 ? zero : noise[i - 1])) / new ReferenceFraction(2));
            filtered[i] = Round(gain * mean + R(feedback1) * (i == 0 ? zero : filtered[i - 1]) + R(feedback2) * (i < 2 ? zero : filtered[i - 2]));
            var decayed = Round(R(.991) * (i == 0 ? zero : peaks[i - 1])); peaks[i] = filtered[i].Abs().CompareTo(decayed) > 0 ? filtered[i].Abs() : decayed;
            line[i] = peaks[i].Sign == 0 ? i == 0 ? 0 : line[i - 1] : (filtered[i] / peaks[i]).ToDouble();
        }
        return new() { { "Euo", line }, { "Signal", SmoothRocBankStage(line.Select(R).ToArray(), Math.Max(1, signalLength), kind).Select(v => v.ToDouble()).ToArray() } };
    }
}
