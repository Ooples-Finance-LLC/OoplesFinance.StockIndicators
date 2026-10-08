using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Ratio of simple-smoothed sample deviations of positive and negative price changes.</summary>
/// <remarks>The previous price initially equals zero. Both deviation and smoothing
/// windows use all available history, with singleton deviation zero. The result is
/// 100*up/(up+down), or zero when both averages are zero. Price changes are exact;
/// deviation and smoothing stages round once with extended upper exponents.
/// Final percentages round once and remain bounded. Histories grow lazily.</remarks>
public static class RelativeVolatilitySnapshot
{
    /// <summary>Calculates the sample-deviation relative volatility convention for a period of at least two.</summary>
    public static IReadOnlyList<double> Calculate(IReadOnlyList<Bar> bars, int period = 14)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        foreach (var bar in bars)
            if (!FrameworkCompatibility.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var up = new Leg(period);
        var down = new Leg(period);
        var previous = BigInteger.Zero;
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ExactVarianceWindow.Units(bars[i].Close);
            var change = price - previous;
            previous = price;
            var u = up.Next(BigInteger.Max(change, 0));
            var d = down.Next(BigInteger.Max(-change, 0));
            output[i] =
                u.IsZero && d.IsZero ? 0 : ExactMeanAccumulator.UnitRatio((100 * u) << 1074, u + d);
        }
        return output;
    }

    private sealed class Leg(int period)
    {
        private readonly Queue<BigInteger> _changes = new(),
            _deviations = new();
        private BigInteger _sum,
            _squares,
            _deviationSum;

        internal BigInteger Next(BigInteger value)
        {
            if (_changes.Count == period)
            {
                var old = _changes.Dequeue();
                _sum -= old;
                _squares -= old * old;
            }
            _changes.Enqueue(value);
            _sum += value;
            _squares += value * value;
            var count = _changes.Count;
            var deviation = BigInteger.Zero;
            if (count > 1)
            {
                var numerator = count * _squares - _sum * _sum;
                var denominator = new BigInteger((long)count * (count - 1));
                for (var shift = 0; ; shift += 32)
                {
                    var root = ExactPopulationDeviation.RootRatio(
                        numerator,
                        denominator << (2 * shift)
                    );
                    if (!FrameworkCompatibility.IsFinite(root))
                        continue;
                    deviation = ExactVarianceWindow.Units(root) << shift;
                    break;
                }
            }
            if (_deviations.Count == period)
                _deviationSum -= _deviations.Dequeue();
            _deviations.Enqueue(deviation);
            _deviationSum += deviation;
            return RocBankValue.RoundUnits(_deviationSum, _deviations.Count);
        }
    }
}
