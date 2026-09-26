using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ParametricCorrectiveOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double alpha = 1, double per = 35)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 50));
        var offset = ((ReferenceFraction.FromDouble(per) / new ReferenceFraction(100)).RoundExtendedBinary64() * new ReferenceFraction(length)).RoundExtendedBinary64();
        var weights = Enumerable.Range(0, bars.Count).Select(j =>
        {
            var p = (new ReferenceFraction(j + 1) - offset).RoundExtendedBinary64();
            return p.Sign < 0 ? (p * ReferenceFraction.FromDouble(alpha)).RoundExtendedBinary64() : p;
        }).ToArray();
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var mass = new ReferenceFraction(0); var sum = mass;
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            {
                mass += weights[j];
                if (j >= length) sum += weights[j] * ReferenceFraction.FromDouble(bars[j - length].Close);
            }
            output[i] = mass.Sign == 0 ? 0 : (sum / mass).ToDouble();
        }
        return Outputs(("Pclma", output));
    }
}
