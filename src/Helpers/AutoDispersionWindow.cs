using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AutoDispersionWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<BigInteger> _squares;
    private readonly Extremum _maximum, _minimum;
    private readonly RocBankAverage? _upperFirst, _upperSecond, _lowerFirst, _lowerSecond;
    private readonly IMovingAverageSmoother? _upperFirstFallback, _upperSecondFallback, _lowerFirstFallback, _lowerSecondFallback;
    private BigInteger _sum;
    private long _count;
    internal AutoDispersionWindow(MovingAvgType kind, int length, int smoothLength, bool external = false, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); var capacity = Math.Min(_length, Math.Max(1, capacityHint)); _prices = new(capacity); _squares = new(capacity); _maximum = new(_length, capacity, true); _minimum = new(_length, capacity, false);
        if (!external) { if (StrengthWindow.Supports(kind)) { _upperFirst = new(kind, _length, capacityHint); _upperSecond = new(kind, smoothLength, capacityHint); _lowerFirst = new(kind, _length, capacityHint); _lowerSecond = new(kind, smoothLength, capacityHint); } else { _upperFirstFallback = MovingAverageSmootherFactory.Create(kind, _length); _upperSecondFallback = MovingAverageSmootherFactory.Create(kind, smoothLength); _lowerFirstFallback = MovingAverageSmootherFactory.Create(kind, _length); _lowerSecondFallback = MovingAverageSmootherFactory.Create(kind, smoothLength); } }
    }
    private static RocBankValue Value(BigInteger units) { var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); return RocBankValue.Round(sum); }
    internal (RocBankValue Upper, RocBankValue Lower) Generate(double close, bool commit)
    {
        var price = ExactVarianceWindow.Units(close); var change = _count < _length ? BigInteger.Zero : price - ExactVarianceWindow.Units(_prices[0]); var square = change * change; var sum = _sum + square; if (_count >= _length) sum -= _squares[0]; var divisor = new BigInteger(Math.Min(_length, _count + 1)); var root = ExactPopulationDeviation.RootRatio(sum, divisor);
        var width = double.IsInfinity(root) ? ExactVarianceWindow.Units(ExactPopulationDeviation.RootRatio(sum, divisor << 2048)) << 1024 : ExactVarianceWindow.Units(root);
        var upper = _maximum.Next(RocBankValue.RoundUnits(price + width, BigInteger.One), _count, commit); var lower = _minimum.Next(RocBankValue.RoundUnits(price - width, BigInteger.One), _count, commit);
        if (commit) { _prices.TryAdd(close, out _); _squares.TryAdd(square, out _); _sum = sum; _count++; }
        return (Value(upper), Value(lower));
    }
    internal static (double Upper, double Middle, double Lower) Bands(RocBankValue upper, RocBankValue lower)
    { var sum = new ExactMeanAccumulator(); upper.AddTo(ref sum); lower.AddTo(ref sum); return (upper.Publish(), sum.Mean(2), lower.Publish()); }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var raw = Generate(close, commit);
        var firstUpper = _upperFirst is not null ? _upperFirst.Next(raw.Upper, commit) : new RocBankValue(_upperFirstFallback!.Next(raw.Upper.Publish(), commit)); var firstLower = _lowerFirst is not null ? _lowerFirst.Next(raw.Lower, commit) : new RocBankValue(_lowerFirstFallback!.Next(raw.Lower.Publish(), commit));
        var upper = _upperSecond is not null ? _upperSecond.Next(firstUpper, commit) : new RocBankValue(_upperSecondFallback!.Next(firstUpper.Publish(), commit)); var lower = _lowerSecond is not null ? _lowerSecond.Next(firstLower, commit) : new RocBankValue(_lowerSecondFallback!.Next(firstLower.Publish(), commit)); return Bands(upper, lower);
    }
    internal void Reset() { _prices.Clear(); _squares.Clear(); _maximum.Reset(); _minimum.Reset(); _upperFirst?.Reset(); _upperSecond?.Reset(); _lowerFirst?.Reset(); _lowerSecond?.Reset(); _upperFirstFallback?.Reset(); _upperSecondFallback?.Reset(); _lowerFirstFallback?.Reset(); _lowerSecondFallback?.Reset(); _sum = default; _count = 0; }
    public void Dispose() { _prices.Dispose(); _squares.Dispose(); _upperFirst?.Dispose(); _upperSecond?.Dispose(); _lowerFirst?.Dispose(); _lowerSecond?.Dispose(); _upperFirstFallback?.Dispose(); _upperSecondFallback?.Dispose(); _lowerFirstFallback?.Dispose(); _lowerSecondFallback?.Dispose(); }
    private sealed class Extremum
    {
        private readonly BigInteger[] _values;
        private readonly long[] _indices;
        private readonly int _length;
        private readonly bool _maximum;
        private int _start, _count;
        internal Extremum(int length, int capacity, bool maximum) { _length = length; _values = new BigInteger[capacity]; _indices = new long[capacity]; _maximum = maximum; }
        private bool Better(BigInteger a, BigInteger b) => _maximum ? a >= b : a <= b;
        internal BigInteger Next(BigInteger value, long index, bool commit)
        {
            if (!commit) { var skip = _count > 0 && _indices[_start] <= index - _length ? 1 : 0; if (_count == skip) return value; var first = _values[(_start + skip) % _values.Length]; return Better(value, first) ? value : first; }
            if (_count > 0 && _indices[_start] <= index - _length) { _start = (_start + 1) % _values.Length; _count--; }
            while (_count > 0 && Better(value, _values[(_start + _count - 1) % _values.Length])) _count--;
            var tail = (_start + _count) % _values.Length; _values[tail] = value; _indices[tail] = index; _count++; return _values[_start];
        }
        internal void Reset() { _start = _count = 0; }
    }
}
