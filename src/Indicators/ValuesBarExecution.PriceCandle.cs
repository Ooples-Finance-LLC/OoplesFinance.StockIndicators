#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillPriceCandle(Bar[] source, double[][] output, PriceCandlePattern owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        bool locked = false;
        Bar[] captured = ArrayPool<Bar>.Shared.Rent(source.Length);
        Bar latest = default;
        try
        {
            // Retain every raw field and validate the complete source before
            // publishing detail overflow; no candle depends on its neighbors.
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                captured[i] = latest;
                if (owned is not null) owned[i] = latest;
            }
            if (CanParallelize(source.Length, 512)) locked = Monitor.TryEnter(ParallelBarGate);
            int chunks = locked ? WorkerCount() : 1;
            void Fill(int chunk)
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                var state = new PriceCandlePattern.State(owner.Kind, owner.Percent);
                Span<double> values = stackalloc double[12];
                for (int i = start; i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    state.Update(in captured[i], values);
                    for (int slot = 0; slot < 12; slot++) output[slot][i] = values[slot];
                }
            }
            if (chunks == 1) Fill(0);
            else CpuParallelSettings.LightweightParallel(chunks, chunks, Fill);
            cancellation.ThrowIfCancellationRequested();
            return latest;
        }
        finally
        {
            if (locked) Monitor.Exit(ParallelBarGate);
            ArrayPool<Bar>.Shared.Return(captured);
        }
    }
}
#endif
