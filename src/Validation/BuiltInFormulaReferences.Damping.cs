using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] DampingOutputs(IReadOnlyList<Bar> bars, int length, int kind = 1)
    {
        var ranges = bars.Select(b => RoundRocBankStage(ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low))).ToArray();
        var average = SmoothRocBankStage(ranges, Math.Max(1, length), kind);
        return Enumerable.Range(0, bars.Count).Select(i => i < 6 || average[i - 6].Sign == 0 ? 0 : (average[i - 1] / average[i - 6]).ToDouble()).ToArray();
    }
}
