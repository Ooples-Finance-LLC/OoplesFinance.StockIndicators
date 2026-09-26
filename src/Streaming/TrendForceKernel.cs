namespace OoplesFinance.StockIndicators.Streaming;

// Translation removes an irrelevant price offset from arithmetic-seeded EMAs. A log-domain
// ratio avoids underflow/overflow of a fourth-degree force before its normalization.
internal sealed class TrendForceKernel : IDisposable
{
    private readonly SpreadAverage _first, _second;
    private readonly ExactTrendForceWindow? _exact;
    private readonly RollingWindowMax _logMaximum;
    private readonly bool _center;
    private bool _hasAnchor;
    private double _anchor, _previousFirst, _previousSecond;
    internal TrendForceKernel(MovingAvgType kind, int length, int lookback)
    {
        var period = Math.Max(1, Math.Min(530, (int)Math.Ceiling(Math.Max(1, length) / 2d)));
        _first = new(kind, period); _second = new(kind, period);
        _logMaximum = new(Math.Max(1, lookback));
        _center = kind == MovingAvgType.ExponentialMovingAverage;
        if (StrengthWindow.Supports(kind)) _exact = new ExactTrendForceWindow(kind, length, lookback);
    }
    internal double Next(double value, bool final)
    {
        if (_exact is not null) return _exact.Next(value, final);
        var anchor = _hasAnchor ? _anchor : value;
        var source = _center ? value - anchor : value * 1000;
        var first = _first.Next(new SpreadNumber(source), final);
        var second = _second.Next(first, final);
        var gap = Math.Abs(first.Value - second.Value);
        var change = ((first.Value - _previousFirst) + (second.Value - _previousSecond)) / 2;
        var logForce = gap == 0 || change == 0 ? double.NegativeInfinity : Math.Log(gap) + 3 * Math.Log(Math.Abs(change));
        var maximum = final ? _logMaximum.Add(logForce, out _) : _logMaximum.Preview(logForce, out _);
        var result = double.IsNegativeInfinity(logForce) ? 0 : Math.Sign(change) * Math.Exp(logForce - maximum);
        if (final)
        {
            _hasAnchor = true; _anchor = anchor; _previousFirst = first.Value; _previousSecond = second.Value;
        }
        return result;
    }
    internal void Reset()
    {
        _exact?.Reset();
        _first.Reset(); _second.Reset(); _logMaximum.Reset();
        _hasAnchor = false; _anchor = _previousFirst = _previousSecond = 0;
    }
    public void Dispose() { _exact?.Dispose(); _first.Dispose(); _second.Dispose(); _logMaximum.Dispose(); }
}
