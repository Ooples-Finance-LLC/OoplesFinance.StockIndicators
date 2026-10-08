using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? QuadraticFit(IBuiltInIndicator indicator) => indicator.BatchName != IndicatorName.QuadraticLeastSquaresMovingAverage ? null
        : new("Qlma", new[] { "Qlma", "Forecast" }, bars => QuadraticFitOutputs(bars, indicator));
    internal static IReadOnlyDictionary<string, double[]> QuadraticFitOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        QuadraticFitOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 50)), 14);
    internal static IReadOnlyDictionary<string, double[]> QuadraticFitOutputs(IReadOnlyList<Bar> bars, int length, int horizon)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        var fitted = new double[bars.Count]; var forecast = new double[bars.Count];
        for (var i = length - 1; i < bars.Count; i++)
        {
            var samples = bars.Skip(i - length + 1).Take(length).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            if (length < 3) { fitted[i] = forecast[i] = (samples.Aggregate(zero, (sum, value) => sum + value) / new ReferenceFraction(length)).ToDouble(); continue; }
            var matrix = new ReferenceFraction[3, 4];
            for (var a = 0; a < 3; a++) for (var b = 0; b < 4; b++) matrix[a, b] = zero;
            for (var j = 0; j < length; j++)
            {
                var x = new ReferenceFraction(j); var row = new[] { one, x, x * x };
                for (var a = 0; a < 3; a++)
                {
                    for (var b = 0; b < 3; b++) matrix[a, b] += row[a] * row[b];
                    matrix[a, 3] += row[a] * samples[j];
                }
            }
            // Exact normal equations; distinct abscissas make all leading pivots nonzero.
            for (var column = 0; column < 3; column++)
            {
                var pivot = matrix[column, column];
                for (var k = column; k < 4; k++) matrix[column, k] /= pivot;
                for (var row = 0; row < 3; row++)
                {
                    if (row == column) continue; var multiple = matrix[row, column];
                    for (var k = column; k < 4; k++) matrix[row, k] -= multiple * matrix[column, k];
                }
            }
            double At(long x) { var value = new ReferenceFraction(x); return (matrix[0, 3] + value * matrix[1, 3] + value * value * matrix[2, 3]).ToDouble(); }
            fitted[i] = At(length - 1L); forecast[i] = At(length - 1L + horizon);
        }
        return Outputs(("Qlma", fitted), ("Forecast", forecast));
    }
}
