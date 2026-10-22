#if !NETFRAMEWORK
using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Helpers;

// The classic recurrences keep finite scalar state. Any uncertified update
// retries through the existing exact/wide implementation before publication.
internal static class CertifiedParabolicClassic
{
    private static double U(double value) => value == 0 ? 0 : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryAdvance(double stop, double extreme, double factor, out double value)
    {
        double difference;
        // Sterbenz's lemma proves subtraction exact for nearby nonnegative
        // operands. Doubling is exact; overflow to infinity still gives the
        // correct comparison against every finite operand. Other cases use
        // the general error-free subtraction certificate.
        if (stop >= 0 && extreme >= 0 && stop <= 2 * extreme && extreme <= 2 * stop)
            difference = extreme - stop;
        else if (!Binary64ArithmeticCertificate.TryDifference(extreme, stop, out difference))
        { value = 0; return false; }
        value = Math.FusedMultiplyAdd(factor, difference, stop);
        if (!double.IsFinite(value)) return false;
        value = U(value);
        return true;
    }

    internal static bool TryCalculate(IReadOnlyList<Bar> bars, bool signed,
        double start, double offset, double initialLong, double stepLong, double maximumLong,
        double initialShort, double stepShort, double maximumShort, out double?[] result)
    {
        result = new double?[bars.Count];
        if (bars.Count < 2)
            return true;
        var array = bars as Bar[];
        initialShort = U(Math.Min(initialShort, maximumShort));
        initialLong = U(Math.Min(initialLong, maximumLong));
        stepShort = U(Math.Min(stepShort, maximumShort));
        stepLong = U(Math.Min(stepLong, maximumLong));
        maximumShort = U(maximumShort);
        maximumLong = U(maximumLong);
        var offsetUnits = U(offset);
        var down = ParabolicStopValue.Of(bars[0].Low) - ParabolicStopValue.Of(bars[1].Low);
        var upMove = ParabolicStopValue.Of(bars[1].High) - ParabolicStopValue.Of(bars[0].High);
        var rising = start == 0 ? !(down.Sign > 0 && down > upMove) : start > 0;
        var extreme = U(rising ? bars[1].High : bars[1].Low);
        var stop = U(
            start == 0
                ? rising
                    ? bars[0].Low
                    : bars[0].High
                : Math.Abs(start)
        );
        var factor = rising ? initialLong : initialShort;
        var priorHigh = U(bars[1].High);
        var priorLow = U(bars[1].Low);
        for (var i = 1; i < (array is null ? bars.Count : array.Length); i++)
        {
            var current = array is null ? bars[i] : array[i];
            var high = U(current.High);
            var low = U(current.Low);
            var reversal = rising ? low <= stop : high >= stop;
            if (reversal)
            {
                rising = !rising;
                stop = rising
                    ? Math.Min(extreme, Math.Min(priorLow, low))
                    : Math.Max(extreme, Math.Max(priorHigh, high));
                stop = Math.FusedMultiplyAdd(stop, rising ? -offsetUnits : offsetUnits, stop);
                if (!double.IsFinite(stop)) return false;
                factor = rising ? initialLong : initialShort;
                extreme = rising ? high : low;
            }
            result[i] = U(signed && !rising ? -stop : stop);
            if (!reversal && (rising ? high > extreme : low < extreme))
            {
                extreme = rising ? high : low;
                factor = Math.Min(factor + (rising ? stepLong : stepShort), rising ? maximumLong : maximumShort);
                if (!double.IsFinite(factor)) return false;
            }
            if (!TryAdvance(stop, extreme, factor, out stop)) return false;
            stop = rising
                ? Math.Min(stop, Math.Min(priorLow, low))
                : Math.Max(stop, Math.Max(priorHigh, high));
            priorHigh = high;
            priorLow = low;
        }
        return true;
    }

}
#endif
