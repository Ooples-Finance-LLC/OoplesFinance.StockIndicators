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
/// Running sums held as an unevaluated pair, high + low, so the digits a double cannot hold are kept.
/// </summary>
/// <remarks>
/// A prefix sum of a long series grows without bound, and a window sum taken as the difference of two prefixes
/// inherits the rounding of every value before the window. After 100,000 bars near 100,000 the prefix is near
/// 10^10, whose last bit is worth about 2e-6: a 20-bar window of prices near 10 came back off by 1e-8 of itself.
/// Kept as high + low with error-free additions, the prefix is exact to about 1e-32 of its size and the window
/// sum to the rounding of the result.
/// </remarks>
internal static class CompensatedSum
{
    /// <summary>The prefix pair after adding <paramref name="value"/> to <paramref name="high"/> + <paramref name="low"/>.</summary>
    public static (double High, double Low) Add(double high, double low, double value)
    {
        // Knuth's TwoSum: sum + error == high + value exactly.
        var sum = high + value;
        var virtualValue = sum - high;
        var error = (high - (sum - virtualValue)) + (value - virtualValue);
        low += error;

        // Dekker's FastTwoSum renormalises, so the high part carries all it can.
        var renormalised = sum + low;
        return (renormalised, low - (renormalised - sum));
    }

    /// <summary>The sum between two prefixes: the later minus the earlier.</summary>
    public static double Difference(double endHigh, double endLow, double startHigh, double startLow) =>
        (endHigh - startHigh) + (endLow - startLow);
}
