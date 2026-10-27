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
    internal double Publish()
    {
        // Ordinary stages already have binary64 precision and range. Publishing
        // them is identity except that AddTo deliberately canonicalizes both zeros.
        // Keep extended/nonfinite values on the validating accumulator path.
        if (UpperShift == 0 && Math.Abs(Mantissa) <= double.MaxValue)
            return Mantissa == 0 ? 0 : Mantissa;
        var sum = new ExactMeanAccumulator(); AddTo(ref sum); return sum.Mean(1);
    }
    internal RocBankValue Multiply(double factor)
    {
        var product = new ExactMeanAccumulator(); product.AddProduct(Mantissa, factor);
        product.ScaleByPowerOfTwo(UpperShift);
        return Round(product);
    }
    internal static BigInteger RoundUnits(BigInteger numerator, BigInteger denominator)
    {
#if !NETFRAMEWORK
        if (denominator.IsOne && TryRoundIntegerUnits(numerator, out var integer)) return integer;
        if (!denominator.IsOne && denominator.IsPowerOfTwo
            && TryRoundPowerOfTwoUnits(numerator, denominator, out var binary)) return binary;
        if (TryCompactUnits(numerator, denominator, out var compact)) return compact;
#endif
        for (var shift = 0; ; shift += 32)
        {
            var value = ExactMeanAccumulator.UnitRatio(numerator, denominator << shift);
            if (!double.IsInfinity(value)) return ExactVarianceWindow.Units(value) << shift;
        }
    }
#if !NETFRAMEWORK
    private static bool TryRoundIntegerUnits(BigInteger numerator, out BigInteger result)
    {
        result = numerator;
        var magnitude = BigInteger.Abs(numerator);
        long bits = magnitude.GetBitLength();
        // Leave the highest finite binade and extended overflow rounding unchanged.
        if (bits > 2097) return false;
        if (bits <= 53) return true;
        int shift = (int)bits - 53;
        var trailing = BigInteger.TrailingZeroCount(magnitude);
        if (trailing >= shift) return true;
        // A guard bit and a sticky-bit check give exact nearest-even rounding.
        ulong guarded = (ulong)(magnitude >> (shift - 1));
        ulong rounded = guarded >> 1;
        if ((guarded & 1) != 0 && ((rounded & 1) != 0 || trailing < shift - 1))
            rounded++;
        result = new BigInteger(rounded) << shift;
        if (numerator.Sign < 0) result = -result;
        return true;
    }

    private static bool TryRoundPowerOfTwoUnits(BigInteger numerator, BigInteger denominator, out BigInteger result)
    {
        result = default;
        long denominatorBits = denominator.GetBitLength();
        // Bound shifts; unsupported denominators retain the existing ratio path.
        if (denominatorBits > 4097) return false;
        if (numerator.IsZero) return true;
        int divisorShift = (int)denominatorBits - 1;
        var magnitude = BigInteger.Abs(numerator);
        long bits = magnitude.GetBitLength();
        // Preserve the existing highest-binade and extended-overflow handling.
        if (bits - divisorShift > 2097) return false;
        int grid = (int)Math.Max(0L, bits - divisorShift - 53);
        int discard = divisorShift + grid;
        ulong guarded = (ulong)(magnitude >> (discard - 1));
        ulong rounded = guarded >> 1;
        if ((guarded & 1) != 0 && ((rounded & 1) != 0
            || BigInteger.TrailingZeroCount(magnitude) < discard - 1))
            rounded++;
        result = new BigInteger(rounded) << grid;
        if (numerator.Sign < 0) result = -result;
        return true;
    }

    private static bool TryCompactUnits(BigInteger numerator, BigInteger denominator, out BigInteger result)
    {
        result = default;
        // Keep the original denominator validation and extended-overflow loop.
        if (denominator.Sign <= 0 || numerator.IsZero) return false;
        var magnitude = BigInteger.Abs(numerator);
        var topTrailing = BigInteger.TrailingZeroCount(magnitude);
        var bottomTrailing = BigInteger.TrailingZeroCount(denominator);
        if (topTrailing > int.MaxValue || bottomTrailing > int.MaxValue) return false;
        int topZeros = (int)topTrailing;
        int bottomZeros = (int)bottomTrailing;
        if (magnitude.GetBitLength() - topZeros > 128
            || denominator.GetBitLength() - bottomZeros > 128) return false;
        long scale = (long)topZeros - bottomZeros - 1074;
        if (scale < int.MinValue || scale > int.MaxValue) return false;
        var top = (UInt128)(magnitude >> topZeros);
        var bottom = (UInt128)(denominator >> bottomZeros);
        // Exact midpoint checks certify the rounded normal result; underflow,
        // overflow and unsupported midpoint products retain the original path.
        if (!CertifiedCompactRatio.TryDivide(top, bottom, (int)scale, out double rounded)) return false;
        result = ExactVarianceWindow.Units(numerator.Sign < 0 ? -rounded : rounded);
        return true;
    }
#endif

    internal static RocBankValue Round(ExactMeanAccumulator sum, double unit = 1, long count = 1)
    {
        if (sum.IsExactlyZero) return default;
#pragma warning disable S1244 // The integer mean path applies only to an exact unit multiplier.
        var integerMean = unit == 1 && count > 0;
#pragma warning restore S1244
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
    private readonly Queue<RocBankValue>? _observedHistory;
    private ExactMeanAccumulator _sum, _weighted;
    private RocBankValue _previous;
    private long _count;
    internal RocBankAverage(MovingAvgType kind, int length, int capacityHint, bool observedHistory = false)
    {
        if (!StrengthWindow.Supports(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        _kind = kind; _length = Math.Max(1, length);
        if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
        {
            if (observedHistory) _observedHistory = new();
            else _window = new(Math.Min(_length, Math.Max(1, capacityHint)));
        }
    }
    internal RocBankValue Next(RocBankValue value, bool final)
    {
        var sum = _sum; var weighted = _weighted;
        RocBankValue result;
        if (_window is not null || _observedHistory is not null)
        {
            weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_count >= _length) (_observedHistory is null ? _window![0] : _observedHistory.Peek()).AddTo(ref sum, -1);
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
        if (final)
        {
            _sum = sum; _weighted = weighted; _previous = result; _window?.TryAdd(value, out _);
            if (_observedHistory is not null)
            {
                if (_observedHistory.Count == _length) _observedHistory.Dequeue();
                _observedHistory.Enqueue(value);
            }
            _count++;
        }
        return result;
    }
    internal void Reset() { _sum = _weighted = default; _previous = default; _count = 0; _window?.Clear(); _observedHistory?.Clear(); }
    public void Dispose() { _window?.Dispose(); _observedHistory?.Clear(); }
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
