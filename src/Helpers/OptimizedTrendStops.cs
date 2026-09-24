using System;

namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Two trailing stops with persistent direction for the Optimized Trend Tracker.</summary>
internal struct OptimizedTrendStops
{
    private bool _initialized;
    private bool _rising;
    private double _longStop, _shortStop;

    internal double Next(double average, double percent)
    {
        var distance = Math.Abs(average)*percent/100;
        var lower = average-distance;
        var upper = average+distance;
        if (!_initialized)
        {
            _initialized = true;
            _rising = true;
        }
        else
        {
            if (_rising && average < _longStop) _rising = false;
            else if (!_rising && average > _shortStop) _rising = true;
            if (average > _longStop) lower = Math.Max(lower, _longStop);
            if (average < _shortStop) upper = Math.Min(upper, _shortStop);
        }
        _longStop = lower;
        _shortStop = upper;
        var stop = _rising ? lower : upper;
        return average > stop ? stop*(200+percent)/200 : stop*(200-percent)/200;
    }
}
