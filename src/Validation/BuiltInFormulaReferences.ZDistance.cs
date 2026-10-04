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
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, code);
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
