using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumeAdjustedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double? factorOverride = null)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14));
        var means = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Volume)).ToArray(), length, AverageKind(options, 1));
        var factor = factorOverride ?? Number(options, .67, "Factor"); var zero = new ReferenceFraction(0);
        var weights = bars.Select((b, i) => factor == 0 || means[i].Sign == 0 ? zero : (ReferenceFraction.FromDouble(b.Volume) / means[i]).RoundExtendedBinary64()).ToArray();
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var mass = zero; var total = zero;
            for (var j = Math.Max(0, i - length + 1); j <= i; j++) { mass += weights[j]; total += weights[j] * ReferenceFraction.FromDouble(bars[j].Close); }
            output[i] = mass.Sign == 0 ? 0 : (total / mass).ToDouble();
        }
        return Outputs(("Vama", output));
    }
}
