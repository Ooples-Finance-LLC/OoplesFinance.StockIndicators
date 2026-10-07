using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Packed native stochastic stages, separate from the exact owned range/average formulas.
internal static class ClassicStochNativeReference
{
    internal static (int Start, T[][] Values) Slow<T>(
        T[] highs,
        T[] lows,
        T[] closes,
        int window,
        int k,
        int d,
        ClassicAverageMethod km,
        ClassicAverageMethod dm,
        bool first,
        int suppression,
        int requested,
        int end,
        bool signalAliasesInput = false
    )
        where T : IFloatingPointIeee754<T>
    {
        var kd = (int)ClassicAverageComparison.First(k, km, suppression);
        var dd = (int)ClassicAverageComparison.First(d, dm, suppression);
        var start = Math.Max(requested, window - 1 + kd + dd);
        if (start > end)
            return (
                0,
                [
                    [],
                    [],
                ]
            );
        var smooth = Fast(
            highs,
            lows,
            closes,
            window,
            k,
            km,
            first,
            suppression,
            start - dd,
            end
        ).Values[1];
        if (smooth.Length == 1)
            throw new InvalidOperationException("Native MA rejects its one-element input.");
        var signal = ClassicNativeReference.Packed(
            smooth,
            d,
            dm,
            first,
            suppression,
            0,
            smooth.Length - 1
        );
        return (start, Outputs(smooth, signal, dd, signalAliasesInput, d, dm, first, suppression));
    }

    internal static (int Start, T[][] Values) Fast<T>(
        T[] highs,
        T[] lows,
        T[] closes,
        int window,
        int signal,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int requested,
        int end,
        bool signalAliasesInput = false
    )
        where T : IFloatingPointIeee754<T>
    {
        var delay = (int)ClassicAverageComparison.First(signal, method, suppression);
        var start = Math.Max(requested, window - 1 + delay);
        if (start > end)
            return (
                0,
                [
                    [],
                    [],
                ]
            );
        var begin = start - delay;
        var raw = new T[end - begin + 1];
        var highIndex = -1;
        var lowIndex = -1;
        var high = T.Zero;
        var low = T.Zero;
        for (var i = begin; i <= end; i++)
        {
            Extreme(highs, i - window + 1, i, true, ref highIndex, ref high);
            Extreme(lows, i - window + 1, i, false, ref lowIndex, ref low);
            var width = (high - low) / T.CreateChecked(100);
            raw[i - begin] = T.IsZero(width) ? T.Zero : (closes[i] - low) / width;
        }
        if (raw.Length == 1)
            throw new InvalidOperationException("Native MA rejects its one-element input.");
        var average = ClassicNativeReference.Packed(
            raw,
            signal,
            method,
            first,
            suppression,
            0,
            raw.Length - 1
        );
        return (
            start,
            Outputs(raw, average, delay, signalAliasesInput, signal, method, first, suppression)
        );
    }

    private static T[][] Outputs<T>(
        T[] buffer,
        T[] signal,
        int delay,
        bool alias,
        int period,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
        where T : IFloatingPointIeee754<T>
    {
        // Native D/input aliasing reuses this temporary buffer. D overwrites its
        // prefix before K is copied, leaving an observable mixture of D and K.
        if (alias && period > 1 && method == ClassicAverageMethod.Dema)
        {
            // In-place DEMA first writes its longer EMA1 stream into this same
            // buffer; the tail survives the shorter final DEMA overwrite.
            var ema = ClassicNativeReference.Packed(
                buffer,
                period,
                ClassicAverageMethod.Ema,
                first,
                suppression,
                0,
                buffer.Length - 1
            );
            Array.Copy(ema, buffer, ema.Length);
        }
        if (alias)
            Array.Copy(signal, buffer, signal.Length);
        return [buffer.Skip(delay).Take(signal.Length).ToArray(), signal];
    }

    private static void Extreme<T>(
        T[] values,
        int from,
        int end,
        bool maximum,
        ref int selected,
        ref T value
    )
        where T : IFloatingPointIeee754<T>
    {
        if (selected >= from)
        {
            if (maximum ? values[end] >= value : values[end] <= value)
            {
                selected = end;
                value = values[end];
            }
            return;
        }
        selected = from;
        value = values[from];
        for (var i = from + 1; i <= end; i++)
        {
            // The native fallback's negated comparison deliberately replaces NaN candidates.
            if (maximum ? !(values[i] < value) : !(values[i] > value))
            {
                selected = i;
                value = values[i];
            }
        }
    }
}
