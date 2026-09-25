using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SmoothedDeltaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 100);
        var kind = AverageKind(options, 1);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var means = SmoothStrengthStage(prices, length, kind);
        var changes = prices.Select((v, i) => i < length ? new ReferenceFraction(0) : RoundStrengthStage(v - prices[i - length])).ToArray();
        var absolute = changes.Select(v => v.Sign < 0 ? new ReferenceFraction(0) - v : v).ToArray();
        var travel = SmoothStrengthStage(absolute, length, kind);
        var line = prices.Select((_, i) =>
        {
            if (i < length || travel[i].Sign == 0) return 0;
            var movement = RoundStrengthStage(means[i] - means[i - length]);
            return Math.Max(0, Math.Min(1, (movement / travel[i]).ToDouble()));
        }).ToArray();
        return Outputs(("Sdro", line));
    }
}
