using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ClassicHilbertValues(IReadOnlyList<Bar> bars, int upper, int lower)
    {
        var normalized = HilbertTransformerValues(bars, upper, lower, 1, false); var real = normalized.Outputs["Real"]; double[] coefficients = { .091, .111, .143, .2, .333, 1, -1, -.333, -.2, -.143, -.111, -.091 };
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var imaginary = real.Select((_, i) => Enumerable.Range(0, Math.Min(coefficients.Length, i / 2 + 1)).Select(tap => R(coefficients[tap]) * R(real[i - 2 * tap])).Aggregate(R(0), (sum, term) => sum + term) / R(1.865)).Select(value => value.ToDouble()).ToArray();
        return (new Dictionary<string, double[]> { { "Real", real }, { "Imag", imaginary } }, normalized.Signals);
    }
}
