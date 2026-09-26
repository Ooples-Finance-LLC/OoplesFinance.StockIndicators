using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RickerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double pctWidth = 60)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 50));
        var weights = Enumerable.Range(0, length).Select(j =>
        {
            var x = j == 0 ? 0 : pctWidth == 0 ? double.PositiveInfinity : Math.Abs(j / (double)length * 100 / pctWidth);
            var square = x * x;
            return ReferenceFraction.FromDouble(x > 40 ? 0 : x > 37 ? -Math.Exp(Math.Log(square - 1) - square / 2) : (1 - square) * Math.Exp(-square / 2));
        }).ToArray();
        var mass = weights.Aggregate(new ReferenceFraction(0), (sum, term) => sum + term);
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var total = new ReferenceFraction(0);
            for (var lag = 0; lag < length && lag <= i; lag++) total += weights[lag] * ReferenceFraction.FromDouble(bars[i - lag].Close);
            output[i] = mass.Sign == 0 ? bars[i].Close : (total / mass).ToDouble();
        }
        return Outputs(("Rsrma", output));
    }
}
