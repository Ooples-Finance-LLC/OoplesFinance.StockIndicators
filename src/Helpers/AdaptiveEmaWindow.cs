using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveEmaWindow : IDisposable
{
    private readonly int _length;
    private readonly RocBankAverage? _seed;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private RocBankValue _previous;
    private long _count;
    internal AdaptiveEmaWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); var capacity = Math.Min(_length, Math.Max(1, capacityHint));
        _high = new(capacity); _low = new(capacity);
        if (StrengthWindow.Supports(kind)) _seed = new(kind, _length, capacityHint);
        else _fallback = MovingAverageSmootherFactory.Create(kind, _length);
    }
    internal double Next(double price, double high, double low, bool commit, double? selectedSeed = null)
    {
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var seed = selectedSeed.HasValue ? new RocBankValue(selectedSeed.Value) : _seed is not null
            ? _seed.Next(new RocBankValue(price), commit) : new RocBankValue(_fallback!.Next(price, commit));
        var range = ExactVarianceWindow.Units(highest) - ExactVarianceWindow.Units(lowest);
        var distance = BigInteger.Abs(2 * ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(lowest) - ExactVarianceWindow.Units(highest));
        var offset = range.Sign <= 0 ? 0 : Math.Min(1, ExactMeanAccumulator.UnitRatio(distance << 1074, range));
        var rate = (2 / (_length + 1d)) * (1 + offset);
        RocBankValue value;
        if (_count <= _length) value = seed;
        else
        {
            var sum = new ExactMeanAccumulator(); _previous.AddTo(ref sum); sum.AddProduct(price, rate);
            var correction = new ExactMeanAccumulator(); correction.AddProduct(_previous.Mantissa, rate); correction.ScaleByPowerOfTwo(_previous.UpperShift); sum.Subtract(correction);
            value = RocBankValue.Round(sum);
        }
        if (commit) { _previous = value; _count++; }
        return value.Publish();
    }
    internal void Reset() { _seed?.Reset(); _fallback?.Reset(); _high.Reset(); _low.Reset(); _previous = default; _count = 0; }
    public void Dispose() { _seed?.Dispose(); _fallback?.Dispose(); _high.Dispose(); _low.Dispose(); }
}
