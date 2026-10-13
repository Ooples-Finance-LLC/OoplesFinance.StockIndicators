#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int RateOfChangeWindow(RateOfChangeWithAverage owner)
    {
        // A complete average needs its returns plus the lag preceding the first.
        long window = (long)owner.Period + (owner.AveragePeriod ?? 1);
        return window <= 4096 ? (int)window : 0;
    }

    private static Bar FillRateOfChange(Bar[] source, double[][] output, RateOfChangeWithAverage owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int window = RateOfChangeWindow(owner);
        int chunks = Math.Min(WorkerCount(), source.Length / window);
        bool locked = false;
        Bar[] captured = ArrayPool<Bar>.Shared.Rent(source.Length);
        Bar latest = default;
        try
        {
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                captured[i] = latest;
                if (owned is not null) owned[i] = latest;
            }
            if (chunks >= 2 && CanParallelize(source.Length, 512)) locked = Monitor.TryEnter(ParallelBarGate);
            if (!locked) chunks = 1;
            void Fill(int chunk)
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                var state = new RateOfChangeWithAverage.State(owner.Period, owner.AveragePeriod);
                Span<double> values = stackalloc double[5];
                for (int i = Math.Max(0, start - window + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    state.Update(in captured[i], values);
                    if (i >= start)
                        for (int slot = 0; slot < 5; slot++) output[slot][i] = values[slot];
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
