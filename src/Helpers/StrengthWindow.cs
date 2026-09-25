using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// A difference of finite binary64 prices needs at most one extra exponent bit.
// Convex smoothing preserves that bound. Ordinary and subnormal values retain
// their original scale; globally halving prices would erase tiny changes.
internal readonly struct StrengthValue
{
    internal readonly double Mantissa;
    internal readonly bool Doubled;
    internal StrengthValue(double mantissa, bool doubled = false) { Mantissa = mantissa; Doubled = doubled; }
    internal void AddTo(ref ExactMeanAccumulator sum, long weight = 1) =>
        sum.Add(Mantissa, new BigInteger(weight) * (Doubled ? 2 : 1));
    internal StrengthValue Absolute => new(Math.Abs(Mantissa), Doubled);
    internal static StrengthValue Round(ExactMeanAccumulator sum, long divisor)
    {
        var value = sum.Mean(divisor);
        return double.IsInfinity(value) ? new(sum.Mean(checked(2 * divisor)), true) : new(value);
    }
}

internal sealed class StrengthAverage : IDisposable
{
    private readonly MovingAvgType _kind;
    private readonly int _length;
    private readonly PooledRingBuffer<StrengthValue>? _window;
    private ExactMeanAccumulator _sum, _weighted;
    private StrengthValue _previous;
    private long _count;

    internal StrengthAverage(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        _kind = kind; _length = Math.Max(1, length);
        if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            _window = new(Math.Min(_length, Math.Max(1, capacityHint)));
    }

    internal StrengthValue Next(StrengthValue value, bool final)
    {
        var sum = _sum; var weighted = _weighted;
        StrengthValue result;
        if (_window is not null)
        {
            weighted.Subtract(sum);
            value.AddTo(ref weighted, _length);
            if (_count >= _length) _window[0].AddTo(ref sum, -1);
            value.AddTo(ref sum);
            result = _kind == MovingAvgType.WeightedMovingAverage
                ? StrengthValue.Round(weighted, (long)_length * (_length + 1L) / 2)
                : _count + 1 < _length ? default : StrengthValue.Round(sum, _length);
        }
        else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
        {
            value.AddTo(ref sum);
            result = StrengthValue.Round(sum, _count + 1);
        }
        else
        {
            var next = new ExactMeanAccumulator();
            var ema = _kind == MovingAvgType.ExponentialMovingAverage;
            _previous.AddTo(ref next, _length - 1L);
            value.AddTo(ref next, ema ? 2 : 1);
            result = StrengthValue.Round(next, ema ? _length + 1L : _length);
        }
        if (final)
        {
            _sum = sum; _weighted = weighted; _previous = result;
            _window?.TryAdd(value, out _); _count++;
        }
        return result;
    }
    internal void Reset() { _sum = _weighted = default; _previous = default; _count = 0; _window?.Clear(); }
    public void Dispose() => _window?.Dispose();
}

internal sealed class StrengthWindow : IDisposable
{
    private readonly StrengthAverage[] _signed, _absolute;
    private double _previous;
    private bool _hasPrevious;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.SimpleMovingAverage
        or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    internal StrengthWindow(MovingAvgType kind, int[] lengths, int capacityHint = int.MaxValue)
    {
        if (!Supports(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        _signed = lengths.Select(n => new StrengthAverage(kind, n, capacityHint)).ToArray();
        _absolute = lengths.Select(n => new StrengthAverage(kind, n, capacityHint)).ToArray();
    }
    internal double Next(double price, bool final)
    {
        var change = new ExactMeanAccumulator();
        if (_hasPrevious) { change.Add(price); change.Add(_previous, -1); }
        var result = NextChange(StrengthValue.Round(change, 1), final);
        if (final) { _previous = price; _hasPrevious = true; }
        return result;
    }
    internal double NextChange(StrengthValue signed, bool final)
    {
        var absolute = signed.Absolute;
        for (var i = 0; i < _signed.Length; i++)
        {
            signed = _signed[i].Next(signed, final);
            absolute = _absolute[i].Next(absolute, final);
        }
        var numerator = new ExactMeanAccumulator(); signed.AddTo(ref numerator, 100);
        var denominator = new ExactMeanAccumulator(); absolute.AddTo(ref denominator);
        var result = Math.Max(-100, Math.Min(100, numerator.Ratio(denominator)));
        return result;
    }
    internal static double[] Compute(IReadOnlyList<double> values, MovingAvgType kind, params int[] lengths)
    {
        using var window = new StrengthWindow(kind, lengths, values.Count);
        var result = new double[values.Count];
        for (var i = 0; i < result.Length; i++) result[i] = window.Next(values[i], true);
        return result;
    }
    internal static List<double> Smooth(IReadOnlyList<double> values, MovingAvgType kind, int length)
    {
        using var stage = new StrengthAverage(kind, length, values.Count);
        var result = new List<double>(values.Count);
        foreach (var value in values) result.Add(stage.Next(new StrengthValue(value), true).Mantissa);
        return result;
    }
    internal void Reset() { foreach (var stage in _signed.Concat(_absolute)) stage.Reset(); _previous = 0; _hasPrevious = false; }
    public void Dispose() { foreach (var stage in _signed.Concat(_absolute)) stage.Dispose(); }
}
