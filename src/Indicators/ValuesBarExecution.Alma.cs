#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeAlmaColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, ArnaudLegouxWindow indicator, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        double[] seedCloses = seed.IsEmpty ? [] : System.Buffers.ArrayPool<double>.Shared.Rent(seed.Length);
        int count = seed.Length + end - start;
        AlmaPriceGrid grid = default;
        try
        {
            // The presence column temporarily retains captured closes. Each
            // close is consumed before that row receives its final presence.
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar))
                { region.InvalidInput = true; region.Last = bar; return region; }
                if (i < seed.Length) seedCloses[i] = bar.Close;
                else
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                    output[1][row] = bar.Close;
                }
                grid.Include(bar.Close);
            }
            if (!grid.TryMaximum(out Int128 maximum))
            {
                ReplayCapturedAlma(output, start, count, seedCloses.AsSpan(0, seed.Length), indicator, cancellation, ref region);
                return region;
            }
            // Reuse the original exact Gaussian generator without changing its
            // state/update path. Full-window coefficients remain worker-local.
            if (cancellation.IsCancellationRequested) return region;
            var factors = ArnaudLegouxWindow.State.PrepareBatchWeights(indicator.Period, indicator.Offset, indicator.Sigma);
            if (!TryAlmaWeightGrid(factors, out int weightScale))
            {
                ReplayCapturedAlma(output, start, count, seedCloses.AsSpan(0, seed.Length), indicator, cancellation, ref region);
                return region;
            }
            var weights = System.Buffers.ArrayPool<Int128>.Shared.Rent(indicator.Period);
            try
            {
                Int128 mass = 0;
                bool fits = true;
                for (int i = 0; i < factors.Length; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    var factor = factors[i];
                    Int128 value = factor.Mantissa == 0 ? 0 : (Int128)factor.Mantissa << (factor.Scale - weightScale);
                    weights[i] = value;
                    if (value > Int128.MaxValue - mass) { fits = false; break; }
                    mass += value;
                }
                // Positive Gaussian weights make this a bound on every product
                // and every absolute partial sum, including signed cancellation.
                if (!fits || mass == 0 || maximum > Int128.MaxValue / mass)
                {
                    ReplayCapturedAlma(output, start, count, seedCloses.AsSpan(0, seed.Length), indicator, cancellation, ref region);
                    return region;
                }
                var history = System.Buffers.ArrayPool<Int128>.Shared.Rent(indicator.Period);
                try
                {
                    // Only the first region publishes expanding startup. Seed
                    // rows in later regions need no unpublished startup weights.
                    var startup = !indicator.FullWindowOnly && seed.IsEmpty && indicator.Period > 1
                        ? new ArnaudLegouxWindow.State(indicator.Period, indicator.Offset, indicator.Sigma, false) : null;
                    Span<double> values = stackalloc double[2];
                    int cursor = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (cancellation.IsCancellationRequested) return region;
                        int row = start + i - seed.Length;
                        double close = i < seed.Length ? seedCloses[i] : output[1][row];
                        history[cursor] = grid.Coefficient(close);
                        if (++cursor == indicator.Period) cursor = 0;
                        if (i < seed.Length) continue;
                        if (i < indicator.Period - 1)
                        {
                            if (indicator.FullWindowOnly) StoreAlmaPublication(output, row, 0, 0, ref region);
                            else
                            {
                                var bar = new Bar(default, 0, 0, 0, close, 0);
                                startup!.Update(in bar, values);
                                StoreAlmaPublication(output, row, values[0], values[1], ref region);
                            }
                            continue;
                        }
                        Int128 numerator = 0;
                        if (maximum != 0)
                        {
                            int slot = cursor;
                            for (int k = 0; k < indicator.Period; k++)
                            {
                                numerator += history[slot] * weights[k];
                                if (++slot == indicator.Period) slot = 0;
                            }
                        }
                        double value = PublishAlmaRatio(numerator, mass, grid.Scale - 1074);
                        StoreAlmaPublication(output, row, value, 1, ref region);
                    }
                }
                finally { System.Buffers.ArrayPool<Int128>.Shared.Return(history); }
            }
            finally { System.Buffers.ArrayPool<Int128>.Shared.Return(weights); }
            return region;
        }
        finally { if (!seed.IsEmpty) System.Buffers.ArrayPool<double>.Shared.Return(seedCloses); }
    }

    private static void ReplayCapturedAlma(double[][] output, int start, int count, ReadOnlySpan<double> seed,
        ArnaudLegouxWindow indicator, CancellationToken cancellation, ref PointwiseRegion region)
    {
        var state = new ArnaudLegouxWindow.State(indicator.Period, indicator.Offset, indicator.Sigma, indicator.FullWindowOnly);
        Span<double> values = stackalloc double[2];
        for (int i = 0; i < count; i++)
        {
            if (cancellation.IsCancellationRequested) return;
            int row = start + i - seed.Length;
            double close = i < seed.Length ? seed[i] : output[1][row];
            var bar = new Bar(default, 0, 0, 0, close, 0);
            state.Update(in bar, values);
            if (i >= seed.Length) StoreAlmaPublication(output, row, values[0], values[1], ref region);
        }
    }

    private static void StoreAlmaPublication(double[][] output, int row, double value, double present, ref PointwiseRegion region)
    {
        output[0][row] = value; output[1][row] = present;
        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
        { region.InvalidOutputIndex = row; region.InvalidOutputValue = value; }
    }

    private static double PublishAlmaRatio(Int128 numerator, Int128 mass, int exponent)
    {
        if (numerator == 0) return 0;
        UInt128 magnitude = (UInt128)(numerator < 0 ? -numerator : numerator);
        if (CertifiedCompactRatio.TryDivide(magnitude, (UInt128)mass, exponent, out double result))
            return numerator < 0 ? -result : result;
        return ExactMeanAccumulator.ScaledRatio((BigInteger)numerator, (BigInteger)mass, exponent);
    }

    private static bool TryAlmaWeightGrid(ExactMeanAccumulator.ProductFactor[] factors, out int scale)
    {
        scale = int.MaxValue;
        int end = 0;
        foreach (var factor in factors)
        {
            if (factor.Mantissa < 0) return false;
            if (factor.Mantissa == 0) continue;
            scale = Math.Min(scale, factor.Scale);
            end = Math.Max(end, factor.Scale + 64 - BitOperations.LeadingZeroCount((ulong)factor.Mantissa));
        }
        // Nonzero normalized mantissas plus this width bound also prove every
        // coefficient shift is nonnegative and less than 127.
        return scale != int.MaxValue && end - scale <= 127;
    }

    private struct AlmaPriceGrid
    {
        internal int Scale;
        private int _end;
        private double _maximum;
        private bool _hasValues;
        internal void Include(double value)
        {
            var factor = new ExactMeanAccumulator.ProductFactor(value);
            long mantissa = factor.Mantissa;
            if (mantissa == 0) return;
            ulong magnitude = (ulong)(mantissa < 0 ? -mantissa : mantissa);
            int end = factor.Scale + 64 - BitOperations.LeadingZeroCount(magnitude);
            if (!_hasValues) { Scale = factor.Scale; _end = end; _hasValues = true; }
            else { Scale = Math.Min(Scale, factor.Scale); _end = Math.Max(_end, end); }
            _maximum = Math.Max(_maximum, Math.Abs(value));
        }
        internal readonly bool TryMaximum(out Int128 maximum)
        {
            maximum = 0;
            if (!_hasValues) return true;
            if (_end - Scale > 127) return false;
            maximum = Coefficient(_maximum);
            return true;
        }
        internal readonly Int128 Coefficient(double value)
        {
            var factor = new ExactMeanAccumulator.ProductFactor(value);
            return factor.Mantissa == 0 ? 0 : (Int128)factor.Mantissa << (factor.Scale - Scale);
        }
    }
}
#endif
