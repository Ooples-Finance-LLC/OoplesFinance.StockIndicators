#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeVortexColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, int period, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int count = seed.Length + end - start;
        var captured = System.Buffers.ArrayPool<Bar>.Shared.Rent(count);
        try
        {
            // Capture once: certification and fallback consume the same values,
            // including the owner-captured overlap at worker boundaries.
            long terms = 2L * period;
            var proof = new Core.SmaCpuKernel.Certificate((int)Math.Min(int.MaxValue, terms));
            bool certified = terms <= int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; region.Last = bar; return region; }
                captured[i] = bar;
                if (i >= seed.Length)
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                }
                if (certified) certified = proof.Include(bar.High) & proof.Include(bar.Low) & proof.Include(bar.Close);
            }
            if (!certified)
            {
                var state = new WindowVortex.CompactState(period, count);
                Span<double> values = stackalloc double[4];
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    state.Update(in captured[i], values);
                    if (i < seed.Length) continue;
                    int row = start + i - seed.Length;
                    for (int slot = 0; slot < 4; slot++) output[slot][row] = values[slot];
                }
                return region;
            }
            double range = 0, positive = 0, negative = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                if (i > 0)
                {
                    ref readonly var current = ref captured[i];
                    ref readonly var previous = ref captured[i - 1];
                    range += VortexRange(in current, in previous);
                    positive += Math.Abs(current.High - previous.Low);
                    negative += Math.Abs(current.Low - previous.High);
                }
                if (i >= seed.Length)
                {
                    int row = start + i - seed.Length;
                    bool defined = i >= period && range != 0;
                    output[0][row] = defined && positive != 0 ? positive / range : 0;
                    output[1][row] = defined && negative != 0 ? negative / range : 0;
                    output[2][row] = output[3][row] = defined ? 1 : 0;
                }
                // Publish then evict: all intermediate sums contain at most P
                // differences, each bounded by two certified price terms.
                if (i >= period)
                {
                    int old = i - period + 1;
                    ref readonly var current = ref captured[old];
                    ref readonly var previous = ref captured[old - 1];
                    range -= VortexRange(in current, in previous);
                    positive -= Math.Abs(current.High - previous.Low);
                    negative -= Math.Abs(current.Low - previous.High);
                }
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    private static double VortexRange(in Bar current, in Bar previous) => Math.Max(current.High - current.Low,
        Math.Max(Math.Abs(current.High - previous.Close), Math.Abs(current.Low - previous.Close)));
}
#endif
