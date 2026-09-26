namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Preserves low components until the final band-position division.</summary>
internal sealed class VervoortBandPosition : IDisposable
{
    private readonly SpreadAverage[] _layers;
    private readonly SpreadAverage _dema, _tema, _center;
    private readonly PooledRingBuffer<SpreadNumber> _window;
    private readonly int _length;
    private readonly double _multiplier;
    internal VervoortBandPosition(int band, int cascade, int smooth, double multiplier)
    {
        _length = Math.Max(1, band); _multiplier = multiplier;
        _layers = Enumerable.Range(0, 10).Select(_ => new SpreadAverage(MovingAvgType.SimpleMovingAverage, cascade)).ToArray();
        _dema = new(MovingAvgType.DoubleExponentialMovingAverage, smooth);
        _tema = new(MovingAvgType.TripleExponentialMovingAverage, smooth);
        _center = new(MovingAvgType.WeightedMovingAverage, _length);
        _window = new PooledRingBuffer<SpreadNumber>(_length);
    }
    internal double Next(double close, bool final)
    {
        var stage = new SpreadNumber(close); var rainbow = new SpreadNumber(0);
        for (var i = 0; i < _layers.Length; i++)
        {
            stage = _layers[i].Next(stage, final);
            rainbow = SpreadNumber.Add(rainbow, stage.Times(Math.Max(1, 5-i)));
        }
        var value = _tema.Next(_dema.Next(rainbow.DividedBy(20), final), final);
        var center = _center.Next(value, final);
        var kept = Math.Min(_window.Count, _length-1);
        SpreadNumber At(int i) => i == kept ? value : _window[_window.Count-kept+i];
        double output = 0;
        if (kept+1 == _length && _multiplier != 0)
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
                output = 50+50*SpreadNumber.Subtract(value, center).Value/(_multiplier*sigma);
        }
        if (final) _window.TryAdd(value, out _);
        return output;
    }
    internal void Reset()
    { foreach (var layer in _layers) layer.Reset(); _dema.Reset(); _tema.Reset(); _center.Reset(); _window.Clear(); }
    public void Dispose()
    { foreach (var layer in _layers) layer.Dispose(); _dema.Dispose(); _tema.Dispose(); _center.Dispose(); _window.Dispose(); }
}
