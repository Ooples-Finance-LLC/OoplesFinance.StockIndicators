#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeChoppinessColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, int period, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int count = seed.Length + end - start;
        var captured = System.Buffers.ArrayPool<Bar>.Shared.Rent(count);
        try
        {
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
                var state = new WindowChoppinessIndex.CompactState(period, count);
                Span<double> values = stackalloc double[2];
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    state.Update(in captured[i], values);
                    if (i < seed.Length) continue;
                    int row = start + i - seed.Length;
                    output[0][row] = values[0]; output[1][row] = values[1];
                    RecordChoppinessOutput(ref region, row, values[0]);
                }
                return region;
            }
            var highs = new WindowExtremeDeque(period, maximum: true);
            var lows = new WindowExtremeDeque(period, maximum: false);
            double sum = 0, logPeriod = Math.Log(period);
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                if (i > 0)
                {
                    double high = Math.Max(captured[i].High, captured[i - 1].Close);
                    double low = Math.Min(captured[i].Low, captured[i - 1].Close);
                    sum += high - low;
                    highs.Add(high); lows.Add(low);
                }
                if (i >= seed.Length)
                {
                    int row = start + i - seed.Length;
                    double span = i >= period ? highs.Value - lows.Value : 0;
                    bool defined = i >= period && span != 0;
                    double value = !defined ? 0 : sum == 0 ? double.NegativeInfinity
                        : 100 * (ExactIntervalLogRatio.Calculate(JurikDyadic.FromDouble(sum), JurikDyadic.FromDouble(span)) / logPeriod);
                    output[0][row] = value; output[1][row] = defined ? 1 : 0;
                    RecordChoppinessOutput(ref region, row, value);
                }
                // At most P intervals, each containing two certified price terms.
                // Eviction uses the owned capture rather than a separate queue.
                if (i >= period)
                {
                    int old = i - period + 1;
                    sum -= Math.Max(captured[old].High, captured[old - 1].Close)
                        - Math.Min(captured[old].Low, captured[old - 1].Close);
                }
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    private static void RecordChoppinessOutput(ref PointwiseRegion region, int index, double value)
    {
        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
        {
            region.InvalidOutputIndex = index; region.InvalidOutputValue = value;
        }
    }
}
#endif
