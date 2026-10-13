#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillRegressionStatistics(Bar[] source, double[][] output,
        WindowRegressionStatistics indicator, CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        cancellation.ThrowIfCancellationRequested();
        int period = indicator.Period;
        int chunks = period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;
        // Ten exact moment-derived columns justify the existing expensive-window
        // scheduling threshold. The overlap remains bounded by the fit period.
        if (chunks < 2 || !CanParallelize(source.Length, 512) || !Monitor.TryEnter(ParallelBarGate))
        {
            using var state = new WindowRegressionStatistics.State(period);
            Span<double> values = stackalloc double[10];
            Bar last = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                last = source[i];
                if (!AllFieldsFinite(in last)) IndicatorInputDomain.Finite.Validate(in last);
                if (owned is not null) owned[i] = last;
                state.Update(in last, values);
                for (int slot = 0; slot < 10; slot++) output[slot][i] = values[slot];
            }
            return last;
        }

        double[]? close = null;
        try
        {
            // Capture once, validating every OHLCV field before worker execution.
            // Full already owns the bars; LatestOnly needs only temporary closes.
            if (owned is null) close = ArrayPool<double>.Shared.Rent(source.Length);
            Bar last = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                last = source[i];
                if (!AllFieldsFinite(in last)) IndicatorInputDomain.Finite.Validate(in last);
                if (owned is null) close![i] = last.Close; else owned[i] = last;
            }
            CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                using var state = new WindowRegressionStatistics.State(period);
                Span<double> values = stackalloc double[10];
                // Window-local coordinates and exact moments need only P-1
                // preceding observations, including changing/constant windows.
                for (int i = Math.Max(0, start - period + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var bar = new Bar(default, 0, 0, 0, owned is null ? close![i] : owned[i].Close, 0);
                    state.Update(in bar, values);
                    if (i >= start)
                        for (int slot = 0; slot < 10; slot++) output[slot][i] = values[slot];
                }
            });
            cancellation.ThrowIfCancellationRequested();
            // Leave all output checks to the caller's slot-major scan. In
            // particular an earlier intercept overflow must not hide a later
            // slope overflow, and neither may hide an invalid input field.
            return last;
        }
        finally
        {
            Monitor.Exit(ParallelBarGate);
            if (close is not null) ArrayPool<double>.Shared.Return(close);
        }
    }
}
#endif
