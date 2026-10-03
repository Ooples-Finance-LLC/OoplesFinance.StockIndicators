namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Current-window Fourier bins following Ehlers's high-pass and FIR cleanup.</summary>
internal sealed class DiscreteFourierCycle : IDisposable
{
    private readonly int _minimum, _maximum;
    private readonly double _pole;
    private readonly double[,] _cosines, _sines;
    private readonly double[] _powers;
    private readonly PooledRingBuffer<SpreadNumber> _hp, _cleaned;
    private readonly RollingWindowMax _scale;
    private double _previousInput;
    private int _count;

    internal DiscreteFourierCycle(int minimum, int maximum, int cutoff)
    {
        _minimum = Math.Max(3, minimum); _maximum = Math.Max(_minimum, maximum);
        var angle = Math.Max(.01, Math.Min(.99, 2*Math.PI/Math.Max(1, cutoff)));
        _pole = Math.Cos(angle)/(1+Math.Sin(angle));
        _hp = new PooledRingBuffer<SpreadNumber>(5); _cleaned = new PooledRingBuffer<SpreadNumber>(_maximum);
        _scale = new RollingWindowMax(_maximum);
        _powers = new double[_maximum+1];
        _cosines = new double[_maximum+1, _maximum]; _sines = new double[_maximum+1, _maximum];
        for (var period = _minimum; period <= _maximum; period++)
            for (var lag = 0; lag < _maximum; lag++)
            {
                _cosines[period, lag] = Math.Cos(2*Math.PI*lag/period);
                _sines[period, lag] = Math.Sin(2*Math.PI*lag/period);
            }
    }

    internal double Next(double input, bool final, out double highPass)
    {
        var previous = _hp.Count == 0 ? default : _hp[_hp.Count-1];
        var hp = _count < 6 ? new SpreadNumber(input) : SpreadNumber.Add(previous.Times(_pole),
            SpreadNumber.Subtract(new(input), new(_previousInput)).Times((1+_pole)/2));
        var cleaned = hp;
        if (_count >= 6)
        {
            for (var lag = 1; lag <= 5; lag++)
                cleaned = SpreadNumber.Add(cleaned, _hp[_hp.Count-lag].Times(lag is 2 or 3 ? 3 : lag is 1 or 4 ? 2 : 1));
            cleaned = cleaned.DividedBy(12);
        }
        var scale = final ? _scale.Add(Math.Abs(input), out _) : _scale.Preview(Math.Abs(input), out _);
        SpreadNumber At(int lag) => lag == 0 ? cleaned : lag <= _cleaned.Count ? _cleaned[_cleaned.Count-lag] : default;
        double peak = 0, mass = 0;
        for (var lag = 0; lag < _maximum; lag++) mass = Math.Max(mass, Math.Abs(At(lag).Value));
        double numerator = 0, denominator = 0;
        if (mass > 1.4210854715202004e-14*scale)
        {
            for (var period = _minimum; period <= _maximum; period++)
            {
                var real = new SpreadNumber(0); var imaginary = new SpreadNumber(0);
                for (var lag = 0; lag < _maximum; lag++)
                {
                    real = SpreadNumber.Add(real, At(lag).Times(_cosines[period, lag]));
                    imaginary = SpreadNumber.Add(imaginary, At(lag).Times(_sines[period, lag]));
                }
                // Normalize before squaring so tiny/large finite signals preserve spectral ratios.
                var re = real.Value/mass; var im = imaginary.Value/mass;
                _powers[period] = re*re+im*im;
                peak = Math.Max(peak, _powers[period]);
            }
            if (peak > 0)
                for (var period = _minimum; period <= _maximum; period++)
                {
                    var weight = Math.Max(0, 3-10*Math.Log10(100-99*Math.Min(1, _powers[period]/peak)));
                    numerator += period*weight; denominator += weight;
                }
        }
        if (final)
        {
            _hp.TryAdd(hp, out _); _cleaned.TryAdd(cleaned, out _);
            _previousInput = input; if (_count < 6) _count++;
        }
        highPass = hp.Value;
        return denominator == 0 ? 0 : numerator/denominator;
    }

    internal void Reset()
    { _hp.Clear(); _cleaned.Clear(); _scale.Reset(); _previousInput = 0; _count = 0; }
    public void Dispose() { _hp.Dispose(); _cleaned.Dispose(); _scale.Dispose(); }
}
