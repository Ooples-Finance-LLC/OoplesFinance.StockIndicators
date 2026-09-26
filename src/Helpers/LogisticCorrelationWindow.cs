using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Partial-window Pearson correlation against consecutive time indices. All price
// moments are exact; local indices avoid loss of resolution in long-running streams.
internal sealed class LogisticCorrelationWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly double _gain;
    private BigInteger _sum, _squares, _weighted;
    internal LogisticCorrelationWindow(int length, double gain)
    {
        if (double.IsNaN(gain) || double.IsInfinity(gain)) throw new ArgumentOutOfRangeException(nameof(gain));
        _values = new PooledRingBuffer<double>(Math.Max(1, length)); _gain = gain;
    }
    internal double Next(double value, bool commit)
    {
        var current = ExactVarianceWindow.Units(value);
        var full = _values.Count == _values.Capacity;
        var expired = full ? ExactVarianceWindow.Units(_values[0]) : BigInteger.Zero;
        var count = full ? _values.Count : _values.Count + 1;
        var sum = _sum + current - expired;
        var squares = _squares + current * current - expired * expired;
        var weighted = full ? _weighted - _sum + expired + (count - 1) * current : _weighted + (count - 1) * current;
        var n = new BigInteger(count);
        var covariance = 2 * weighted - (n - 1) * sum;
        var spread = n * squares - sum * sum;
        var correlation = spread.IsZero || count == 1 ? 0 : covariance.Sign * ExactPopulationDeviation.RootRatio(
            (3 * covariance * covariance) << 2148, (n * n - 1) * spread);
        // Preserve the library's exponent ceiling, including the small nonzero floor.
        var result = 1 / (1 + Math.Exp(Math.Min(100, -_gain * correlation)));
        if (commit) { _values.TryAdd(value, out _); _sum = sum; _squares = squares; _weighted = weighted; }
        return result;
    }
    internal void Reset() { _values.Clear(); _sum = _squares = _weighted = default; }
    public void Dispose() => _values.Dispose();
}
