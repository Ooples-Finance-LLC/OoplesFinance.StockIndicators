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
        return ComputeExtended(open, high, low, close, prevOpen, prevClose, limitMove).Publish();
    }
    internal static RocBankValue ComputeExtended(double open, double high, double low, double close, double prevOpen, double prevClose, double limitMove)
    {
        var o = ExactVarianceWindow.Units(open); var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low);
        var c = ExactVarianceWindow.Units(close); var po = ExactVarianceWindow.Units(prevOpen); var pc = ExactVarianceWindow.Units(prevClose);
        var highMove = System.Numerics.BigInteger.Abs(h - pc); var lowMove = System.Numerics.BigInteger.Abs(l - pc);
        var range = h - l; var body = System.Numerics.BigInteger.Abs(pc - po);
        var divisor = highMove >= lowMove && highMove >= range ? 4 * highMove - 2 * lowMove + body
            : lowMove >= highMove && lowMove >= range ? 4 * lowMove - 2 * highMove + body : 4 * range + body;
        var scale = limitMove > 0 ? ExactVarianceWindow.Units(limitMove) : range;
        var denominator = divisor * scale;
        if (denominator.IsZero) return default;
        var numerator = 50 * (4 * (c - pc) + 2 * (c - o) + pc - po) * System.Numerics.BigInteger.Max(highMove, lowMove);
        var units = RocBankValue.RoundUnits((numerator * denominator.Sign) << 1074, System.Numerics.BigInteger.Abs(denominator));
        var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); return RocBankValue.Round(sum);
    }
}
