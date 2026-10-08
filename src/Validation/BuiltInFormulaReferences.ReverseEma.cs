using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ReverseEmaOutputs(IReadOnlyList<Bar> bars, double alpha = .1)
    {
        alpha = Math.Max(.01, Math.Min(.99, alpha)); var complement = 1 - alpha;
        var history = Enumerable.Range(0, 8).Select(_ => new ReferenceFraction[bars.Count]).ToArray(); var result = new double[bars.Count];
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        ReferenceFraction Product(ReferenceFraction v, double factor) => Round(v * ReferenceFraction.FromDouble(factor));
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i > 0 ? history[0][i - 1] : new ReferenceFraction(0);
            var ema = Round(Product(ReferenceFraction.FromDouble(bars[i].Close), alpha) + Product(previous, complement)); history[0][i] = ema; var reverse = ema;
            for (var stage = 0; stage < 8; stage++)
            {
                var prior = i > 0 ? history[stage][i - 1] : new ReferenceFraction(0);
                reverse = Round(Product(reverse, Math.Pow(complement, 1 << stage)) + prior);
                if (stage < 7) history[stage + 1][i] = reverse;
            }
            result[i] = Round(ema - Product(reverse, alpha)).ToDouble();
        }
        return result;
    }
}
