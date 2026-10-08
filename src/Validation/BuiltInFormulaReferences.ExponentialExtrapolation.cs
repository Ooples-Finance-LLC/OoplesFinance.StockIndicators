using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedExponentialExtrapolation(IReadOnlyList<Bar> bars, int length, bool triple)
    {
        var first = RoundedEma(bars, length);
        var second = RoundedEma(first, length);
        var third = triple ? RoundedEma(second, length) : Array.Empty<double>();
        return first.Select((value, i) => (triple
            ? new ReferenceFraction(3) * (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(second[i]))
                + ReferenceFraction.FromDouble(third[i])
            : new ReferenceFraction(2) * ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(second[i])).ToDouble()).ToArray();
    }
}
