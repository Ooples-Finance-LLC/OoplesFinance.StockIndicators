using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedLinearRegression(IReadOnlyList<Bar> bars, int length)
    {
        var fit = new double[bars.Count]; var next = new double[bars.Count];
        var slopes = new double[bars.Count]; var intercepts = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, length);
            var values = bars.Skip(i - count + 1).Take(count).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / new ReferenceFraction(count);
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
            var xx = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
            for (var j = 0; j < count; j++)
            {
                var position = new ReferenceFraction(j) - center;
                xx += position * position;
                xy += position * (values[j] - mean);
            }
            var slope = count == 1 ? new ReferenceFraction(0) : xy / xx;
            slopes[i] = slope.ToDouble();
            fit[i] = (mean + slope * center).ToDouble();
            next[i] = (mean + slope * (center + new ReferenceFraction(1))).ToDouble();
            intercepts[i] = (mean + slope * (center - new ReferenceFraction(i))).ToDouble();
        }
        return Outputs(("LinearRegression", fit), ("PredictedTomorrow", next), ("Slope", slopes), ("Intercept", intercepts));
    }
}
