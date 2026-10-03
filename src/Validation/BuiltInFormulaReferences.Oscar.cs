using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget OscarBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> OscarOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => new() { ["Oscar"] = OscarValues(bars, Integer(indicator.CreateOptions(), "Length", 8)) };
    internal static double[] OscarValues(IReadOnlyList<Bar> bars, int length, double[]? selected = null)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var sum = R(0); var power = BigInteger.One; var result = new double[bars.Count];
        // Closed-form weighted sum: sum(rough[j]*6^j)/(3*6^i).
        for (var i = 0; i < bars.Count; i++)
        {
            var sample = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var high = R(sample.Max(b => b.High)); var low = R(sample.Min(b => b.Low)); var range = high - low;
            var rough = range.Sign == 0 ? R(0) : (R(selected?[i] ?? bars[i].Close) - low) * R(100) / range;
            if (rough.Sign < 0) rough = R(0); if (rough.CompareTo(R(100)) > 0) rough = R(100);
            sum += rough * new ReferenceFraction(power);
            result[i] = (sum / new ReferenceFraction(3 * power)).ToDouble();
            power *= 6;
        }
        return result;
    }
}
