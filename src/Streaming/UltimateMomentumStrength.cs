namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Preserves composite gain/loss precision through RSI and its output average.</summary>
internal sealed class UltimateMomentumStrength : IDisposable
{
    private readonly SpreadAverage _gain, _loss, _signal;
    private readonly PooledRingBuffer<double> _changes;
    private readonly bool _finiteWindow, _holdFlatRatio;
    private readonly int _length;
    private double _previous, _previousStrength;
    private int _gains, _losses;
    private bool _hasPrevious;

    internal UltimateMomentumStrength(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length);
        _gain = new(kind, _length); _loss = new(kind, _length); _signal = new(kind, _length);
        _changes = new PooledRingBuffer<double>(_length);
        _finiteWindow = kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
        _holdFlatRatio = _length > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    }

    internal double Next(double value, bool final)
    {
        var change = _hasPrevious ? value-_previous : 0;
        var outgoing = _changes.Count == _length ? _changes[0] : 0;
        var gains = _gains+(change > 0 ? 1 : 0)-(outgoing > 0 ? 1 : 0);
        var losses = _losses+(change < 0 ? 1 : 0)-(outgoing < 0 ? 1 : 0);
        var gain = _gain.Next(new(Math.Max(0, change)), final).Value;
        var loss = _loss.Next(new(Math.Max(0, -change)), final).Value;
        if (_finiteWindow && gains == 0) gain = 0;
        if (_finiteWindow && losses == 0) loss = 0;
        var strength = loss == 0 ? 100 : gain == 0 ? 0 : Math.Max(0, Math.Min(100, 100*gain/(gain+loss)));
        if (_holdFlatRatio && _hasPrevious && change == 0) strength = _previousStrength;
        var result = _signal.Next(new(strength), final).Value;
        if (final)
        {
            _changes.TryAdd(change, out _); _gains = gains; _losses = losses;
            _previous = value; _previousStrength = strength; _hasPrevious = true;
        }
        return result;
    }

    internal void Reset()
    {
        _gain.Reset(); _loss.Reset(); _signal.Reset(); _changes.Clear();
        _previous = _previousStrength = 0; _gains = _losses = 0; _hasPrevious = false;
    }
    public void Dispose() { _gain.Dispose(); _loss.Dispose(); _signal.Dispose(); _changes.Dispose(); }
}
