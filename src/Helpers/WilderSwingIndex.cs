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
/// Wilder's swing index of a bar against the bar before it (New Concepts in Technical Trading Systems, 1978).
/// </summary>
/// <remarks>
/// <c>SI = 50 * ((C - Cy) + 0.5 * (C - O) + 0.25 * (Cy - Oy)) / R * K / T</c>, with
/// <c>K = max(|H - Cy|, |L - Cy|)</c>, and R taken from whichever of <c>|H - Cy|</c>, <c>|L - Cy|</c> and
/// <c>H - L</c> is largest. T is the market's limit move; where it has none, the bar's range stands in.
/// The batch and streaming engines both compute the index here, so they agree to the last bit.
/// </remarks>
internal static class WilderSwingIndex
{
    public static double Compute(double open, double high, double low, double close, double prevOpen, double prevClose,
        double limitMove)
    {
        var highMove = Math.Abs(high - prevClose);
        var lowMove = Math.Abs(low - prevClose);
        var range = high - low;
        var prevBody = Math.Abs(prevClose - prevOpen);

        var r = highMove >= lowMove && highMove >= range ? highMove - (0.5 * lowMove) + (0.25 * prevBody)
            : lowMove >= highMove && lowMove >= range ? lowMove - (0.5 * highMove) + (0.25 * prevBody)
            : range + (0.25 * prevBody);
        var k = Math.Max(highMove, lowMove);
        var t = limitMove > 0 ? limitMove : range;
        var n = (close - prevClose) + (0.5 * (close - open)) + (0.25 * (prevClose - prevOpen));

        return r != 0 && t != 0 ? 50 * (n / r) * (k / t) : 0;
    }
}
