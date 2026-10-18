#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillVolumeWeightedPrice(Bar[] source, double[][] output, VolumeWeightedPrice indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (indicator is { Period: int period, StartDate: null })
            return FillBoundedWindow<VolumePriceKernel>(source, output, indicator, period, cancellation, owned);

        cancellation.ThrowIfCancellationRequested();
        bool parallel = CanParallelize(source.Length, 512)
            && (indicator.Period is null || indicator.Period <= 4096);
        if (!parallel || !Monitor.TryEnter(ParallelBarGate))
        {
            var state = new VolumeWeightedPrice.CompactState(indicator.Period, indicator.TypicalPrice, indicator.StartDate);
            Span<double> values = stackalloc double[2];
            Bar last = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                last = source[i];
                if (!AllFieldsFinite(in last)) IndicatorInputDomain.Finite.Validate(in last);
                if (owned is not null) owned[i] = last;
                state.Update(in last, values);
                output[0][i] = values[0]; output[1][i] = values[1];
            }
            return last;
        }

        Bar[]? captured = null;
        try
        {
            // Full already owns a bar capture. LatestOnly uses temporary pooled
            // bars; neither worker phase rereads caller-owned source memory.
            if (owned is null) captured = ArrayPool<Bar>.Shared.Rent(source.Length);
            Bar last = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                last = source[i];
                if (!AllFieldsFinite(in last)) IndicatorInputDomain.Finite.Validate(in last);
                if (owned is null) captured![i] = last; else owned[i] = last;
            }
            Bar At(int index) => owned is null ? captured![index] : owned[index];
            int chunks = WorkerCount();
            var prefixes = new (JurikDyadic Weighted, JurikDyadic Mass)[chunks];
            if (indicator.Period is null)
            {
                CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
                {
                    int start = (int)((long)source.Length * chunk / chunks);
                    int end = (int)((long)source.Length * (chunk + 1) / chunks);
                    var state = new VolumeWeightedPrice.CompactState(null, indicator.TypicalPrice, indicator.StartDate);
                    for (int i = start; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var bar = At(i);
                        state.Accumulate(in bar);
                    }
                    prefixes[chunk] = state.Totals;
                });
                cancellation.ThrowIfCancellationRequested();
                // Exact sums are associative, including wide cancellation. Merge
                // unrounded totals, never published ratios or rounded products.
                JurikDyadic weighted = default, mass = default;
                for (int chunk = 0; chunk < chunks; chunk++)
                {
                    var total = prefixes[chunk];
                    prefixes[chunk] = (weighted, mass);
                    weighted.AddExact(total.Weighted); mass.AddExact(total.Mass);
                }
            }
            CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                var state = new VolumeWeightedPrice.CompactState(indicator.Period, indicator.TypicalPrice, indicator.StartDate);
                if (indicator.Period is int length)
                {
                    int origin = start, remaining = length - 1;
                    // Anchored windows count qualifying arrivals, not adjacent
                    // indices. Dates need not be monotonic, even inside a block.
                    while (origin > 0 && remaining > 0)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var bar = At(--origin);
                        if (bar.Time >= indicator.StartDate!.Value) remaining--;
                    }
                    for (int i = origin; i < start; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var bar = At(i);
                        state.Accumulate(in bar);
                    }
                }
                else state.Seed(prefixes[chunk].Weighted, prefixes[chunk].Mass);
                Span<double> values = stackalloc double[2];
                for (int i = start; i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var bar = At(i);
                    state.Update(in bar, values);
                    output[0][i] = values[0]; output[1][i] = values[1];
                }
            });
            cancellation.ThrowIfCancellationRequested();
            // The caller scans both columns in slot order after all input has
            // been validated; signed volumes can yield nonfinite final ratios.
            return last;
        }
        finally
        {
            Monitor.Exit(ParallelBarGate);
            if (captured is not null) ArrayPool<Bar>.Shared.Return(captured);
        }
    }
}
#endif
