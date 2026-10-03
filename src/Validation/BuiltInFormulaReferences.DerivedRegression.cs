using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DerivedRegressionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var oscillator = indicator.BatchName == IndicatorName.RegressionOscillator;
        var options = indicator.CreateOptions(); var period = Integer(options, "Length", oscillator ? 63 : 14);
        var kind = oscillator ? 1 : AverageKind(options, 1);
        var source = Closes(bars).Select(ReferenceFraction.FromDouble).ToArray();
        var means = oscillator ? Array.Empty<ReferenceFraction>() : SmoothStrengthStage(source, period, kind);
        var times = oscillator ? Array.Empty<ReferenceFraction>() : SmoothStrengthStage(Enumerable.Range(0, bars.Count).Select(i => new ReferenceFraction(i)).ToArray(), period, kind);
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, period); var n = new ReferenceFraction(count);
            var window = source.Skip(i - count + 1).Take(count).ToArray();
            var mean = window.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
            var xy = new ReferenceFraction(0); var xx = new ReferenceFraction(0);
            for (var j = 0; j < count; j++)
            {
                var x = new ReferenceFraction(j) - center;
                xy += x * (window[j] - mean); xx += x * x;
            }
            var slope = count == 1 ? new ReferenceFraction(0) : xy / xx;
            var endpoint = mean + slope * center;
            output[i] = oscillator ? endpoint.Sign == 0 ? 0 : (new ReferenceFraction(100) * (source[i] - endpoint) / endpoint).ToDouble()
                : count < period ? means[i].ToDouble() : kind == 1 ? endpoint.ToDouble()
                : (means[i] + slope * (new ReferenceFraction(i) - times[i])).ToDouble();
        }
        return Outputs((oscillator ? "Rosc" : "LinReg", output));
    }
}
