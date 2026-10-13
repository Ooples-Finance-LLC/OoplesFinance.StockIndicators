#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int PriceRelativeWindow(PriceRelativeStrength owner)
    {
        // The mean uses direct ratios, independently of the return lookback.
        long window = Math.Max((long)(owner.Period ?? 0) + 1, owner.MeanPeriod ?? 1);
        return window <= 4096 ? (int)window : 0;
    }

    private static Bar FillPriceRelative(Bar[] source, double[][] output, PriceRelativeStrength owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int window = PriceRelativeWindow(owner);
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
            // A ratio overflow can skip state advancement. Its primary output
            // still rejects the entire run during slot-major validation, before
            // any later trajectory can be published. Missing ratios do advance.
            void Fill(int chunk)
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                var state = new PriceRelativeStrength.State(owner.Period, owner.MeanPeriod, owner.Evaluation, owner.Basis);
                Span<double> values = stackalloc double[6];
                for (int i = Math.Max(0, start - window + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    state.Update(in captured[i], values);
                    if (i >= start)
                        for (int slot = 0; slot < 6; slot++) output[slot][i] = values[slot];
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
