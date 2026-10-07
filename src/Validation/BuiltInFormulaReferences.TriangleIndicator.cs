using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> TriangleIndicatorOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        length = Math.Max(1, length);
        var raw = bars.Select(b => RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open))).ToArray();
        ReferenceFraction[] line;
        if (kind == 7)
        {
            line = new ReferenceFraction[bars.Count];
            var half = length / 2L;
            var mass = new ReferenceFraction(length % 2 == 0 ? half * (half + 1) : (half + 1) * (half + 1));
            for (var i = 0; i < bars.Count; i++)
            {
                var sum = new ReferenceFraction(0);
                for (var lag = 0; lag < Math.Min(length, i + 1); lag++)
                    sum += raw[i - lag] * new ReferenceFraction(Math.Min(lag + 1, length - lag));
                line[i] = RoundRocBankStage(sum / mass);
            }
        }
        else line = SmoothRocBankStage(raw, length, kind);
        var coefficient = ReferenceFraction.FromDouble(length / 2.0 * Math.PI);
        var roc = line.Select((v, i) => RoundRocBankStage(RoundRocBankStage(v - (i == 0 ? new ReferenceFraction(0) : line[i - 1])) * coefficient).ToDouble()).ToArray();
        return new() { { "Etwi", line.Select(v => v.ToDouble()).ToArray() }, { "Roc", roc } };
    }
}
