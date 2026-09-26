using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedGeometricMean(IReadOnlyList<Bar> bars, int length, bool positiveOnly = false)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < length - 1) { result[i] = positiveOnly ? bars[i].Close : 0; continue; }
            var product = new ReferenceFraction(1);
            var used = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var value = positiveOnly ? bars[j].Close : Math.Max(0.000001, bars[j].Close);
                if (value > 0) { product *= ReferenceFraction.FromDouble(value); used++; }
            }
            result[i] = used > 0 ? product.PositiveRootToDouble(used) : 0;
        }
        return result;
    }
}
