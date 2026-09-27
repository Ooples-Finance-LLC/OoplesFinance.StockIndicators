using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SeededRelativeVolatilityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return SeededRelativeVolatilityOutputs(bars, indicator.BatchName == IndicatorName.RelativeVolatilityIndexHigh, Integer(o, "Length", 14), Integer(o, "StdDevLength", 10)); }
    internal static IReadOnlyDictionary<string, double[]> SeededRelativeVolatilityOutputs(IReadOnlyList<Bar> bars, bool high, int length, int deviationLength)
    {
        length = Math.Max(1, length); deviationLength = Math.Max(1, deviationLength); var prices = bars.Select(b => ReferenceFraction.FromDouble(high ? b.High : b.Low)).ToArray(); var result = new double[bars.Count]; var zero = new ReferenceFraction(0); var upSeed = zero; var downSeed = zero; var up = zero; var down = zero;
        for (var i = 1; i < prices.Length; i++)
        {
            var deviation = zero;
            if (i + 1 >= deviationLength)
            {
                var window = prices.Skip(i + 1 - deviationLength).Take(deviationLength).ToArray(); var mean = window.Aggregate(zero,(a,b)=>a+b) / new ReferenceFraction(deviationLength); var variance = window.Aggregate(zero,(a,b)=>a+(b-mean)*(b-mean)) / new ReferenceFraction(deviationLength); deviation = ReferenceFraction.FromDouble(variance.SqrtToDouble());
            }
            var u = (prices[i] - prices[i - 1]).Sign > 0 ? deviation : zero; var d = (prices[i] - prices[i - 1]).Sign < 0 ? deviation : zero;
            if (i <= length) { upSeed += u; downSeed += d; if (i == length) { up = RoundRocBankStage(upSeed / new ReferenceFraction(length)); down = RoundRocBankStage(downSeed / new ReferenceFraction(length)); } }
            else { up = RoundRocBankStage((up * new ReferenceFraction(length - 1L) + u) / new ReferenceFraction(length)); down = RoundRocBankStage((down * new ReferenceFraction(length - 1L) + d) / new ReferenceFraction(length)); }
            if (i >= length) result[i] = (up + down).Sign == 0 ? 50 : (new ReferenceFraction(100) * up / (up + down)).ToDouble();
        }
        return Outputs((high ? "RviHigh" : "RviLow", result));
    }
}
