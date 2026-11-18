#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines.Simd;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillLaggedBlocks(Bar[] source, double[][] output, LaggedPriceChange indicator, int window,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator.Kind switch
    {
        PriceChangeKind.Difference => FillLaggedBlocks<LaggedDifference>(source, output, indicator, window, cancellation, owned),
        PriceChangeKind.Fraction => FillLaggedBlocks<LaggedFraction>(source, output, indicator, window, cancellation, owned),
        PriceChangeKind.Percent => FillLaggedBlocks<LaggedPercent>(source, output, indicator, window, cancellation, owned),
        PriceChangeKind.Ratio => FillLaggedBlocks<LaggedRatio>(source, output, indicator, window, cancellation, owned),
        _ => FillLaggedBlocks<LaggedRatioPercent>(source, output, indicator, window, cancellation, owned)
    };

    private interface ILaggedBlock
    {
        static abstract bool NeedsCertificate { get; }
        static abstract PriceChangeKind Kind { get; }
        static abstract void Transform(ReadOnlySpan<double> current, ReadOnlySpan<double> prior,
            Span<double> result, ReadOnlySpan<double> hundred);
        static abstract bool IsZero(double current, double prior);
    }

    private readonly struct LaggedDifference : ILaggedBlock
    {
        public static bool NeedsCertificate => false;
        public static PriceChangeKind Kind => PriceChangeKind.Difference;
        public static void Transform(ReadOnlySpan<double> current, ReadOnlySpan<double> prior,
            Span<double> result, ReadOnlySpan<double> hundred) => SimdKernels.VectorSubtract(current, prior, result);
        public static bool IsZero(double current, double prior) => current == prior;
    }

    private readonly struct LaggedRatio : ILaggedBlock
    {
        public static bool NeedsCertificate => false;
        public static PriceChangeKind Kind => PriceChangeKind.Ratio;
        public static void Transform(ReadOnlySpan<double> current, ReadOnlySpan<double> prior,
            Span<double> result, ReadOnlySpan<double> hundred) => SimdKernels.VectorDivide(current, prior, result);
        // A quotient of NONZERO operands may underflow to signed zero. Preserve it.
        public static bool IsZero(double current, double prior) => current == 0 || prior == 0;
    }

    private readonly struct LaggedFraction : ILaggedBlock
    {
        public static bool NeedsCertificate => true;
        public static PriceChangeKind Kind => PriceChangeKind.Fraction;
        public static void Transform(ReadOnlySpan<double> current, ReadOnlySpan<double> prior,
            Span<double> result, ReadOnlySpan<double> hundred)
        {
            SimdKernels.VectorSubtract(current, prior, result);
            SimdKernels.VectorDivide(result, prior, result);
        }
        public static bool IsZero(double current, double prior) => prior == 0 || current == prior;
    }

    private readonly struct LaggedPercent : ILaggedBlock
    {
        public static bool NeedsCertificate => true;
        public static PriceChangeKind Kind => PriceChangeKind.Percent;
        public static void Transform(ReadOnlySpan<double> current, ReadOnlySpan<double> prior,
            Span<double> result, ReadOnlySpan<double> hundred)
        {
            SimdKernels.VectorSubtract(current, prior, result);
            SimdKernels.VectorMultiply(result, hundred, result);
            SimdKernels.VectorDivide(result, prior, result);
        }
        public static bool IsZero(double current, double prior) => prior == 0 || current == prior;
    }

    private readonly struct LaggedRatioPercent : ILaggedBlock
    {
        public static bool NeedsCertificate => true;
        public static PriceChangeKind Kind => PriceChangeKind.RatioPercent;
        public static void Transform(ReadOnlySpan<double> current, ReadOnlySpan<double> prior,
            Span<double> result, ReadOnlySpan<double> hundred)
        {
            SimdKernels.VectorMultiply(current, hundred, result);
            SimdKernels.VectorDivide(result, prior, result);
        }
        public static bool IsZero(double current, double prior) => current == 0 || prior == 0;
    }

    private static PointwiseRegion ComputeLaggedBlocks<T>(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation) where T : struct, ILaggedBlock
    {
        const int blockSize = 64;
        int period = window - 1;
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        // For accepted grid facts, every signed sum with total absolute weights
        // <=200 is exact in binary64. This certifies both a-b and 100*(a-b),
        // including the separate 100*a case, before the one final division.
        var proof = new Core.SmaCpuKernel.Certificate(200);
        bool certified = true;
        foreach (ref readonly var bar in seed)
        {
            if (cancellation.IsCancellationRequested) return region;
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
            if (T.NeedsCertificate && certified) certified = proof.Include(bar.Close);
        }

        int tailStart = end - tail.Length;
        for (int row = start; row < end; row++)
        {
            if (cancellation.IsCancellationRequested) return region;
            var bar = row >= tailStart ? tail[row - tailStart] : source[row];
            region.Last = bar; // Keep the final CHRONOLOGICAL input, never a reverse-loop row.
            if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; return region; }
            output[row] = bar.Close;
            if (owned is not null) owned[row] = bar;
            if (T.NeedsCertificate && certified) certified = proof.Include(bar.Close);
        }

        Span<double> current = stackalloc double[blockSize];
        Span<double> prior = stackalloc double[blockSize];
        Span<double> result = stackalloc double[blockSize];
        Span<double> hundred = stackalloc double[blockSize];
        hundred.Fill(100);
        int first = Math.Max(start, period);
        for (int blockEnd = end; blockEnd > first;)
        {
            // Cancellation remains bounded by one 64-lane transform/publication;
            // workers return quietly and the caller throws after all quiesce.
            if (cancellation.IsCancellationRequested) return region;
            int blockStart = Math.Max(first, blockEnd - blockSize);
            int count = blockEnd - blockStart;
            // Load ALL lanes before writing any output, also when P<4 or P<64.
            // Own lower output cells still contain raw closes; another worker's
            // cells may already be transformed, so only immutable seed is used.
            output.AsSpan(blockStart, count).CopyTo(current);
            int priorStart = blockStart - period;
            if (priorStart >= start)
            {
                output.AsSpan(priorStart, count).CopyTo(prior);
            }
            else
            {
                int seedCount = Math.Min(count, start - priorStart);
                for (int offset = 0; offset < seedCount; offset++)
                {
                    int row = blockStart + offset;
                    prior[offset] = seed[(row - period) - (start - period)].Close;
                }
                if (seedCount < count)
                    output.AsSpan(start, count - seedCount).CopyTo(prior[seedCount..]);
            }
            if (certified)
            {
                T.Transform(current[..count], prior[..count], result[..count], hundred[..count]);
            }
            else
            {
                for (int offset = 0; offset < count; offset++)
                    result[offset] = LaggedPriceChange.Calculate(current[offset], prior[offset], T.Kind);
            }
            for (int offset = 0; offset < count; offset++)
            {
                int row = blockStart + offset;
                double value = certified && T.IsZero(current[offset], prior[offset]) ? 0 : result[offset];
                output[row] = value;
                // Reverse visitation must still report the first chronological
                // arithmetic failure, with that same row's invalid value.
                if (!double.IsFinite(value) && (region.InvalidOutputIndex < 0 || row < region.InvalidOutputIndex))
                {
                    region.InvalidOutputIndex = row;
                    region.InvalidOutputValue = value;
                }
            }
            blockEnd = blockStart;
        }
        // Earlier closes must survive until every dependent mature row is done.
        if (cancellation.IsCancellationRequested) return region;
        if (start < period) output.AsSpan(start, Math.Min(end, period) - start).Clear();
        return region;
    }

    private static Bar FillLaggedBlocks<T>(Bar[] source, double[][] output, LaggedPriceChange indicator, int period,
        CancellationToken cancellation, OwnedBarBuffer? owned) where T : struct, ILaggedBlock
    {
        cancellation.ThrowIfCancellationRequested();
        int chunks = period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;

        bool lockTaken = false;
        Bar[][]? tails = null;
        try
        {
            if (chunks >= 2 && CanParallelize(source.Length, BoundedParallelMinimum(indicator)))
                Monitor.TryEnter(ParallelBarGate, ref lockTaken);
            if (!lockTaken)
            {
                var region = ComputeLaggedBlocks<T>(source, output[0], owned, 0, source.Length, period, [], [], cancellation);
                cancellation.ThrowIfCancellationRequested();
                ValidatePointwiseInput(in region);
                ValidatePointwiseOutput(indicator, in region);
                return region.Last;
            }
            tails = new Bar[chunks - 1][];
            var regions = new PointwiseRegion[chunks];
            // Each tail belongs to one disjoint input region. Its owner consumes
            // this capture too, so no source bar is read a second time for seeding.
            for (int chunk = 0; chunk < tails.Length; chunk++)
            {
                cancellation.ThrowIfCancellationRequested();
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                tails[chunk] = period == 1 ? [] : System.Buffers.ArrayPool<Bar>.Shared.Rent(period - 1);
                source.AsSpan(end - period + 1, period - 1).CopyTo(tails[chunk]);
            }
            AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                regions[chunk] = ComputeLaggedBlocks<T>(source, output[0], owned, start, end, period,
                    chunk == 0 ? [] : tails[chunk - 1].AsSpan(0, period - 1),
                    chunk == chunks - 1 ? [] : tails[chunk].AsSpan(0, period - 1), cancellation);
            });
            cancellation.ThrowIfCancellationRequested();
            foreach (var region in regions) ValidatePointwiseInput(in region);
            foreach (var region in regions) ValidatePointwiseOutput(indicator, in region);
            return regions[^1].Last;
        }
        finally
        {
            // Workers have quiesced. Release the gate even if pool cleanup fails.
            if (lockTaken) Monitor.Exit(ParallelBarGate);
            if (tails is not null)
                foreach (var tail in tails)
                    if (tail is { Length: > 0 }) System.Buffers.ArrayPool<Bar>.Shared.Return(tail);
        }
    }

}
#endif
