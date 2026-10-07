using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RocBandWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<BigInteger> _squares;
    private readonly RocBankAverage? _signal;
    private readonly IMovingAverageSmoother? _fallback;
    private BigInteger _sum;
    private long _count;
    internal RocBandWindow(MovingAvgType kind, int length, int smoothLength, bool external = false, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); var capacity = Math.Min(_length, Math.Max(1, capacityHint)); _prices = new(capacity); _squares = new(capacity);
        if (!external) { if (StrengthWindow.Supports(kind)) _signal = new(kind, smoothLength, capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, smoothLength)); }
    }
    internal (double Upper, double Roc, double RawReturn) Next(double close, bool commit)
    {
        var value = _count < _length ? default : RocBankValue.Return(close, _prices[0]);
        var units = ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift; var square = units * units;
        var sum = _sum + square; if (_count >= _length) sum -= _squares[0];
        var upper = ExactPopulationDeviation.RootRatio(sum, new BigInteger(Math.Min(_length, _count + 1)));
        var roc = _signal is not null ? _signal.Next(value, commit).Publish() : _fallback?.Next(value.Publish(), commit) ?? 0;
        if (commit) { _prices.TryAdd(close, out _); _squares.TryAdd(square, out _); _sum = sum; _count++; }
        return (upper, roc, value.Publish());
    }
    internal void Reset() { _prices.Clear(); _squares.Clear(); _signal?.Reset(); _fallback?.Reset(); _sum = BigInteger.Zero; _count = 0; }
    public void Dispose() { _prices.Dispose(); _squares.Dispose(); _signal?.Dispose(); _fallback?.Dispose(); }
}
