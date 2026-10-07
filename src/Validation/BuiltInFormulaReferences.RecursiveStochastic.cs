using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RecursiveStochasticOutputs(IReadOnlyList<Bar> bars, int length, double alpha = .1)
    {
        length = Math.Max(1, length); alpha = Math.Max(0, Math.Min(1, alpha));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var blends = new ReferenceFraction[bars.Count]; var outputs = new double[bars.Count];
        double Percent(ReferenceFraction value, ReferenceFraction high, ReferenceFraction low)
        {
            var span = RoundRocBankStage(high - low); if (span.Sign == 0) return 0;
            return (RoundRocBankStage(RoundRocBankStage(value - low) / span) * new ReferenceFraction(100)).ToDouble();
        }
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - length + 1); var sample = prices.Skip(start).Take(i - start + 1).ToArray(); var stoch = Percent(prices[i], sample.Max(), sample.Min());
            var current = RoundRocBankStage(ReferenceFraction.FromDouble(alpha) * ReferenceFraction.FromDouble(stoch));
            var prior = RoundRocBankStage(ReferenceFraction.FromDouble(1 - alpha) * ReferenceFraction.FromDouble(i > 0 ? outputs[i - 1] : 0));
            blends[i] = RoundRocBankStage(current + prior); var history = blends.Skip(start).Take(i - start + 1).ToArray();
            outputs[i] = Math.Max(0, Math.Min(100, Percent(blends[i], history.Max(), history.Min())));
        }
        return outputs;
    }
}
