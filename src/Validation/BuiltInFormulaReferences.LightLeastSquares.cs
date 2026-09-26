using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> LightLeastSquaresOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        LightLeastSquaresOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 250)), 1);
    internal static IReadOnlyDictionary<string, double[]> LightLeastSquaresOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var indices = Enumerable.Range(0, bars.Count).Select(i => new ReferenceFraction(i)).ToArray();
        var first = SmoothRocBankStage(prices, length, kind);
        var second = SmoothRocBankStage(prices, Math.Max(2, Math.Min(530, (int)((length + 1L) / 2))), kind);
        var indexMeans = SmoothRocBankStage(indices, length, kind); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var value = first[i];
            if (i + 1 >= length && length > 1)
            {
                var start = i - length + 1;
                if (Enumerable.Range(start, length).Any(j => bars[j].Close != bars[i].Close))
                {
                    var mean = new ReferenceFraction(start + (long)i) / new ReferenceFraction(2);
                    var squared = new ReferenceFraction(0);
                    for (var j = start; j <= i; j++) { var delta = indices[j] - mean; squared += delta * delta; }
                    var deviation = ReferenceFraction.FromDouble((squared / new ReferenceFraction(length)).SqrtToDouble());
                    value += (indices[i] - indexMeans[i]) * (second[i] - first[i]) / deviation;
                }
            }
            result[i] = value.ToDouble();
        }
        return Outputs(("Llsma", result));
    }
}
