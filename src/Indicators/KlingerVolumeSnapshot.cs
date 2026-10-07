using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Klinger oscillator and its independently nullable EMA signal.</summary>
public sealed record KlingerVolumeValue(double? Oscillator, double? Signal);

/// <summary>Volume-force Klinger oscillator with independent SMA-seeded EMAs.</summary>
/// <remarks>The first two candles initialize direction/range history without
/// contributing force. HLC direction and accumulated ranges compare exactly.
/// Complete force and EMA stages round once with extended upper exponents;
/// only unrepresentable published values are rejected. Zero cumulative range
/// retains previous force except when range or volume selects a zero/flat branch.
/// Period storage is constant and input order is preserved.</remarks>
public static class KlingerVolumeSnapshot
{
    /// <summary>Calculates fast period greater than two, larger slow period, and positive signal period.</summary>
    public static IReadOnlyList<KlingerVolumeValue> Calculate(
        IReadOnlyList<Bar> bars,
        int fastPeriod = 34,
        int slowPeriod = 55,
        int signalPeriod = 13
    )
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (fastPeriod < 3)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod <= fastPeriod)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (signalPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        foreach (var b in bars)
            if (
                !double.IsFinite(b.High)
                || !double.IsFinite(b.Low)
                || !double.IsFinite(b.Close)
                || !double.IsFinite(b.Volume)
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
        var fast = new EmaDifferenceSignal.Average(fastPeriod, false);
        var slow = new EmaDifferenceSignal.Average(slowPeriod, false);
        var signal = new EmaDifferenceSignal.Average(signalPeriod, false);
        var previousBasis = BigInteger.Zero;
        var previousRange = BigInteger.Zero;
        var cumulative = BigInteger.Zero;
        var force = BigInteger.Zero;
        var previousDirection = 0;
        var result = new KlingerVolumeValue[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var high = ExactVarianceWindow.Units(bar.High);
            var low = ExactVarianceWindow.Units(bar.Low);
            var basis = high + low + ExactVarianceWindow.Units(bar.Close);
            var range = high - low;
            var direction = basis > previousBasis ? 1 : -1;
            result[i] = new(null, null);
            if (i >= 2)
            {
                cumulative =
                    direction == previousDirection ? cumulative + range : previousRange + range;
                var volume = ExactVarianceWindow.Units(bar.Volume);
                if (range == cumulative || volume.IsZero)
                    force = 0;
                else if (range.IsZero)
                    force = RocBankValue.RoundUnits(200 * direction * volume, 1);
                else if (!cumulative.IsZero)
                    force = RocBankValue.RoundUnits(
                        200 * direction * volume * BigInteger.Abs(range - cumulative),
                        BigInteger.Abs(cumulative)
                    );
                var f = fast.Add(force);
                var s = slow.Add(force);
                if (f.HasValue && s.HasValue)
                {
                    var oscillator = RocBankValue.RoundUnits(f.Value - s.Value, 1);
                    var average = signal.Add(oscillator);
                    var value = ExactMeanAccumulator.UnitRatio(oscillator, 1);
                    double? signalValue = average.HasValue
                        ? ExactMeanAccumulator.UnitRatio(average.Value, 1)
                        : null;
                    if (
                        !double.IsFinite(value)
                        || signalValue.HasValue && !double.IsFinite(signalValue.Value)
                    )
                        throw new OverflowException("Klinger output is not representable.");
                    result[i] = new(value, signalValue);
                }
            }
            previousBasis = basis;
            previousRange = range;
            previousDirection = direction;
        }
        return result;
    }
}
