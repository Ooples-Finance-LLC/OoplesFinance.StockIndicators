using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] FallingRisingOutputs(IReadOnlyList<Bar> bars, int length = 14)
    {
        length = Math.Max(2, length); var alpha = ReferenceFraction.FromDouble(2d / (length + 1d)); var zero = new ReferenceFraction(0); var line = zero; var error = zero;
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var prior = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(i + 1, length)).Select(k => k == 0 ? 0d : bars[k - 1].Close).ToArray();
            var beta = bars[i].Close > prior.Max() || bars[i].Close < prior.Min() ? new ReferenceFraction(1) : alpha;
            line = RoundRocBankStage(RoundRocBankStage(line + RoundRocBankStage(error * alpha)) + RoundRocBankStage(error * beta));
            error = RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - line); result[i] = line.ToDouble();
        }
        return result;
    }
}
