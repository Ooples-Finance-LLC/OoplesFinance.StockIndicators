using System;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The population standard deviation of the trailing window: 0 until the window is full.
/// </summary>
/// <remarks>
/// <para>
/// The streaming twin of the batch <c>GetStandardDeviationList</c>, and computed the same way - a mean over
/// the window, then the mean squared distance from it, oldest value first - so the two engines agree to the
/// last bit rather than to a tolerance.
/// </para>
/// <para>
/// Two passes over the window on purpose. Running sums of values and squares are cheaper, but they drift
/// over a long stream and cancel catastrophically when the values are large next to their spread: a bar
/// index a year into a minute stream squares to around 10^11 while its window variance is about 16.
/// </para>
/// </remarks>
internal sealed class RollingStandardDeviation : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;

    public RollingStandardDeviation(int length)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
    }

    /// <summary>The standard deviation including <paramref name="value"/>, committed only when final.</summary>
    public double Next(double value, bool isFinal)
    {
        StreamingInputValidation.Finite(value, nameof(value));
        if (isFinal)
        {
            _window.TryAdd(value, out _);
            return _window.Count < _length ? 0 : Compute(0, value, includeValue: false);
        }

        // A preview replaces the oldest value only if the window is already full.
        var kept = Math.Min(_window.Count, _length - 1);
        return kept + 1 < _length ? 0 : Compute(_window.Count - kept, value, includeValue: true);
    }

    public void Reset() => _window.Clear();

    public void Dispose() => _window.Dispose();

    private double Compute(int first, double value, bool includeValue)
    {
        var anchor = first < _window.Count ? _window[first] : value;
        double sum = 0;
        for (var i = first; i < _window.Count; i++)
        {
            sum += _window[i] - anchor;
        }

        if (includeValue)
        {
            sum += value - anchor;
        }

        var meanOffset = sum / _length;
        double variance = 0;
        var lostSquare = false;
        for (var i = first; i < _window.Count; i++)
        {
            var diff = (_window[i] - anchor) - meanOffset;
            var square = diff * diff;
            lostSquare |= diff != 0 && square < 2.2250738585072014E-308;
            variance += square;
        }

        if (includeValue)
        {
            var diff = (value - anchor) - meanOffset;
            var square = diff * diff;
            lostSquare |= diff != 0 && square < 2.2250738585072014E-308;
            variance += square;
        }

        variance /= _length;
        if (lostSquare || double.IsNaN(variance) || double.IsInfinity(variance)
            || variance > 0 && variance < 2.2250738585072014E-308)
        {
            var exact = new ExactPopulationDeviation();
            for (var i = first; i < _window.Count; i++) exact.Add(_window[i]);
            if (includeValue) exact.Add(value);
            return exact.Value();
        }
        return Math.Sqrt(variance);
    }
}
