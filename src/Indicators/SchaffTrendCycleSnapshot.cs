using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Three-point simple-smoothed stochastic of independently seeded MACD averages.</summary>
/// <remarks>This is the single-stochastic STC convention. Both EMAs seed from their
/// own complete windows. The cycle window starts with the first available MACD.
/// Flat windows contribute zero; three complete stochastic values are required.
/// Intermediate stages round once with extended upper exponents. Output is bounded
/// by 0 and 100. Input order is preserved and histories grow only as data arrives.</remarks>
public static class SchaffTrendCycleSnapshot
{
    /// <summary>Calculates the single-stochastic convention with positive cycle/fast periods and a larger slow period.</summary>
    public static IReadOnlyList<double?> Calculate(
        IReadOnlyList<Bar> bars,
        int cyclePeriod = 10,
        int fastPeriod = 23,
        int slowPeriod = 50
    )
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (cyclePeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(cyclePeriod));
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod <= fastPeriod)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        foreach (var bar in bars)
            if (!double.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = new double?[bars.Count];
        var fast = new EmaDifferenceSignal.Average(fastPeriod, false);
        var slow = new EmaDifferenceSignal.Average(slowPeriod, false);
        var window = new Queue<BigInteger>();
        var smoothing = new Queue<double>();
        var sum = new ExactMeanAccumulator();
        for (var i = 0; i < bars.Count; i++)
        {
            var units = ExactVarianceWindow.Units(bars[i].Close);
            var f = fast.Add(units);
            var s = slow.Add(units);
            if (!f.HasValue || !s.HasValue)
                continue;
            var macd = RocBankValue.RoundUnits(f.Value - s.Value, 1);
            if (window.Count == cyclePeriod)
                window.Dequeue();
            window.Enqueue(macd);
            if (window.Count < cyclePeriod)
                continue;
            var low = window.Min();
            var high = window.Max();
            var value =
                high == low
                    ? 0
                    : ExactMeanAccumulator.UnitRatio((100 * (macd - low)) << 1074, high - low);
            if (smoothing.Count == 3)
                sum.Add(smoothing.Dequeue(), -1);
            smoothing.Enqueue(value);
            sum.Add(value);
            if (smoothing.Count == 3)
                result[i] = sum.Mean(3);
        }
        return result;
    }
}
