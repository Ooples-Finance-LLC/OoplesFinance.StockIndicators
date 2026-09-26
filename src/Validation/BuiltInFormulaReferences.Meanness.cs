using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MeannessOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerSmoothed = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 100); var kind = AverageKind(options, 0);
        var prices = Closes(bars);
        var line = prices.Select((_, i) =>
        {
            if (length == 1) return 0d;
            var ordered = prices.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).OrderBy(value => value).ToArray();
            var median = ExactPriceMean(ordered[(ordered.Length - 1) / 2], ordered[ordered.Length / 2]);
            var pairs = Enumerable.Range(0, length - 1).Select(lag =>
                (Current: i >= lag ? prices[i - lag] : 0, Previous: i > lag ? prices[i - lag - 1] : 0));
            var outward = pairs.Count(pair => pair.Current > median && pair.Current > pair.Previous || pair.Current < median && pair.Current < pair.Previous);
            return 100d * outward / (length - 1);
        }).ToArray();
        var smoothed = customerSmoothed ?? (kind == 0 ? KendallTrajectory(line, length)
            : SmoothRocBankStage(line.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray());
        return Outputs(("Mmi", line), ("MmiSmoothed", smoothed));
    }
}
