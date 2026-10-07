using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VerticalHorizontalAverageWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<BigInteger> _prices, _changes;
    private readonly RollingWindowMax _maximum;
    private readonly RollingWindowMin _minimum;
    private BigInteger _sum, _previous;
    private long _count;
    internal VerticalHorizontalAverageWindow(int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); var capacity = Math.Min(_length, Math.Max(1, capacityHint));
        _prices = new(capacity); _changes = new(capacity); _maximum = new(capacity); _minimum = new(capacity);
    }
    internal double Next(double price, bool commit)
    {
        var units = ExactVarianceWindow.Units(price);
        var prior = _count >= _length ? _prices[0] : BigInteger.Zero;
        var change = BigInteger.Abs(units - prior); var sum = _sum + change;
        if (_changes.Count == _changes.Capacity) sum -= _changes[0];
        var high = commit ? _maximum.Add(price, out _) : _maximum.Preview(price, out _);
        var low = commit ? _minimum.Add(price, out _) : _minimum.Preview(price, out _);
        var range = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
        var ratio = sum.IsZero ? BigInteger.Zero : RocBankValue.RoundUnits(range << 1074, sum);
        var gain = RocBankValue.RoundUnits(ratio * ratio, BigInteger.One << 1074);
        var previous = _count == 0 ? units : _previous;
        var result = RocBankValue.RoundUnits((previous << 1074) + gain * (units - previous), BigInteger.One << 1074);
        if (commit) { _prices.TryAdd(units, out _); _changes.TryAdd(change, out _); _sum = sum; _previous = result; _count++; }
        return ExactMeanAccumulator.UnitRatio(result, BigInteger.One);
    }
    internal void Reset() { _prices.Clear(); _changes.Clear(); _maximum.Reset(); _minimum.Reset(); _sum = _previous = default; _count = 0; }
    public void Dispose() { _prices.Dispose(); _changes.Dispose(); _maximum.Dispose(); _minimum.Dispose(); }
}
