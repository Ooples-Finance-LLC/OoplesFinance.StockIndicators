using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Conditional RMS of simple-return shortfalls. Keep rational returns until the
// final root: an overflowing return can still have a representable RMS.
internal sealed class ExactDownsideWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _prices;
    private readonly BigInteger _target;
    internal ExactDownsideWindow(int length, double target = 0)
    {
        _target = ExactVarianceWindow.Units(target);
        _prices = new PooledRingBuffer<double>(Math.Max(1, length));
    }

    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        double result = 0;
        if (_prices.Count == _prices.Capacity)
        {
            BigInteger sum = 0, denominator = 1;
            var shortfalls = 0;
            for (var j = 0; j < _prices.Count; j++)
            {
                var previous = ExactVarianceWindow.Units(_prices[j]);
                var next = j + 1 == _prices.Count ? current : ExactVarianceWindow.Units(_prices[j + 1]);
                var divisor = previous.Sign > 0 ? previous << 1074 : BigInteger.One << 1074;
                var difference = previous.Sign > 0 ? ((next - previous) << 1074) - _target * previous : -_target;
                if (difference.Sign >= 0) continue;
                shortfalls++;
                var gcd = BigInteger.GreatestCommonDivisor(difference, divisor);
                difference /= gcd; divisor /= gcd;
                var square = divisor * divisor;
                var common = BigInteger.GreatestCommonDivisor(denominator, square);
                sum = sum * (square / common) + difference * difference * (denominator / common);
                denominator *= square / common;
                gcd = BigInteger.GreatestCommonDivisor(sum, denominator);
                sum /= gcd; denominator /= gcd;
            }
            result = shortfalls == 0 ? 0 : ExactPopulationDeviation.RootRatio(sum << 2148, denominator * shortfalls);
        }
        if (commit) _prices.TryAdd(value, out _);
        return result;
    }
    internal void Reset() => _prices.Clear();
    public void Dispose() => _prices.Dispose();
}
