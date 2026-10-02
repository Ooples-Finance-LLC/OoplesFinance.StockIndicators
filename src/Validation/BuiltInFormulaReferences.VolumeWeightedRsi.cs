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
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            if (kind is 3 or 6) return SmoothRocBankStage(values, period, kind, v => v);
            if (kind is not (1 or 2)) return Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
            // Direct finite windows avoid accumulating irrelevant historical
            // ratio denominators in a prefix sum; all arithmetic remains exact.
            return values.Select((_, i) =>
            {
                if (kind == 1 && i + 1 < period) return R(0);
                var sum = R(0);
                for (var j = Math.Max(0, i - period + 1); j <= i; j++)
                    sum += values[j] * R(kind == 2 ? period - (long)i + j : 1);
                return sum / (kind == 2 ? R(period) * R(period + 1L) / R(2) : R(period));
            }).ToArray();
        }
        // Independent net/absolute flow identity, without the production gain/loss split.
        var signed = bars.Select((b, i) => i == 0 ? R(0) : (R(b.Close) - R(bars[i - 1].Close)) * R(b.Volume)).ToArray();
        var net = Mean(signed, length); var total = Mean(signed.Select(v => v.Abs()).ToArray(), length);
        var centered = net.Select((v, i) => total[i].Sign == 0 ? R(100) : R(100) * v / total[i]).ToArray();
        return Mean(centered, smoothLength).Select(v => v.ToDouble()).ToArray();
    }
}
