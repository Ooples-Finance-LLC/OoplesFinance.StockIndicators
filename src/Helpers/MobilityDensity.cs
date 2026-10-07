using System.Numerics;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact density kernel for ordered, finite candle intervals. Route adapters own
// input validation. Keeping this separate makes the bin geometry independent of
// streaming history, smoothing and the selected comparison-price source.
internal static class MobilityDensity
{
    internal static Number Evaluate(IReadOnlyList<(BigInteger Low, BigInteger High)> sample,
        BigInteger comparison, int binCount)
    {
        if (sample.Count == 0) return default;
        binCount = Math.Max(1, binCount);
        var minimum = sample[0].Low;
        var maximum = sample[0].High;
        for (var i = 1; i < sample.Count; i++)
        {
            minimum = BigInteger.Min(minimum, sample[i].Low);
            maximum = BigInteger.Max(maximum, sample[i].High);
        }
        var range = maximum - minimum;
        if (range.IsZero) return default;
        int Bin(BigInteger value)
            => (int)BigInteger.Min(binCount - 1, (value - minimum) * binCount / range);

        // Between candle endpoints shifted by zero or one bin, total bin mass
        // is linear in the bin index. Adjacent integer bins contain each change
        // of slope and each point mass. Endpoints cover constant/linear stretches;
        // sorting preserves the first exact maximum without iterating all bins.
        var candidates = new SortedSet<int> { 0, binCount - 1 };
        foreach (var candle in sample)
        {
            foreach (var endpoint in new[] { candle.Low, candle.High })
            {
                var index = (long)((endpoint - minimum) * binCount / range);
                for (var offset = -1; offset <= 1; offset++)
                {
                    var candidate = index + offset;
                    if (candidate >= 0 && candidate < binCount) candidates.Add((int)candidate);
                }
            }
        }
        Number Mass(int bin)
        {
            var lower = minimum * binCount + bin * range;
            var upper = lower + range;
            Number total = default;
            foreach (var candle in sample)
            {
                if (candle.High == candle.Low)
                {
                    if (Bin(candle.Low) == bin) total += Number.Of(1);
                    continue;
                }
                var overlap = BigInteger.Min(candle.High * binCount, upper)
                    - BigInteger.Max(candle.Low * binCount, lower);
                if (overlap.Sign > 0)
                    total += Number.Integer(overlap).Divide(Number.Integer((candle.High - candle.Low) * binCount));
            }
            return total;
        }
        var mode = 0;
        Number maximumMass = default;
        foreach (var candidate in candidates)
        {
            var mass = Mass(candidate);
            if ((mass - maximumMass).Sign > 0) { maximumMass = mass; mode = candidate; }
        }
        if (maximumMass.Sign == 0) return default;
        var priceMass = comparison < minimum || comparison > maximum ? default : Mass(Bin(comparison));
        var magnitude = (Number.Of(1) - priceMass.Divide(maximumMass)).Times(100);
        if (magnitude.Sign < 0) return default;
        return 2L * binCount * (comparison - minimum) < (2L * mode + 1) * range
            ? magnitude : default(Number) - magnitude;
    }
}
