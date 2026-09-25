using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FoldedRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 3); var length = Integer(options, "Length", 14);
        var rsi = RoundedPriceRsi(bars, length, kind);
        var folded = rsi.Select(v => ReferenceFraction.FromDouble(2 * Math.Abs(v - 50))).ToArray();
        var line = new double[bars.Count];
        for (var i = 0; i < line.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += folded[j];
            line[i] = sum.ToDouble();
        }
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Frsi", line), ("Signal", signal));
    }
}
