using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? PredictiveFilters(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.EhlersVossPredictiveFilter or IndicatorName.EhlersTruncatedBandPassFilter)) return null;
        var truncated = name == IndicatorName.EhlersTruncatedBandPassFilter;
        var options = indicator.CreateOptions();
        var length = Integer(options, truncated ? "Length1" : "Length", 20);
        var cutoff = Integer(options, "Length2", 10);
        var bandwidth = Number(options, truncated ? .1 : .25, "Bandwidth");
        var cosine = Math.Cos(2 * Math.PI * bandwidth / length);
        var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        var angle = 2 * Math.PI / length;
        if (truncated) angle = Clamp(angle, .01, .99);
        var feedback = Math.Cos(angle) * (1 + decay);
        var order = Clamp((int)Math.Ceiling(3 * Number(options, 3, "Predict")), 2, 530);
        return new(truncated ? "Etbpf" : "Voss", truncated ? new[] { "Etbpf" } : new[] { "Voss", "Filt" }, bars =>
        {
            var gap = Complex.Sqrt(feedback * feedback - 4 * decay);
            var first = (feedback + gap) / 2;
            var second = (feedback - gap) / 2;
            var response = Enumerable.Range(0, bars.Count).Select(j => gap.Magnitude < 1e-12
                ? ((j + 1) * Complex.Pow(first, j)).Real
                : ((Complex.Pow(first, j + 1) - Complex.Pow(second, j + 1)) / gap).Real).ToArray();
            var drive = bars.Select((b, i) => !truncated && i <= 5 ? 0 : (1 - decay) / 2 * (b.Close - (i < 2 ? 0 : bars[i - 2].Close))).ToArray();
            var filtered = drive.Select((_, i) => Enumerable.Range(truncated ? Math.Max(0, i - cutoff + 1) : 0,
                truncated ? Math.Min(cutoff, i + 1) : i + 1).Sum(j => response[i - j] * drive[j])).ToArray();
            if (truncated) return Outputs(("Etbpf", filtered));
            // Polynomial inversion of the predictor's weighted-delay denominator.
            var predictor = new double[bars.Count];
            for (var i = 0; i < predictor.Length; i++)
                predictor[i] = i == 0 ? (3d + order) / 2 : -Enumerable.Range(1, Math.Min(order, i))
                    .Sum(lag => (order + 1d - lag) / order * predictor[i - lag]);
            var predicted = filtered.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => predictor[i - j] * filtered[j])).ToArray();
            return Outputs(("Voss", predicted), ("Filt", filtered));
        });
    }
}
