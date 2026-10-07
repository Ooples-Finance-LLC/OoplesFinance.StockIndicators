using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class StrengthReference
{
    // Validation-only exact fractions: choose the quantum from numerator and
    // denominator bit lengths, then perform nearest/even integer division.
    internal static ReferenceFraction Quantize(ReferenceFraction value)
    {
        var (n, d) = value.Components;
        if (n.IsZero)
            return new(0);
        var exponent = (int)(n.GetBitLength() - d.GetBitLength());
        if (exponent >= 0 ? n < (d << exponent) : (n << -exponent) < d)
            exponent--;
        var shift = exponent - 52;
        if (shift < 0)
            n <<= -shift;
        else
            d <<= shift;
        var mantissa = BigInteger.DivRem(n, d, out var remainder);
        if (2 * remainder > d || 2 * remainder == d && !mantissa.IsEven)
            mantissa++;
        return shift >= 0
            ? new ReferenceFraction(mantissa << shift)
            : new ReferenceFraction(mantissa) / new ReferenceFraction(BigInteger.One << -shift);
    }

    internal static double[][] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        WilderStrengthConvention convention,
        int unstable
    )
    {
        var result = new[] { new double[bars.Count], new double[bars.Count] };
        var gain = new ReferenceFraction(0);
        var loss = new ReferenceFraction(0);
        for (var i = 1; i < bars.Count; i++)
        {
            var difference =
                ReferenceFraction.FromDouble(bars[i].Close)
                - ReferenceFraction.FromDouble(bars[i - 1].Close);
            var up = difference.Sign > 0 ? difference : new ReferenceFraction(0);
            var down = difference.Sign < 0 ? difference.Abs() : new ReferenceFraction(0);
            if (i <= period)
            {
                gain += up;
                loss += down;
                if (i < period)
                    continue;
                gain = Quantize(gain / new ReferenceFraction(period));
                loss = Quantize(loss / new ReferenceFraction(period));
            }
            else
            {
                gain = Quantize(
                    (gain * new ReferenceFraction(period - 1) + up) / new ReferenceFraction(period)
                );
                loss = Quantize(
                    (loss * new ReferenceFraction(period - 1) + down)
                        / new ReferenceFraction(period)
                );
            }
            if (i < (long)period + unstable)
                continue;
            var total = gain + loss;
            result[0][i] =
                total.Sign == 0
                    ? (convention == WilderStrengthConvention.RsiHundredFlat ? 100 : 0)
                    : (
                        new ReferenceFraction(100)
                        * (
                            convention == WilderStrengthConvention.ChandeZeroFlat
                                ? gain - loss
                                : gain
                        )
                        / total
                    ).ToDouble();
            result[1][i] = 1;
        }
        return result;
    }
}
