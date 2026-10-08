using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> LinearExtrapolationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 500)); var zero = new ReferenceFraction(0);
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var firstIndex = Math.Max(0, i - (long)length); var secondIndex = Math.Max(0, i - 2L * length);
            var first = i >= length ? ReferenceFraction.FromDouble(bars[i - length].Close) : zero;
            var second = i >= 2L * length ? ReferenceFraction.FromDouble(bars[(int)(i - 2L * length)].Close) : zero;
            var value = firstIndex == secondIndex ? first : first + (first - second) * new ReferenceFraction(i - firstIndex) / new ReferenceFraction(firstIndex - secondIndex);
            output[i] = value.ToDouble();
        }
        return Outputs(("LinExt", output));
    }
}
