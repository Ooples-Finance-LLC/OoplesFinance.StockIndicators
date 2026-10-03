using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CycleAmplitudeWindow : IDisposable
{
    private readonly ClampedBandPassWindow _filter;
    private readonly PooledRingBuffer<BigInteger> _lag, _pairs;
    private readonly int _length, _delay;
    private BigInteger _sum;
    private long _count;
    internal CycleAmplitudeWindow(int length, double delta, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); _delay = (int)((_length + 3L) / 4); _filter = new(_length, delta, 2);
        _lag = new(Math.Min(_delay, Math.Max(1, capacityHint))); _pairs = new(Math.Min(_length, Math.Max(1, capacityHint)));
    }
    internal double Next(double close, bool commit)
    {
        _filter.Next(close, commit, out var band); var square = band * band;
        var pair = square + (_count >= _delay ? _lag[0] : BigInteger.Zero);
        var sum = _sum + pair - (_count >= _length ? _pairs[0] : BigInteger.Zero);
        var root = ExactPopulationDeviation.RootRatio(sum, new BigInteger(_length));
        var amplitude = double.IsInfinity(root) ? double.PositiveInfinity : KeltnerWindow.Bands(new RocBankValue(0), new RocBankValue(root), 2 * 1.414).Upper;
        if (commit) { _lag.TryAdd(square, out _); _pairs.TryAdd(pair, out _); _sum = sum; _count++; }
        return amplitude;
    }
    internal void Reset() { _filter.Reset(); _lag.Clear(); _pairs.Clear(); _sum = default; _count = 0; }
    public void Dispose() { _lag.Dispose(); _pairs.Dispose(); }
}
