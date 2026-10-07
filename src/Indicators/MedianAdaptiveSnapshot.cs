using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Median-adaptive filtering with descending odd/even windows and upper medians.</summary>
/// <remarks>The first three prices pass through. A 1/2/2/1 four-price mean then
/// fills the median history. At maturity, candidate lengths decrease by two;
/// the final length is at least three. The error uses a signed median denominator,
/// so a negative median is intentionally different from its absolute value.
/// Exact comparisons avoid overflow, underflow and tolerance ties. Each complete
/// smoothing update rounds once. The threshold may be any finite signed value.</remarks>
public static class MedianAdaptiveSnapshot
{
    /// <summary>Calculates the median-adaptive convention with a positive history and finite threshold.</summary>
    public static IReadOnlyList<double> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 39,
        double threshold = .002
    )
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold));
        foreach (var bar in bars)
            if (!double.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = new double[bars.Count];
        var history = new Queue<double>();
        var previousFilter = 0d;
        var previousCandidate = 0d;
        var thresholdUnits = ExactVarianceWindow.Units(threshold);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < 3)
            {
                result[i] = bars[i].Close;
                continue;
            }
            var sum = new ExactMeanAccumulator();
            sum.Add(bars[i - 3].Close);
            sum.Add(bars[i - 2].Close, 2);
            sum.Add(bars[i - 1].Close, 2);
            sum.Add(bars[i].Close);
            var smooth = sum.Mean(6);
            if (history.Count == period)
                history.Dequeue();
            history.Enqueue(smooth);
            if (history.Count < period)
            {
                result[i] = smooth;
                continue;
            }
            var length = period;
            var candidate = previousCandidate;
            var above = .2 > threshold;
            var values = history.ToArray();
            while (above && length > 0)
            {
                var median = values
                    .Skip(values.Length - length)
                    .OrderBy(v => v)
                    .ElementAt(length / 2);
                candidate = Blend(smooth, previousCandidate, length);
                var m = ExactVarianceWindow.Units(median);
                if (!m.IsZero)
                {
                    var n = BigInteger.Abs(m - ExactVarianceWindow.Units(candidate)) << 1074;
                    above = m.Sign > 0 ? n > thresholdUnits * m : n < thresholdUnits * m;
                }
                length -= 2;
            }
            result[i] = Blend(smooth, previousFilter, Math.Max(3, length));
            previousFilter = result[i];
            previousCandidate = candidate;
        }
        return result;
    }

    private static double Blend(double input, double previous, int length)
    {
        var total = new ExactMeanAccumulator();
        total.Add(input, 2);
        total.Add(previous, length - 1);
        return total.Mean((long)length + 1);
    }
}
