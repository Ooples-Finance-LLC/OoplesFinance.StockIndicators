using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumeAdaptiveBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return VolumeAdaptiveBandOutputs(bars, Math.Max(1, Integer(options, "Length", 100)), AverageKind(options, 1)); }
    internal static IReadOnlyDictionary<string, double[]> VolumeAdaptiveBandOutputs(IReadOnlyList<Bar> bars, int length, int kind, double[]? externalVolume = null, double[]? externalUpper = null, double[]? externalLower = null)
    {
        length = Math.Max(1, length); var volumes = externalVolume ?? RoundedBoundedStage(bars.Select(b => b.Volume).ToArray(), length, kind); var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++) { var price = ReferenceFraction.FromDouble(bars[i].Close); var divisor = ReferenceFraction.FromDouble(Math.Max(1, volumes[i])); up[i] = (price + (i == 0 ? price : up[i - 1]) / divisor).RoundExtendedBinary64(); down[i] = (price - (i == 0 ? price : down[i - 1]) / divisor).RoundExtendedBinary64(); }
        var upper = externalUpper is null ? SmoothRocBankStage(up, length, kind) : externalUpper.Select(ReferenceFraction.FromDouble).ToArray(); var lower = externalLower is null ? SmoothRocBankStage(down, length, kind) : externalLower.Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("UpperBand", upper.Select(v => v.ToDouble()).ToArray()), ("MiddleBand", upper.Select((v,i) => ((v + lower[i]) / new ReferenceFraction(2)).ToDouble()).ToArray()), ("LowerBand", lower.Select(v => v.ToDouble()).ToArray()), ("RawUp", up.Select(v => v.ToDouble()).ToArray()), ("RawDown", down.Select(v => v.ToDouble()).ToArray()));
    }
}
