using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SelfWeightedOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var result = new double[bars.Count];
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        for (var i = 0; i < prices.Length; i++)
        {
            var top = new ReferenceFraction(0); var bottom = new ReferenceFraction(0);
            for (var source = Math.Max(0, (long)i - 2L * length + 1); source <= (long)i - length; source++)
            {
                var weight = prices[(int)source]; top += weight * prices[(int)source + length]; bottom += weight;
            }
            result[i] = bottom.Sign == 0 ? 0 : (top / bottom).ToDouble();
        }
        return Outputs(("Swma", result));
    }
}
