namespace OoplesFinance.StockIndicators.Helpers;

// Price minus its arithmetic-seeded EMA, computed without subtracting nearly equal price levels.
internal sealed class SeededEmaResidual
{
    private readonly int _length;
    private readonly double _retention;
    private int _count;
    private double _previous, _residual;

    internal SeededEmaResidual(int length)
    {
        _length = Math.Max(1, length);
        _retention = 1 - 2d / (_length + 1);
    }

    internal double Next(double value, bool commit)
    {
        var retention = _count < _length ? (double)_count / (_count + 1) : _retention;
        var result = _count == 0 ? 0 : retention * (_residual + (value - _previous));
        if (commit)
        {
            _previous = value;
            _residual = result;
            if (_count < _length) _count++;
        }
        return result;
    }

    internal void Reset() { _count = 0; _previous = _residual = 0; }
}
