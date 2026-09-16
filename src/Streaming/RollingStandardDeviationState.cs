//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System;
using System.Collections.Generic;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// Streaming counterpart of <c>IndicatorMath.RollingStandardDeviation</c> - the population standard
/// deviation of the last <c>length</c> values about their own mean.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately not <see cref="StandardDeviationVolatilityState"/>. That state mirrors
/// <c>CalculateStandardDeviationVolatility</c>, which squares each bar's deviation from its own
/// contemporaneous moving average and averages those - the mean squared residual from the
/// moving-average line, not a rolling standard deviation. Bands defined against sigma need this one.
/// </para>
/// <para>
/// Warm-up matches the batch helper and <c>MovingAverageCore.SimpleMovingAverage</c>: zero until the
/// window is full. Squared deviations are summed over the window rather than derived from a running
/// sum of squares, for the reason given on the batch helper - the one-pass form cancels badly on price
/// data and drifts over a long series.
/// </para>
/// </remarks>
internal sealed class RollingStandardDeviationState
{
    private readonly double[] _window;
    private readonly int _length;
    private int _count;
    private int _next;

    public RollingStandardDeviationState(int length)
    {
        _length = Math.Max(1, length);
        _window = new double[_length];
    }

    public void Reset()
    {
        Array.Clear(_window, 0, _window.Length);
        _count = 0;
        _next = 0;
    }

    /// <summary>
    /// Adds <paramref name="value"/> to the window and returns the standard deviation.
    /// </summary>
    /// <param name="value">The next value of the series.</param>
    /// <param name="isFinal">
    /// When false the value is evaluated without being retained, so an unconfirmed bar can be previewed
    /// and then replaced.
    /// </param>
    public double Next(double value, bool isFinal)
    {
        // Evaluate against a window that includes the incoming value, whether or not it is retained.
        var filled = Math.Min(_count + 1, _length);
        if (filled < _length)
        {
            if (isFinal)
            {
                Store(value);
            }

            return 0;
        }

        double sum = value;
        // The oldest entry drops out when the window is already full.
        var take = _count >= _length ? _length - 1 : _count;
        for (var k = 1; k <= take; k++)
        {
            sum += _window[((_next - k) % _length + _length) % _length];
        }

        var mean = sum / _length;
        var deviation = value - mean;
        double sumOfSquaredDeviations = deviation * deviation;
        for (var k = 1; k <= take; k++)
        {
            var previous = _window[((_next - k) % _length + _length) % _length];
            var d = previous - mean;
            sumOfSquaredDeviations += d * d;
        }

        if (isFinal)
        {
            Store(value);
        }

        var variance = sumOfSquaredDeviations / _length;

        return variance > 0 ? Math.Sqrt(variance) : 0;
    }

    private void Store(double value)
    {
        _window[_next] = value;
        _next = (_next + 1) % _length;
        if (_count < _length)
        {
            _count++;
        }
    }
}
