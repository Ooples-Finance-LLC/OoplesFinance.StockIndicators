using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] HarmonicMeanReference(IReadOnlyList<Bar> bars, int length)
    {
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < length - 1) { output[i] = bars[i].Close; continue; }
            var sum = new ReferenceFraction(0);
            var count = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (bars[j].Close == 0) continue;
                sum += new ReferenceFraction(1) / ReferenceFraction.FromDouble(bars[j].Close);
                count++;
            }
            output[i] = sum.Sign == 0 ? 0 : (new ReferenceFraction(count) / sum).ToDouble();
        }
        return output;
    }
}
