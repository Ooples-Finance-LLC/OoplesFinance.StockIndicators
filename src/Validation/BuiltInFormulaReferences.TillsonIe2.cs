using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TillsonIe2Outputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerAverage = null)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 15)); var kind = AverageKind(options, 1);
        var means = customerAverage is null ? SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind) : customerAverage.Select(ReferenceFraction.FromDouble).ToArray();
        var previous = new ReferenceFraction(0); var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, length); var n = new ReferenceFraction(count);
            var values = bars.Skip(i - count + 1).Take(count).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2); var xx = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
            for (var j = 0; j < count; j++) { var x = new ReferenceFraction(j) - center; xx += x * x; xy += x * (values[j] - mean); }
            var endpoint = (count == 1 ? mean : mean + xy / xx * center).RoundExtendedBinary64();
            output[i] = ((endpoint + (endpoint - previous) + means[i]) / new ReferenceFraction(2)).ToDouble(); previous = endpoint;
        }
        return Outputs(("Ie2", output));
    }
}
