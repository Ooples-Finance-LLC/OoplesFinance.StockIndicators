using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AffineAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length");
        var sharp = indicator.BatchName == IndicatorName.SharpModifiedMovingAverage;
        var mean = sharp ? Average(Closes(bars), length, AverageKind(options, 1)) : new double[bars.Count];
        var offset = Integer(options, "Offset", 4);
        var n = new ReferenceFraction(length);
        var divisor = sharp ? n * new ReferenceFraction(length + 1L)
            : n * new ReferenceFraction(length + 1L - 2L * offset) / new ReferenceFraction(2);
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (divisor.Sign == 0) continue;
            var sum = ReferenceFraction.FromDouble(mean[i]) * divisor;
            // Chronological coefficients, independent of the production window traversal.
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            {
                var age = i - j;
                var weight = sharp ? 3L * (length - 1L - 2L * age) : (long)length - offset - age;
                sum += ReferenceFraction.FromDouble(bars[j].Close) * new ReferenceFraction(weight);
            }
            output[i] = (sum / divisor).ToDouble();
        }
        return Outputs((sharp ? "Smma" : "Epma", output));
    }
}
