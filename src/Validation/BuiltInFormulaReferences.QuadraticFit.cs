using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? QuadraticFit(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.QuadraticLeastSquaresMovingAverage) return null;
        var length = Integer(indicator.CreateOptions(), "Length");
        return new("Qlma", new[] { "Qlma", "Forecast" }, bars =>
        {
            var fitted = new double[bars.Count]; var forecast = new double[bars.Count];
            for (var i = length-1; i < bars.Count; i++)
            {
                var samples = bars.Skip(i-length+1).Take(length).Select(b => (decimal)b.Close).ToArray();
                if (length < 3)
                {
                    fitted[i] = forecast[i] = (double)samples.Average();
                    continue;
                }
                // Solve the design matrix in decimal with pivoted elimination. Production
                // uses orthogonal discrete polynomials instead of these normal equations.
                var system = new decimal[3,4];
                for (var j = 0; j < length; j++)
                {
                    var x = j-(length-1)/2m;
                    var row = new[] { 1m, x, x*x };
                    for (var a = 0; a < 3; a++)
                    {
                        for (var b = 0; b < 3; b++) system[a,b] += row[a]*row[b];
                        system[a,3] += row[a]*samples[j];
                    }
                }
                for (var column = 0; column < 3; column++)
                {
                    var pivot = Enumerable.Range(column, 3-column).OrderByDescending(row => Math.Abs(system[row,column])).First();
                    for (var k = column; k < 4; k++) (system[column,k], system[pivot,k]) = (system[pivot,k], system[column,k]);
                    var scale = system[column,column];
                    for (var k = column; k < 4; k++) system[column,k] /= scale;
                    for (var row = 0; row < 3; row++)
                    {
                        if (row == column) continue;
                        var multiple = system[row,column];
                        for (var k = column; k < 4; k++) system[row,k] -= multiple*system[column,k];
                    }
                }
                double At(decimal x) => (double)(system[0,3]+x*system[1,3]+x*x*system[2,3]);
                fitted[i] = At((length-1)/2m);
                forecast[i] = At((length-1)/2m+14);
            }
            return Outputs(("Qlma", fitted), ("Forecast", forecast));
        });
    }
}
