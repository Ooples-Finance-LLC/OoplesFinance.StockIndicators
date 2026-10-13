#if !NETFRAMEWORK
using System.Buffers;
using System.Runtime.ExceptionServices;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillNullableBalanceOfPower(Bar[] source, double[][] output, BalanceOfPowerWithValidity owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int chunks = Math.Min(WorkerCount(), source.Length / owner.Period);
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
            var failures = new ExceptionDispatchInfo?[chunks];
            void Fill(int chunk)
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                var state = new BalanceOfPowerWithValidity.State(owner.Period);
                Span<double> values = stackalloc double[2];
                try
                {
                    for (int i = Math.Max(0, start - owner.Period + 1); i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        state.Update(in captured[i], values);
                        if (i >= start) { output[0][i] = values[0]; output[1][i] = values[1]; }
                    }
                }
                catch (Exception error) { failures[chunk] = ExceptionDispatchInfo.Capture(error); }
            }
            if (chunks == 1) Fill(0);
            else CpuParallelSettings.LightweightParallel(chunks, chunks, Fill);
            cancellation.ThrowIfCancellationRequested();
            // Preserve the original arithmetic exception without worker wrapping.
            // Chunk order also preserves the first failing input in the trajectory.
            foreach (var failure in failures) failure?.Throw();
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
