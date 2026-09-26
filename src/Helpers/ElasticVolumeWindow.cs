using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ElasticVolumeWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _volumes;
    private BigInteger _sum;
    private RocBankValue _previous;
    private bool _started;
    internal ElasticVolumeWindow(int length) => _volumes = new(Math.Max(1, length));
    internal double Next(double price, double volume, bool commit)
    {
        var current = ExactVarianceWindow.Units(volume);
        var expired = _volumes.Count == _volumes.Capacity ? ExactVarianceWindow.Units(_volumes[0]) : BigInteger.Zero;
        var sum = _sum + current - expired;
        var previous = _started ? _previous : new RocBankValue(price);
        var value = previous;
        if (sum.Sign > 0)
        {
            var top = new ExactMeanAccumulator();
            top.Add(previous.Mantissa, (sum - current) << previous.UpperShift); top.Add(price, current);
            for (var shift = 0; ; shift += 1024)
            {
                var bottom = new ExactMeanAccumulator(); bottom.Add(1, sum << shift);
                var rounded = top.Ratio(bottom);
                if (!double.IsInfinity(rounded)) { value = new RocBankValue(rounded, shift); break; }
            }
        }
        if (commit) { _volumes.TryAdd(volume, out _); _sum = sum; _previous = value; _started = true; }
        return value.Publish();
    }
    internal void Reset() { _volumes.Clear(); _sum = default; _previous = default; _started = false; }
    public void Dispose() => _volumes.Dispose();
}
