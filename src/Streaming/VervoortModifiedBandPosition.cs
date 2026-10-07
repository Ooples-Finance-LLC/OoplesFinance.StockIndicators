namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Preserves the Heikin-Ashi cascade through band-position normalization.</summary>
internal sealed class VervoortModifiedBandPosition : IDisposable
{
    private readonly SpreadAverage _first, _second, _final, _center;
    private readonly PooledRingBuffer<SpreadNumber> _window;
    private readonly int _length;
    private SpreadNumber _previousInput, _previousOpen;
    internal VervoortModifiedBandPosition(MovingAvgType kind, int band, int smooth)
    {
        _length = Math.Max(1, band);
        _first = new(kind, smooth); _second = new(kind, smooth); _final = new(kind, smooth);
        _center = new(MovingAvgType.WeightedMovingAverage, _length);
        _window = new PooledRingBuffer<SpreadNumber>(_length);
    }
    internal double Next(double source, double high, double low, bool final)
    {
        var input = new SpreadNumber(source);
        var open = SpreadNumber.Add(_previousInput, _previousOpen).DividedBy(2);
        var upper = SpreadNumber.Subtract(new(high), open).Value >= 0 ? new SpreadNumber(high) : open;
        var lower = SpreadNumber.Subtract(new(low), open).Value <= 0 ? new SpreadNumber(low) : open;
        var close = SpreadNumber.Add(SpreadNumber.Add(input, open), SpreadNumber.Add(upper, lower)).DividedBy(4);
        var first = _first.Next(close, final);
        var second = _second.Next(first, final);
        var value = _final.Next(SpreadNumber.Subtract(first.Times(2), second), final);
        var center = _center.Next(value, final);
        var kept = Math.Min(_window.Count, _length-1);
        SpreadNumber At(int i) => i == kept ? value : _window[_window.Count-kept+i];
        double output = 0;
        if (kept+1 == _length)
        {
            var mean = new SpreadNumber(0);
            for (var i = 0; i < _length; i++) mean = SpreadNumber.Add(mean, At(i));
            mean = mean.DividedBy(_length);
            double variance = 0, scale = 0;
            for (var i = 0; i < _length; i++)
            {
                var delta = SpreadNumber.Subtract(At(i), mean).Value;
                variance += delta*delta; scale = Math.Max(scale, Math.Abs(At(i).Value));
            }
            var sigma = Math.Sqrt(variance/_length);
            if (sigma > 1.4210854715202004e-14*scale)
                output = 50+25*SpreadNumber.Subtract(value, center).Value/sigma;
        }
        if (final) { _window.TryAdd(value, out _); _previousInput = input; _previousOpen = open; }
        return output;
    }
    internal void Reset()
    {
        _first.Reset(); _second.Reset(); _final.Reset(); _center.Reset(); _window.Clear();
        _previousInput = _previousOpen = default;
    }
    public void Dispose()
    { _first.Dispose(); _second.Dispose(); _final.Dispose(); _center.Dispose(); _window.Dispose(); }
}
