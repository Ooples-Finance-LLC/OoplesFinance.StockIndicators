using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VerticalHorizontalWindow : IDisposable
{
    private readonly RollingWindowMax _maximum;
    private readonly RollingWindowMin _minimum;
    private readonly PooledRingBuffer<BigInteger> _changes;
    private BigInteger _sum, _previous;
    private bool _started;
    internal VerticalHorizontalWindow(int length, int capacityHint = int.MaxValue)
    {
        var capacity = Math.Min(Math.Max(1, length), Math.Max(1, capacityHint));
        _maximum = new(capacity); _minimum = new(capacity); _changes = new(capacity);
    }
    internal double Next(double price, bool commit)
    {
        var units = ExactVarianceWindow.Units(price);
        var high = commit ? _maximum.Add(price, out _) : _maximum.Preview(price, out _);
        var low = commit ? _minimum.Add(price, out _) : _minimum.Preview(price, out _);
        var change = _started ? BigInteger.Abs(units - _previous) : BigInteger.Zero;
        var sum = _sum + change;
        if (_changes.Count == _changes.Capacity) sum -= _changes[0];
        var range = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
        var result = sum.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(range << 1074, sum);
        if (commit) { _changes.TryAdd(change, out _); _sum = sum; _previous = units; _started = true; }
        return result;
    }
    internal static void SmoothSignal(ReadOnlySpan<double> input, Span<double> output, MovingAvgType kind, int length)
    {
        using var average = new RocBankAverage(kind, Math.Max(1, length), Math.Max(1, input.Length));
        for (var i = 0; i < input.Length; i++) output[i] = average.Next(new RocBankValue(input[i]), true).Publish();
    }
    internal void Reset() { _maximum.Reset(); _minimum.Reset(); _changes.Clear(); _sum = _previous = default; _started = false; }
    public void Dispose() { _maximum.Dispose(); _minimum.Dispose(); _changes.Dispose(); }
}
