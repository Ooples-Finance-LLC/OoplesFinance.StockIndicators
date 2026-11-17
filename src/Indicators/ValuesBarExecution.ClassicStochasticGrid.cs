#if NET10_0
using System.Buffers;
using AiDotNet.Tensors.Engines.Simd;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool CanUseClassicStochasticGrid(ClassicStochastic owner) =>
        owner.Period <= 8 && owner.KPeriod <= 64 && owner.DPeriod <= 64
        && owner.KMethod == ClassicAverageMethod.Sma && owner.DMethod == ClassicAverageMethod.Sma
        && !owner.FirstPriceSeed && owner.Suppression == 0
        && (long)owner.Period + owner.KPeriod + owner.DPeriod - 3 <= 128;

    // All publication is deferred until the entire captured block has passed.
    // A rejected grid leaves the original state replay responsible for this block.
    private static unsafe bool TryFillClassicStochasticGrid(Bar[] captured, double[][] output,
        ClassicStochastic owner, int start, int end, CancellationToken cancellation)
    {
        int halo = owner.Period + owner.KPeriod + owner.DPeriod - 3;
        int first = Math.Max(0, start - halo), count = end - first;
        int rawCount = count - owner.Period + 1;
        int kCount = rawCount - owner.KPeriod + 1, dCount = kCount - owner.DPeriod + 1;
        if (dCount <= 0 || count > int.MaxValue / 6) return false;
        double[] scratch = ArrayPool<double>.Shared.Rent(checked(count * 6));
        Int128[] words = ArrayPool<Int128>.Shared.Rent(count);
        try
        {
            Span<double> highs = scratch.AsSpan(0, count), lows = scratch.AsSpan(count, count);
            Span<double> maxima = scratch.AsSpan(2 * count, rawCount), minima = scratch.AsSpan(3 * count, rawCount);
            Span<double> raw = scratch.AsSpan(4 * count, rawCount), ks = scratch.AsSpan(5 * count, kCount);
            int priceGrid = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return false;
                ref readonly Bar bar = ref captured[first + i];
                highs[i] = bar.High; lows[i] = bar.Low;
                IncludeGrid(bar.High, ref priceGrid); IncludeGrid(bar.Low, ref priceGrid);
                IncludeGrid(bar.Close, ref priceGrid);
            }
            if (priceGrid == int.MaxValue) priceGrid = 0;
            highs.Slice(owner.Period - 1, rawCount).CopyTo(maxima);
            lows.Slice(owner.Period - 1, rawCount).CopyTo(minima);
            fixed (double* p = scratch)
            {
                for (int lag = 1; lag < owner.Period; lag++)
                {
                    int offset = owner.Period - 1 - lag;
                    SimdKernels.VectorMaxUnsafe(p + 2 * count, p + offset, p + 2 * count, rawCount);
                    SimdKernels.VectorMinUnsafe(p + 3 * count, p + count + offset, p + 3 * count, rawCount);
                }
            }
            for (int i = 0; i < rawCount; i++)
            {
                if (cancellation.IsCancellationRequested) return false;
                if (!TryGridWord(maxima[i], priceGrid, out long high)
                    || !TryGridWord(minima[i], priceGrid, out long low)
                    || !TryGridWord(captured[first + owner.Period - 1 + i].Close, priceGrid, out long close)) return false;
                Int128 denominator = (Int128)high - low;
                if (denominator == 0) { raw[i] = 0; continue; }
                Int128 numerator = ((Int128)close - low) * 100;
                if (denominator < 0 || denominator >= (1L << 57)
                    || numerator <= -100L * (1L << 56) || numerator >= 100L * (1L << 56)
                    || !StrengthRoundedRatio.TryDivide((long)numerator, (long)denominator, out double value)) return false;
                raw[i] = value;
            }
            if (!TryClassicStochasticGridMeans(raw, ks, owner.KPeriod, words, cancellation)) return false;
            // The range scratch is no longer live; reuse it for rounded D values.
            Span<double> ds = maxima.Slice(0, dCount);
            if (!TryClassicStochasticGridMeans(ks, ds, owner.DPeriod, words, cancellation)) return false;
            if (cancellation.IsCancellationRequested) return false;
            int ready = first + halo;
            for (int i = start; i < end; i++)
            {
                if (cancellation.IsCancellationRequested) return false;
                if (i < ready)
                {
                    for (int slot = 0; slot < 4; slot++) output[slot][i] = 0;
                    continue;
                }
                int dIndex = i - ready;
                output[0][i] = ks[dIndex + owner.DPeriod - 1];
                output[1][i] = ds[dIndex];
                output[2][i] = output[3][i] = 1;
            }
            return true;
        }
        finally
        {
            ArrayPool<Int128>.Shared.Return(words);
            ArrayPool<double>.Shared.Return(scratch);
        }
    }

    private static void IncludeGrid(double value, ref int grid)
    {
        var parts = ExactMeanAccumulator.DecomposeFinite(value);
        if (parts.Integer != 0) grid = Math.Min(grid, parts.Exponent);
    }

    private static bool TryGridWord(double value, int grid, out long word)
    {
        var parts = ExactMeanAccumulator.DecomposeFinite(value);
        if (parts.Integer == 0) { word = 0; return true; }
        int shift = parts.Exponent - grid;
        if (shift is < 0 or >= 64) { word = 0; return false; }
        word = unchecked(parts.Integer << shift);
        return (word >> shift) == parts.Integer;
    }

    private static bool TryClassicStochasticGridMeans(ReadOnlySpan<double> input, Span<double> output,
        int period, Int128[] words, CancellationToken cancellation)
    {
        if (period == 1)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (cancellation.IsCancellationRequested) return false;
                output[i] = input[i];
            }
            return true;
        }
        int grid = int.MaxValue;
        foreach (double value in input)
        {
            if (cancellation.IsCancellationRequested) return false;
            IncludeGrid(value, ref grid);
        }
        if (grid == int.MaxValue) { output.Clear(); return true; }
        for (int i = 0; i < input.Length; i++)
        {
            if (cancellation.IsCancellationRequested) return false;
            var parts = ExactMeanAccumulator.DecomposeFinite(input[i]);
            int shift = parts.Exponent - grid;
            if (parts.Integer == 0) { words[i] = 0; continue; }
            if (shift is < 0 or >= 128) return false;
            Int128 word = (Int128)parts.Integer << shift;
            if ((word >> shift) != parts.Integer
                || word > (Int128.MaxValue >> 7) || word < -(Int128.MaxValue >> 7)) return false;
            words[i] = word;
        }
        var divisor = new ExactMeanAccumulator.WordMeanDivisor(period);
        Int128 sum = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (cancellation.IsCancellationRequested) return false;
            // Each coefficient has magnitude < 2^120 and at most 64 are retained.
            // The add-before-remove transient is bounded by 65*2^120 < 2^127.
            sum += words[i];
            if (i >= period) sum -= words[i - period];
            if (i < period - 1) continue;
            double value;
            if (sum >= long.MinValue && sum <= long.MaxValue)
                value = ExactMeanAccumulator.MeanScaledWord((long)sum, grid, in divisor);
            else
            {
                bool negative = sum < 0;
                UInt128 magnitude = (UInt128)(negative ? -sum : sum);
                if (!CertifiedMeanRelativeError.TryDivideCount(magnitude, period, grid, out value)) return false;
                if (negative) value = -value;
            }
            long bits = BitConverter.DoubleToInt64Bits(value);
            int exponent = (int)((bits >> 52) & 2047);
            if (value != 0 && (exponent is 0 or 2047)) return false;
            output[i - period + 1] = value;
        }
        return true;
    }
}
#endif
