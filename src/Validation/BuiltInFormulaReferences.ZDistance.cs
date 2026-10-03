using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ZDistanceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return new Dictionary<string, double[]> { ["Zscore"] = ZDistanceValues(bars,
            (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!, Integer(options, "Length", 20)) };
    }
    internal static double[] ZDistanceValues(IReadOnlyList<Bar> bars, MovingAvgType kind = MovingAvgType.VolumeWeightedAveragePrice, int length = 20)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var code = AverageKind(new { MaType = kind }, 3);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length)
        {
            length = Math.Max(1, length);
            if (code is 3 or 6)
            {
                // Recursive means do not need cumulative sums of normalized ratios.
                // Expand the update as previous + alpha*(input-previous), independently of production's weighted numerator.
                var output = new ReferenceFraction[values.Length]; var seed = R(0); var previous = R(0);
                var alpha = R(code == 3 ? 2 : 1) / R(code == 3 ? length + 1L : length);
                for (var i = 0; i < values.Length; i++)
                {
                    if (code == 3 && i < length) { seed += values[i]; previous = seed / R(i + 1L); }
                    else previous += alpha * (values[i] - previous);
                    output[i] = previous;
                }
                return output;
            }
            if (code is not (1 or 2)) return Average(values.Select(v => v.ToDouble()).ToArray(), length, code).Select(R).ToArray();
            return values.Select((_, i) =>
            {
                if (code == 1 && i + 1 < length) return R(0);
                var sum = R(0);
                for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R(code == 2 ? length - (long)i + j : 1);
                return sum / (code == 2 ? R(length) * R(length + 1L) / R(2) : R(length));
            }).ToArray();
        }
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var means = kind == MovingAvgType.VolumeWeightedAveragePrice ? prices.Select((price, i) =>
        {
            // Translate the window by the current price, independently of production's product sum.
            var offset = R(0); var volume = R(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            { var weight = R(bars[j].Volume); offset += weight * (prices[j] - price); volume += weight; }
            return volume.Sign == 0 ? R(0) : price + offset / volume;
        }).ToArray() : Mean(prices, length);
        var residuals = prices.Select((value, i) => value - means[i]).ToArray();
        return residuals.Select((residual, i) =>
        {
            if (i + 1 < length) return 0d;
            var sum = R(0);
            for (var j = i - length + 1; j <= i; j++) sum += residuals[j] * residuals[j];
            return sum.Sign == 0 ? 0d : residual.Sign * (residual * residual * R(length) / sum).SqrtToDouble();
        }).ToArray();
    }
}
