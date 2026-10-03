using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] TrigonometricOutputs(IReadOnlyList<Bar> bars, int length = 200)
    {
        length = Math.Max(1, length);
        ReferenceFraction Fit(IReadOnlyList<ReferenceFraction> values, int end)
        {
            var n = Math.Min(length, end + 1); var sum = new ReferenceFraction(0);
            // Closed-form OLS endpoint weights, independent of production rolling moments.
            for (var j = 0; j < n; j++) sum += values[end - n + 1 + j] * new ReferenceFraction(6L * j - 2L * n + 4);
            return RoundRocBankStage(sum / new ReferenceFraction(n) / new ReferenceFraction(n + 1L));
        }
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var angles = new ReferenceFraction[bars.Count]; var previous = new ReferenceFraction(0); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var fit = Fit(prices, i); var direction = fit.CompareTo(previous); angles[i] = ReferenceFraction.FromDouble(direction > 0 ? Math.PI : direction < 0 ? -3 * Math.PI : 0);
            result[i] = Math.Atan(Fit(angles, i).ToDouble()); previous = fit;
        }
        return result;
    }
}
