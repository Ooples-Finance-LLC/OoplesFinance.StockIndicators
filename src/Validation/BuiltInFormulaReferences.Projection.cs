using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ProjectionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return ProjectionOutputs(bars, indicator.BatchName, Math.Max(1, Integer(options, "Length", 14)),
            AverageKind(options, 2), Math.Max(1, Integer(options, "SmoothLength", 4)));
    }

    internal static IReadOnlyDictionary<string, double[]> ProjectionOutputs(IReadOnlyList<Bar> bars,
        IndicatorName family, int length, int kind, int smooth)
    {
        var zero = new ReferenceFraction(0);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        ReferenceFraction[] Envelope(double[] prices, bool upper)
        {
            var slopes = new ReferenceFraction[prices.Length];
            var envelope = new ReferenceFraction[prices.Length];
            for (var i = 0; i < prices.Length; i++)
            {
                var first = Math.Max(0, i - length + 1); var count = i - first + 1;
                // Centered covariance is independent of the production rolling moments.
                var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
                var covariance = zero; var variance = zero;
                for (var j = first; j <= i; j++)
                {
                    var offset = new ReferenceFraction(j - first) - center;
                    covariance += offset * R(prices[j]); variance += offset * offset;
                }
                slopes[i] = variance.Sign == 0 ? zero : Round(covariance / variance);
                var candidates = new List<ReferenceFraction> { R(prices[i]) };
                for (var lag = 1L; lag <= Math.Min((long)length, i + 1L); lag++)
                {
                    var slope = i < lag ? zero : slopes[i - (int)lag];
                    candidates.Add(Round(R(prices[i - (int)lag + 1]) + Round(new ReferenceFraction(lag) * slope)));
                }
                if (length > i + 1L) candidates.Add(zero);
                envelope[i] = upper ? candidates.Max() : candidates.Min();
            }
            return envelope;
        }
        var high = Envelope(bars.Select(b => b.High).ToArray(), true);
        var low = Envelope(bars.Select(b => b.Low).ToArray(), false);
        if (family == IndicatorName.ProjectionBands)
            return Outputs(("UpperBand", high.Select(v => v.ToDouble()).ToArray()),
                ("LowerBand", low.Select(v => v.ToDouble()).ToArray()),
                ("MiddleBand", high.Select((v, i) => Round((v + low[i]) / new ReferenceFraction(2)).ToDouble()).ToArray()));
        var oscillator = family == IndicatorName.ProjectionOscillator;
        var line = high.Select((v, i) =>
        {
            var denominator = oscillator ? v - low[i] : v + low[i];
            var numerator = oscillator ? new ReferenceFraction(100) * (R(bars[i].Close) - low[i]) : new ReferenceFraction(200) * (v - low[i]);
            return denominator.Sign == 0 ? zero : Round(numerator / denominator);
        }).ToArray();
        var signal = SmoothRocBankStage(line, oscillator ? smooth : length, kind, Round);
        return Outputs((oscillator ? "Pbo" : "Pbw", line.Select(v => v.ToDouble()).ToArray()),
            ("Signal", signal.Select(v => v.ToDouble()).ToArray()));
    }
}
