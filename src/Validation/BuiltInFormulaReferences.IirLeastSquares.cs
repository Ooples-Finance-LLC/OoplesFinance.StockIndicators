using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> IirLeastSquaresOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var gain = ReferenceFraction.FromDouble(4d / (length + 2d));
        var half = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        var factor = Math.Max(.01, Math.Min(.99, 2d / (half + 1d)));
        var alpha = ReferenceFraction.FromDouble(factor); var retained = ReferenceFraction.FromDouble(1 - factor);
        var previous = new ReferenceFraction(0); var ema = previous; var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var prior = i == 0 ? price : previous;
            ema = RoundRocBankStage(RoundRocBankStage(alpha * prior) + RoundRocBankStage(retained * ema));
            previous = RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(gain * price) + prior) - RoundRocBankStage(gain * ema));
            values[i] = previous.ToDouble();
        }
        return Outputs(("IIRLse", values));
    }
}
