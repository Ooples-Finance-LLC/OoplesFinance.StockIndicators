using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BuffOutputs(IReadOnlyList<Bar> bars, int fast, int slow = 20)
    {
        double[] Weighted(int period)
        {
            var values = new double[bars.Count]; period = Math.Max(1, period);
            for (var i = 0; i < bars.Count; i++)
            {
                var numerator = new ReferenceFraction(0); var denominator = new ReferenceFraction(0);
                for (var j = Math.Max(0, i - period + 1); j <= i; j++)
                {
                    var volume = ReferenceFraction.FromDouble(bars[j].Volume);
                    numerator += ReferenceFraction.FromDouble(bars[j].Close) * volume; denominator += volume;
                }
                values[i] = denominator.Sign == 0 ? 0 : (numerator / denominator).ToDouble();
            }
            return values;
        }
        return Outputs(("FastBuff", Weighted(fast)), ("SlowBuff", Weighted(slow)));
    }
}
