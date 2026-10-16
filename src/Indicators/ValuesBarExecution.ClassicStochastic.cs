#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int ClassicStochasticWindow(ClassicStochastic owner)
    {
        static bool Finite(ClassicMovingAverage average) => average.Period == 1
            || average.Method is ClassicAverageMethod.Sma or ClassicAverageMethod.Wma or ClassicAverageMethod.Trima;
        long window = owner.First + 1;
        return Finite(owner.KAverage) && Finite(owner.DAverage) && window <= 4096 ? (int)window : 0;
    }

    private static Bar FillClassicStochastic(Bar[] source, double[][] output, ClassicStochastic owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int period = ClassicStochasticWindow(owner);
        int chunks = Math.Min(WorkerCount(), source.Length / period);
        bool locked = false;
        Bar[] captured = ArrayPool<Bar>.Shared.Rent(source.Length);
        Bar latest = default;
        try
        {
            // Price-window replay needs the original high/low as well as close.
            // Capture each input once, before validating any published output.
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
                using var state = new ClassicStochastic.State(owner);
                Span<double> values = stackalloc double[4];
                for (int i = Math.Max(0, start - period + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    state.Update(in captured[i], values);
                    if (i >= start)
                        for (int slot = 0; slot < 4; slot++) output[slot][i] = values[slot];
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
