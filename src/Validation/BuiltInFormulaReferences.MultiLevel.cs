using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] MultiLevelOutputs(IReadOnlyList<Bar> bars, int length = 14, double factor = 10000)
    {
        length = Math.Max(1, length); var scale = ReferenceFraction.FromDouble(factor);
        return bars.Select((bar, i) => RoundRocBankStage(RoundRocBankStage(ReferenceFraction.FromDouble(i < length ? 0 : bars[i - length].Open) - ReferenceFraction.FromDouble(bar.Open)) * scale).ToDouble()).ToArray();
    }
}
