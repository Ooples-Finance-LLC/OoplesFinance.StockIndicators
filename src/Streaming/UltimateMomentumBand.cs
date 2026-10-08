namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Stable band position before the composite magnifies it and differentiates it.</summary>
internal sealed class UltimateMomentumBand : IDisposable
{
    private readonly SpreadAverage _center;
    private readonly PooledRingBuffer<double> _window;
    private readonly int _length;
    private readonly double _multiplier;
    internal UltimateMomentumBand(MovingAvgType kind, int length, double multiplier)
    {
        _length = Math.Max(1, length); _multiplier = multiplier;
        _center = new(kind, _length); _window = new(_length);
    }
    internal double Next(double value, bool final)
    {
        var center = _center.Next(new(value), final);
        var kept = Math.Min(_window.Count, _length-1);
        double At(int i) => i == kept ? value : _window[_window.Count-kept+i];
        double position = 0;
        if (kept+1 == _length && _multiplier != 0)
        {
            var mean = new SpreadNumber(0);
            for (var i = 0; i < _length; i++) mean = SpreadNumber.Add(mean, new(At(i)));
            mean = mean.DividedBy(_length);
            double variance = 0;
            for (var i = 0; i < _length; i++)
            {
                var delta = SpreadNumber.Subtract(new(At(i)), mean).Value;
                variance += delta*delta;
            }
            var sigma = Math.Sqrt(variance/_length);
            if (sigma != 0) position = 50+50*SpreadNumber.Subtract(new(value), center).Value/(_multiplier*sigma);
        }
        if (final) _window.TryAdd(value, out _);
        return position;
    }
    internal void Reset() { _center.Reset(); _window.Clear(); }
    public void Dispose() { _center.Dispose(); _window.Dispose(); }
}
