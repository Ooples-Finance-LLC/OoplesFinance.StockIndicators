using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// A return can exceed binary64 even when weighted legs later cancel. Keep each
// rounded unpublished stage at binary64 precision with an extended upper exponent.
// Ordinary/subnormal stages retain normal binary64 rounding.
internal readonly struct RocBankValue
{
    internal readonly double Mantissa;
    internal readonly int UpperShift;
    internal RocBankValue(double mantissa, int upperShift = 0) { Mantissa = mantissa; UpperShift = upperShift; }
    internal void AddTo(ref ExactMeanAccumulator sum, long weight = 1)
    {
        if (Mantissa == 0 || weight == 0) return;
        if (UpperShift == 0 && weight >= int.MinValue && weight <= int.MaxValue) sum.Add(Mantissa, (int)weight);
        else sum.Add(Mantissa, new BigInteger(weight) << UpperShift);
    }
    internal double Publish() { var sum = new ExactMeanAccumulator(); AddTo(ref sum); return sum.Mean(1); }
    internal RocBankValue Multiply(double factor)
    {
        var product = new ExactMeanAccumulator(); product.AddProduct(Mantissa, factor);
        product.ScaleByPowerOfTwo(UpperShift);
        return Round(product);
    }
    internal static RocBankValue Round(ExactMeanAccumulator sum, double unit = 1, long count = 1)
    {
        if (sum.IsExactlyZero) return default;
        var integerMean = unit == 1 && count > 0;
        if (integerMean)
        {
            var mean = sum.Mean(count);
            if (!double.IsInfinity(mean)) return new RocBankValue(mean);
        }
        for (var shift = integerMean ? 1024 : 0; ; shift += 1024)
        {
            var denominator = new ExactMeanAccumulator(); denominator.Add(unit, new BigInteger(count) << shift);
            var value = sum.Ratio(denominator);
            if (!double.IsInfinity(value)) return new RocBankValue(value, shift);
        }
    }
    internal static RocBankValue Return(double current, double previous)
    {
        if (previous == 0) return default;
        var numerator = new ExactMeanAccumulator(); numerator.Add(current, 100); numerator.Add(previous, -100);
        return Round(numerator, previous);
    }
}

internal sealed class RocBankAverage : IDisposable
{
    private readonly MovingAvgType _kind;
    private readonly int _length;
    private readonly PooledRingBuffer<RocBankValue>? _window;
    private ExactMeanAccumulator _sum, _weighted;
    private RocBankValue _previous;
    private long _count;
    internal RocBankAverage(MovingAvgType kind, int length, int capacityHint)
    {
        if (!StrengthWindow.Supports(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        _kind = kind; _length = Math.Max(1, length);
        if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            _window = new(Math.Min(_length, Math.Max(1, capacityHint)));
    }
    internal RocBankValue Next(RocBankValue value, bool final)
    {
        var sum = _sum; var weighted = _weighted;
        RocBankValue result;
        if (_window is not null)
        {
            weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_count >= _length) _window[0].AddTo(ref sum, -1);
            value.AddTo(ref sum);
            result = _kind == MovingAvgType.WeightedMovingAverage
                ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _count + 1 < _length ? default : RocBankValue.Round(sum, count: _length);
        }
        else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
        {
            value.AddTo(ref sum); result = RocBankValue.Round(sum, count: _count + 1);
        }
        else
        {
            var next = new ExactMeanAccumulator();
            var ema = _kind == MovingAvgType.ExponentialMovingAverage;
            _previous.AddTo(ref next, _length - 1L); value.AddTo(ref next, ema ? 2 : 1);
            result = RocBankValue.Round(next, count: ema ? _length + 1L : _length);
        }
        if (final) { _sum = sum; _weighted = weighted; _previous = result; _window?.TryAdd(value, out _); _count++; }
        return result;
    }
    internal void Reset() { _sum = _weighted = default; _previous = default; _count = 0; _window?.Clear(); }
    public void Dispose() => _window?.Dispose();
}

internal sealed class RocBankWindow : IDisposable
{
    private readonly int[] _lookbacks, _weights;
    private readonly PooledRingBuffer<double> _prices;
    private readonly RocBankAverage[] _legs;
    private readonly RocBankAverage _signal;
    private long _count;
    internal RocBankWindow(MovingAvgType kind, int[] lookbacks, int[] smoothing, int[] weights, int signal, int capacityHint = int.MaxValue)
    {
        if (lookbacks.Length != smoothing.Length || lookbacks.Length != weights.Length || lookbacks.Length == 0)
            throw new ArgumentException("Each ROC leg requires a lookback, smoothing period, and weight.");
        _lookbacks = lookbacks.Select(n => Math.Max(1, n)).ToArray(); _weights = weights.ToArray();
        _prices = new PooledRingBuffer<double>(Math.Min(_lookbacks.Max(), Math.Max(1, capacityHint)));
        _legs = smoothing.Select(n => new RocBankAverage(kind, n, capacityHint)).ToArray();
        _signal = new RocBankAverage(kind, signal, capacityHint);
    }
    internal (double Value, double Signal) Next(double price, bool final)
    {
        var total = new ExactMeanAccumulator();
        for (var i = 0; i < _legs.Length; i++)
        {
            var change = _count < _lookbacks[i] ? default : RocBankValue.Return(price, _prices[_prices.Count - _lookbacks[i]]);
            _legs[i].Next(change, final).AddTo(ref total, _weights[i]);
        }
        var value = RocBankValue.Round(total);
        var signal = _signal.Next(value, final);
        if (final) { _prices.TryAdd(price, out _); _count++; }
        return (value.Publish(), signal.Publish());
    }
    internal void Reset() { foreach (var leg in _legs) leg.Reset(); _signal.Reset(); _prices.Clear(); _count = 0; }
    public void Dispose() { foreach (var leg in _legs) leg.Dispose(); _signal.Dispose(); _prices.Dispose(); }
}
