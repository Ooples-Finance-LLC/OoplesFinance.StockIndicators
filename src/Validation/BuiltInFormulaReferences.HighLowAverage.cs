using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HighLowAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14)); var kind = AverageKind(options, 2);
        var highs = new ReferenceFraction[bars.Count]; var lows = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = bars[i].High; var low = bars[i].Low;
            for (var j = Math.Max(0, i - length + 1); j < i; j++) { high = Math.Max(high, bars[j].High); low = Math.Min(low, bars[j].Low); }
            highs[i] = ReferenceFraction.FromDouble(high); lows[i] = ReferenceFraction.FromDouble(low);
        }
        var upper = SmoothRocBankStage(highs, length, kind); var lower = SmoothRocBankStage(lows, length, kind);
        return Outputs(("UpperBand", upper.Select(v => v.ToDouble()).ToArray()),
            ("MiddleBand", upper.Select((v, i) => ((v + lower[i]) / new ReferenceFraction(2)).ToDouble()).ToArray()),
            ("LowerBand", lower.Select(v => v.ToDouble()).ToArray()));
    }
}
