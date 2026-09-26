using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ElasticVolumeOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var result = new double[bars.Count]; var previous = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var volume = ReferenceFraction.FromDouble(bars[i].Volume);
            if (i == 0) previous = price;
            var total = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++) total += ReferenceFraction.FromDouble(bars[j].Volume);
            if (total.Sign > 0) previous = (previous + volume / total * (price - previous)).RoundExtendedBinary64();
            result[i] = previous.ToDouble();
        }
        return Outputs(("Evwma", result));
    }
}
