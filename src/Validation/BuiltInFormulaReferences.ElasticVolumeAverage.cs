using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ElasticVolumeAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double multiplier = 20)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 40); var kind = AverageKind(options, 1);
        var averages = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Volume)).ToArray(), length, kind);
        var result = new double[bars.Count]; var previous = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var volume = ReferenceFraction.FromDouble(bars[i].Volume);
            if (i == 0) previous = price;
            var denominator = averages[i] * ReferenceFraction.FromDouble(multiplier);
            if (denominator.Sign > 0) previous = (previous + volume / denominator * (price - previous)).RoundExtendedBinary64();
            result[i] = previous.ToDouble();
        }
        return Outputs(("Evwma", result));
    }
}
