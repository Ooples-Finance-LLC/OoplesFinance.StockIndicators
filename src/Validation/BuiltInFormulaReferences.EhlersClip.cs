using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> EhlersClipOutputs(IReadOnlyList<Bar> bars, int lag = 2, int length = 50, int signalLength = 22, int kind = 3)
    {
        lag = Math.Max(1, lag); length = Math.Max(1, length); var zero = new ReferenceFraction(0);
        var differences = bars.Select((b, i) => i < lag ? zero : RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - lag].Close))).ToArray();
        var clips = differences.Select((v, i) => {
            var energy = Window(differences, i, length).Aggregate(zero, (sum, d) => sum + d * d) / new ReferenceFraction(length);
            if (energy.Sign == 0) return 0d;
            var ratioSquared = new ReferenceFraction(4) * v * v / energy;
            return ratioSquared.CompareTo(new ReferenceFraction(1)) >= 0 ? v.Sign : v.Sign * ratioSquared.SqrtToDouble();
        }).ToArray();
        var line = clips.Select((v, i) => { for (var lagIndex = 1; lagIndex <= 3; lagIndex++) v += i >= lagIndex ? clips[i - lagIndex] : 0; return ReferenceFraction.FromDouble(v); }).ToArray();
        var signal = SmoothRocBankStage(line, Math.Max(1, signalLength), kind);
        return new() { { "Esci", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
