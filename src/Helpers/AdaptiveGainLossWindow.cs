namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RapidGainLossWindow : IDisposable
{
    private readonly IntradayGainLossWindow _changes;
    private double _previous;
    private bool _hasPrevious;
    internal RapidGainLossWindow(int length, int capacityHint = int.MaxValue) => _changes = new(length, capacityHint);
    internal double Next(double price, bool final)
    {
        var value = _changes.Next(price, _hasPrevious ? _previous : price, final, 100);
        if (final) { _previous = price; _hasPrevious = true; }
        return value;
    }
    internal void Reset() { _changes.Reset(); _previous = 0; _hasPrevious = false; }
    public void Dispose() => _changes.Dispose();
}

internal sealed class AsymmetricGainLossWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<bool> _flags;
    private int _positiveCount;
    private RocBankValue _up, _down;
    private double _previous;
    internal AsymmetricGainLossWindow(int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length);
        _flags = new(Math.Min(_length, Math.Max(1, capacityHint)));
    }
    private static RocBankValue Smooth(RocBankValue previous, RocBankValue current, int count)
    {
        if (count == 0) return previous;
        var sum = new ExactMeanAccumulator(); previous.AddTo(ref sum, count - 1L); current.AddTo(ref sum);
        return RocBankValue.Round(sum, count: count);
    }
    internal double Next(double price, bool final)
    {
        var change = RocBankValue.Return(price, _previous);
        var positive = change.Mantissa >= 0;
        var count = _positiveCount + (positive ? 1 : 0);
        if (_flags.Count == _length && _flags[0]) count--;
        var up = Smooth(_up, change.Mantissa > 0 ? change : default, count);
        var down = Smooth(_down, change.Mantissa < 0 ? new RocBankValue(-change.Mantissa, change.UpperShift) : default, _length - count);
        var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, 100);
        var total = new ExactMeanAccumulator(); up.AddTo(ref total); down.AddTo(ref total);
        var value = GainLossShare.Of(numerator, total, 100);
        if (final) { _up = up; _down = down; _previous = price; _positiveCount = count; _flags.TryAdd(positive, out _); }
        return value;
    }
    internal void Reset() { _flags.Clear(); _positiveCount = 0; _up = _down = default; _previous = 0; }
    public void Dispose() => _flags.Dispose();
}
