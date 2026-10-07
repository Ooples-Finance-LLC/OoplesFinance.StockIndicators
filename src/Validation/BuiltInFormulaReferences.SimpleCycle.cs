using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] SimpleCycleOutputs(IReadOnlyList<Bar> bars, int length = 50)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0);
        var a = 1d / length; var k = Math.Max(.01, Math.Min(.99, 2d / (length + 1d)));
        var previous = zero; var ema = zero; var sources = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            sources[i] = RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) + previous);
            ema = RoundRocBankStage(RoundRocBankStage(previous * ReferenceFraction.FromDouble(k)) + RoundRocBankStage(ema * ReferenceFraction.FromDouble(1 - k)));
            var residual = RoundRocBankStage(previous - ema); var change = RoundRocBankStage(sources[i] - (i < length ? zero : sources[i - length]));
            previous = RoundRocBankStage(RoundRocBankStage(change * ReferenceFraction.FromDouble(a)) + RoundRocBankStage(residual * ReferenceFraction.FromDouble(1 - a)));
            result[i] = previous.ToDouble();
        }
        return result;
    }
}
