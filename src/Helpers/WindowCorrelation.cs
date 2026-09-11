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
/// Pearson's correlation of a window of paired values, from their distances to their means.
/// </summary>
/// <remarks>
/// The raw-sum form, <c>n*Sum(xy) - Sum(x)*Sum(y)</c>, subtracts two numbers that agree in almost every digit
/// when the values sit far from zero: with x the bar index, a two-bar window around bar 200 subtracts two
/// numbers near 80,000 to recover a change in price. Where the true answer is zero, because one side is
/// constant, it returned a rounding residue of either sign, and an indicator that sums the correlation's sign
/// followed it. Centring each value on its window's mean first gives exactly zero there, and both engines use
/// this one routine over the same window, oldest value first, so they agree to the last bit.
/// </remarks>
internal static class WindowCorrelation
{
    /// <summary>The correlation of <paramref name="x"/> and <paramref name="y"/>; 0 below two points or with no spread.</summary>
    public static double Pearson(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        var n = x.Length;
        if (n <= 1)
        {
            return 0;
        }

        double sumX = 0, sumY = 0;
        for (var i = 0; i < n; i++)
        {
            sumX += x[i];
            sumY += y[i];
        }

        var meanX = sumX / n;
        var meanY = sumY / n;
        double sumXY = 0, sumX2 = 0, sumY2 = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            sumXY += dx * dy;
            sumX2 += dx * dx;
            sumY2 += dy * dy;
        }

        var denom = Math.Sqrt(sumX2 * sumY2);
        return denom != 0 ? sumXY / denom : 0;
    }
}
