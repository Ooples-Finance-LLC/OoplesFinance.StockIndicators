//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright (c) Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>
/// The least-squares line through a trailing window, with x counted from the window's first value.
/// </summary>
/// <remarks>
/// <para>
/// Counting x from the window rather than the series keeps the fit well conditioned. With the bar index as x,
/// <c>n*Sum(x^2)</c> and <c>Sum(x)^2</c> agree in their leading digits a few hundred bars in, and the slope is
/// whatever survives their cancellation. Counted from the window, the sums of x and x^2 depend only on how many
/// values it holds and are exact.
/// </para>
/// <para>
/// Each value costs O(1). Sliding the window drops its oldest value, whose x is 0, and moves every other value
/// one step left, so <c>Sum(xy)</c> loses the <c>Sum(y)</c> of the values that stay, before the new value joins
/// at x = length - 1. Kept running forever, those sums would carry rounding from values long out of the window,
/// so they are rebuilt exactly from the window every length values, after that value's fit is taken: a preview
/// still equals the final value it previews.
/// </para>
/// <para>
/// Until the window fills, the line is fitted through the values there are. The batch and streaming engines both
/// feed this one class, so they agree to the last bit.
/// </para>
/// </remarks>
internal sealed class RollingLeastSquares : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private double _sumY;
    private double _sumXY;
    private int _sinceRebuild;

    public RollingLeastSquares(int length)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
    }

    /// <summary>The fit through the window with <paramref name="value"/> added, committed only when final.</summary>
    public LeastSquaresFit Next(double value, bool isFinal)
    {
        double sumY;
        double sumXY;
        int count;
        if (_window.Count == _length)
        {
            var remaining = _sumY - _window[0];
            sumXY = (_sumXY - remaining) + ((_length - 1) * value);
            sumY = remaining + value;
            count = _length;
        }
        else
        {
            sumXY = _sumXY + (_window.Count * value);
            sumY = _sumY + value;
            count = _window.Count + 1;
        }

        var fit = LeastSquaresFit.Solve(sumY, sumXY, count);
        if (isFinal)
        {
            _window.TryAdd(value, out _);
            _sumY = sumY;
            _sumXY = sumXY;
            if (++_sinceRebuild == _length)
            {
                _sinceRebuild = 0;
                Rebuild();
            }
        }

        return fit;
    }

    public void Reset()
    {
        _window.Clear();
        _sumY = 0;
        _sumXY = 0;
        _sinceRebuild = 0;
    }

    public void Dispose() => _window.Dispose();

    private void Rebuild()
    {
        double sumY = 0;
        double sumXY = 0;
        for (var i = 0; i < _window.Count; i++)
        {
            var y = _window[i];
            sumY += y;
            sumXY += i * y;
        }

        _sumY = sumY;
        _sumXY = sumXY;
    }
}

/// <summary>A least-squares line through a window, x counted from the window's first value.</summary>
internal readonly struct LeastSquaresFit
{
    private LeastSquaresFit(double slope, double intercept, int count)
    {
        Slope = slope;
        Intercept = intercept;
        Count = count;
    }

    public double Slope { get; }

    /// <summary>The line at the window's first value.</summary>
    public double Intercept { get; }

    /// <summary>The number of values the line was fitted through.</summary>
    public int Count { get; }

    /// <summary>The line at the window's last value.</summary>
    public double Last => Intercept + (Slope * (Count - 1));

    /// <summary>The line one value beyond the window.</summary>
    public double Next => Intercept + (Slope * Count);

    public static LeastSquaresFit Solve(double sumY, double sumXY, int count)
    {
        double n = count;
        var sumX = n * (n - 1) / 2;
        var sumX2 = (n - 1) * n * ((2 * n) - 1) / 6;
        var bottom = (n * sumX2) - (sumX * sumX);
        var slope = bottom != 0 ? ((n * sumXY) - (sumX * sumY)) / bottom : 0;
        var intercept = n != 0 ? (sumY - (slope * sumX)) / n : 0;
        return new LeastSquaresFit(slope, intercept, count);
    }
}
