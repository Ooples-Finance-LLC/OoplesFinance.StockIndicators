using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> LinearQuadraticOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return LinearQuadraticOutputs(bars, Math.Max(1, Integer(options, "Length", 50)),
            Math.Max(1, Integer(options, "SignalLength", 25)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string, double[]> LinearQuadraticOutputs(IReadOnlyList<Bar> bars, int length, int signalLength, int kind)
    {
        var zero = new ReferenceFraction(0);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var quadratic = QuadraticProjectionStages(bars, length, kind);
        var difference = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - length + 1); var count = i - start + 1;
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
            var mean = zero; var covariance = zero; var variance = zero;
            for (var j = start; j <= i; j++)
            {
                var x = new ReferenceFraction(j - start) - center;
                mean += prices[j]; covariance += x * prices[j]; variance += x * x;
            }
            mean /= new ReferenceFraction(count);
            var endpoint = (variance.Sign == 0 ? mean : mean + covariance / variance * center).RoundExtendedBinary64();
            difference[i] = (quadratic[i] - endpoint).RoundExtendedBinary64();
        }
        var signal = SmoothRocBankStage(difference, signalLength, kind, v => v.RoundExtendedBinary64());
        return Outputs(("Lqcdo", difference.Select((value, i) =>
            ((value - signal[i]).RoundExtendedBinary64() - signal[i]).RoundExtendedBinary64().ToDouble()).ToArray()));
    }
}
