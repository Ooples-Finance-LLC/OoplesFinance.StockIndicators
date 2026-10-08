using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumeWeightedRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return new Dictionary<string, double[]> { ["Vwrsi"] = VolumeWeightedRsiValues(bars, Integer(options, "Length", 10),
            Integer(options, "SmoothLength", 3), AverageKind(options, 2)) };
    }
    internal static double[] VolumeWeightedRsiValues(IReadOnlyList<Bar> bars, int length, int smoothLength = 3, int kind = 2)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period) => WindowedRationalAverage(values, period, kind);
        // Independent net/absolute flow identity, without the production gain/loss split.
        var signed = bars.Select((b, i) => i == 0 ? R(0) : (R(b.Close) - R(bars[i - 1].Close)) * R(b.Volume)).ToArray();
        var net = Mean(signed, length); var total = Mean(signed.Select(v => v.Abs()).ToArray(), length);
        var centered = net.Select((v, i) => total[i].Sign == 0 ? R(100) : R(100) * v / total[i]).ToArray();
        return Mean(centered, smoothLength).Select(v => v.ToDouble()).ToArray();
    }
}
