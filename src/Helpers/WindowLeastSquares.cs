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
/// A least-squares line through a window of values, with x counted from the window's first bar.
/// </summary>
/// <remarks>
/// <para>
/// Counting x from the window's first bar rather than the series' keeps the fit well conditioned. With the bar
/// index as x, <c>n*Sum(x^2)</c> and <c>Sum(x)^2</c> agree in their leading digits a few hundred bars in, and the
/// slope is whatever survives their cancellation. Counted from the window, the sums of x and x^2 depend only on
/// the window size and are exact.
/// </para>
/// <para>
/// The sums of y and xy are rebuilt from the window on every bar, oldest value first, rather than kept running.
/// The batch and streaming engines both feed the window that way, so they agree to the last bit, and neither
/// drifts over a long series.
/// </para>
/// </remarks>
internal struct WindowLeastSquares
{
    private double _sumY;
    private double _sumXY;
    private int _count;

    /// <summary>The number of values added.</summary>
    public int Count => _count;

    /// <summary>Adds the window's next value, oldest first.</summary>
    public void Add(double y)
    {
        _sumY += y;
        _sumXY += _count * y;
        _count++;
    }

    /// <summary>The slope, and the intercept at the window's first bar.</summary>
    /// <param name="divisor">The number of points the fit divides by: <see cref="Count"/> for a textbook fit.</param>
    public (double Slope, double Intercept) Solve(int divisor)
    {
        double n = divisor;
        double count = _count;
        var sumX = count * (count - 1) / 2;
        var sumX2 = (count - 1) * count * ((2 * count) - 1) / 6;
        var bottom = (n * sumX2) - (sumX * sumX);
        var slope = bottom != 0 ? ((n * _sumXY) - (sumX * _sumY)) / bottom : 0;
        var intercept = n != 0 ? (_sumY - (slope * sumX)) / n : 0;
        return (slope, intercept);
    }
}
