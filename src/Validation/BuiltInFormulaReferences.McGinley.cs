using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> McGinleyOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => McGinleyValues(bars, Integer(indicator.CreateOptions(), "Length", 14), Number(indicator.CreateOptions(), .6, "K"));
    internal static Dictionary<string, double[]> McGinleyValues(IReadOnlyList<Bar> bars, int length = 14, double factor = .6)
    {
        var coefficient = ReferenceFraction.FromDouble(factor) * new ReferenceFraction(Math.Max(1, length));
        var result = new double[bars.Count]; var one = new ReferenceFraction(1);
        for (var i = 0; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close); var prior = ReferenceFraction.FromDouble(i == 0 ? bars[i].Close : result[i - 1]);
            if (prior.Sign == 0) { result[i] = bars[i].Close; continue; }
            var ratio = current / prior; var square = ratio * ratio; var denominator = coefficient * square * square;
            if (denominator.CompareTo(one) < 0) denominator = one;
            result[i] = (prior + (current - prior) / denominator).ToDouble();
        }
        return new() { ["Mdi"] = result };
    }
}
