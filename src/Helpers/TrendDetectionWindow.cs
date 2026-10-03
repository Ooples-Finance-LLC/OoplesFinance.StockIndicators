namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TrendDetectionWindow : IDisposable
{
    private readonly int _short, _long;
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<RocBankValue> _momenta;
    private ExactMeanAccumulator _direction, _shortAbsolute, _longAbsolute;
    internal TrendDetectionWindow(int shortLength, int longLength)
    {
        _short = Math.Max(1, shortLength); _long = Math.Max(1, longLength);
        _prices = new(_short); _momenta = new(Math.Max(_short, _long));
    }
    private static RocBankValue Absolute(RocBankValue value) => new(Math.Abs(value.Mantissa), value.UpperShift);
    internal (double Line, double Direction) Next(double price, bool commit)
    {
        var change = new ExactMeanAccumulator();
        if (_prices.Count == _short) { change.Add(price); change.Add(_prices[0], -1); }
        var momentum = RocBankValue.Round(change);
        var direction = _direction; var shortAbsolute = _shortAbsolute; var longAbsolute = _longAbsolute;
        if (_momenta.Count >= _short)
        {
            var old = _momenta[_momenta.Count - _short]; old.AddTo(ref direction, -1); Absolute(old).AddTo(ref shortAbsolute, -1);
        }
        if (_momenta.Count >= _long) Absolute(_momenta[_momenta.Count - _long]).AddTo(ref longAbsolute, -1);
        momentum.AddTo(ref direction); Absolute(momentum).AddTo(ref shortAbsolute); Absolute(momentum).AddTo(ref longAbsolute);
        var line = shortAbsolute; line.Subtract(longAbsolute);
        // Add the magnitude of the exact direction total without publishing it:
        // an overflowing direction can still cancel the long absolute total.
        var negativeMagnitude = direction;
        if (direction.Sign >= 0) { negativeMagnitude = default; negativeMagnitude.Subtract(direction); }
        line.Subtract(negativeMagnitude);
        if (commit)
        {
            _prices.TryAdd(price, out _); _momenta.TryAdd(momentum, out _);
            _direction = direction; _shortAbsolute = shortAbsolute; _longAbsolute = longAbsolute;
        }
        return (line.Mean(1), direction.Mean(1));
    }
    internal void Reset() { _prices.Clear(); _momenta.Clear(); _direction = default; _shortAbsolute = default; _longAbsolute = default; }
    public void Dispose() { _prices.Dispose(); _momenta.Dispose(); }
}
