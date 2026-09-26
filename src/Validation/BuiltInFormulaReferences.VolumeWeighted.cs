using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumeWeightedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14)); var kind = AverageKind(options, 1);
        var means = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Volume)).ToArray(), length, kind);
        var output = new double[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var mass = zero; var total = zero; var start = Math.Max(0, i - length + 1);
            for (var j = start; j <= i; j++) { var volume = ReferenceFraction.FromDouble(bars[j].Volume); mass += volume; total += volume * ReferenceFraction.FromDouble(bars[j].Close); }
            var denominator = kind == 1 ? mass : means[i] * new ReferenceFraction(i - start + 1);
            output[i] = kind == 1 && i < length - 1 || denominator.Sign == 0 ? 0 : (total / denominator).ToDouble();
        }
        return Outputs(("Vwma", output));
    }
}
