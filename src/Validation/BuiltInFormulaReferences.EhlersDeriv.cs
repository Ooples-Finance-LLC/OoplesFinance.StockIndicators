using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> EhlersDerivOutputs(IReadOnlyList<Bar> bars, int length = 2, int signalLength = 8, int kind = 3)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0);
        var differences = bars.Select((b, i) => i < length ? zero : RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - length].Close))).ToArray();
        var line = differences.Select((v, i) => {
            for (var lag = 1; lag <= 3; lag++) v = RoundRocBankStage(v + (i >= lag ? differences[i - lag] : zero)); return v;
        }).ToArray();
        var signal = SmoothRocBankStage(line, Math.Max(1, signalLength), kind);
        return new() { { "Esdi", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
