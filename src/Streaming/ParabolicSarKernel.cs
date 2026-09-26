namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Wilder stop, seeded long at the first low; only completed bars change the state.</summary>
internal sealed class ParabolicSarKernel
{
    private readonly double _start, _increment, _maximum;
    private bool _initialized, _long = true;
    private int _count;
    private double _sar, _extreme, _acceleration, _high1, _high2, _low1, _low2;

    internal ParabolicSarKernel(double start, double increment, double maximum)
    {
        if (double.IsNaN(start) || double.IsInfinity(start) || start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (double.IsNaN(increment) || double.IsInfinity(increment) || increment < 0) throw new ArgumentOutOfRangeException(nameof(increment));
        if (double.IsNaN(maximum) || double.IsInfinity(maximum) || maximum < start) throw new ArgumentOutOfRangeException(nameof(maximum));
        _start = start; _increment = increment; _maximum = maximum;
    }

    internal double Next(double high, double low, bool final)
    {
        var rising = _long;
        var extreme = _initialized ? _extreme : high;
        var acceleration = _initialized ? _acceleration : _start;
        var stop = _initialized ? _sar + acceleration * (extreme - _sar) : low;
        if (_initialized)
        {
            stop = rising ? Math.Min(stop, _count > 1 ? Math.Min(_low1, _low2) : _low1)
                : Math.Max(stop, _count > 1 ? Math.Max(_high1, _high2) : _high1);
            if (rising ? low < stop : high > stop)
            {
                stop = rising ? Math.Max(extreme, high) : Math.Min(extreme, low);
                rising = !rising;
                extreme = rising ? high : low;
                acceleration = _start;
            }
            else if (rising ? high > extreme : low < extreme)
            {
                extreme = rising ? high : low;
                acceleration = Math.Min(_maximum, acceleration + _increment);
            }
        }
        if (final)
        {
            _initialized = true; _count++; _long = rising;
            _sar = stop; _extreme = extreme; _acceleration = acceleration;
            _high2 = _high1; _high1 = high; _low2 = _low1; _low1 = low;
        }
        return stop;
    }

    internal void Reset()
    {
        _initialized = false; _long = true; _count = 0;
        _sar = _extreme = _acceleration = _high1 = _high2 = _low1 = _low2 = 0;
    }
}
