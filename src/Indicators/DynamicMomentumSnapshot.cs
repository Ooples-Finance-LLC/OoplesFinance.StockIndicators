using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Volatility-selected fixed-period Wilder RSI, with nullable input and output.</summary>
public static class DynamicMomentumSnapshot
{
    /// <summary>Computes population deviation, its nullable SMA, and selects floor(RSI period / relative volatility), clamped to the supplied limits.</summary>
    /// <remarks>Each selected RSI uses the original complete history. Missing values follow NullableStrengthOscillator semantics.
    /// Deviation and smoothing round once; the volatility ratio rounds before period selection. Period clamping precedes integer conversion.</remarks>
    public static IReadOnlyList<double?> FromValues(
        IReadOnlyList<double?> values,
        int deviationPeriod = 5,
        int smoothingPeriod = 10,
        int rsiPeriod = 14,
        int upperLimit = 30,
        int lowerLimit = 5
    )
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        if (
            deviationPeriod < 1
            || smoothingPeriod < 1
            || rsiPeriod < 1
            || lowerLimit < 1
            || upperLimit < lowerLimit
        )
            throw new ArgumentOutOfRangeException(nameof(deviationPeriod));
        foreach (var value in values)
            if (value.HasValue && !FrameworkCompatibility.IsFinite(value.Value))
                throw new ArgumentOutOfRangeException(nameof(values));
        var deviations = new double?[values.Count];
        var result = new double?[values.Count];
        var cache = new Dictionary<int, double?[]>();
        BigInteger sum = 0,
            squares = 0,
            smoothSum = 0;
        int present = 0,
            smoothCount = 0;
        void Moment(double? value, int sign)
        {
            if (!value.HasValue)
                return;
            var u = ExactVarianceWindow.Units(value.Value);
            sum += sign * u;
            squares += sign * u * u;
            present += sign;
        }
        for (var i = 0; i < values.Count; i++)
        {
            Moment(values[i], 1);
            if (i >= deviationPeriod)
                Moment(values[i - deviationPeriod], -1);
            if (i >= deviationPeriod - 1)
                deviations[i] =
                    present == 0
                        ? 0
                        : ExactPopulationDeviation.RootRatio(
                            present * squares - sum * sum,
                            (BigInteger)present * deviationPeriod
                        );
            if (deviations[i].HasValue)
            {
                smoothSum += ExactVarianceWindow.Units(deviations[i]!.Value);
                smoothCount++;
            }
            if (i >= smoothingPeriod && deviations[i - smoothingPeriod].HasValue)
            {
                smoothSum -= ExactVarianceWindow.Units(deviations[i - smoothingPeriod]!.Value);
                smoothCount--;
            }
            if (i < smoothingPeriod - 1 || smoothCount == 0 || !deviations[i].HasValue)
                continue;
            var mean = ExactMeanAccumulator.UnitRatio(smoothSum, smoothCount);
            if (mean == 0)
                continue;
            var ratio = ExactMeanAccumulator.UnitRatio(
                ExactVarianceWindow.Units(deviations[i]!.Value) << 1074,
                ExactVarianceWindow.Units(mean)
            );
            if (ratio == 0)
                continue;
            int selected;
            if (double.IsPositiveInfinity(ratio))
                selected = lowerLimit;
            else
            {
                var n = (BigInteger)rsiPeriod << 1074;
                var d = ExactVarianceWindow.Units(ratio);
                selected =
                    n >= d * upperLimit ? upperLimit
                    : n <= d * lowerLimit ? lowerLimit
                    : (int)(n / d);
            }
            if (!cache.TryGetValue(selected, out var trajectory))
            {
                trajectory = NullableStrengthOscillator.FromValues(values, selected).ToArray();
                cache.Add(selected, trajectory);
            }
            result[i] = trajectory[i];
        }
        return result;
    }
}
