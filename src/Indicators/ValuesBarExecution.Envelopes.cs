#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int AverageEnvelopeWindow(WindowAverageEnvelope owner) =>
        owner.Period <= 4096 && owner.Average is EnvelopeAverage.Alma or EnvelopeAverage.Epma
            or EnvelopeAverage.Hma or EnvelopeAverage.Sma or EnvelopeAverage.Wma
            ? (int)owner.First + 1 : 0;

    private static Bar FillAverageEnvelope(Bar[] source, double[][] output, WindowAverageEnvelope owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int period = AverageEnvelopeWindow(owner);
        int chunks = Math.Min(WorkerCount(), source.Length / period);
        bool locked = false;
        double[] prices = ArrayPool<double>.Shared.Rent(source.Length);
        Bar latest = default;
        try
        {
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                prices[i] = latest.Close;
                if (owned is not null) owned[i] = latest;
            }
            if (chunks >= 2 && CanParallelize(source.Length, 512)) locked = Monitor.TryEnter(ParallelBarGate);
            if (!locked) chunks = 1;
            void Fill(int chunk)
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                // Hull requires the full base window and its final root window.
                // Every replay owns and disposes the original average engine.
                using var state = new WindowAverageEnvelope.State(owner.Period, owner.PercentOffset, owner.Average, owner.First);
                Span<double> values = stackalloc double[4];
                for (int i = Math.Max(0, start - period + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var bar = new Bar(default, 0, 0, 0, prices[i], 0);
                    state.Update(in bar, values);
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
            ArrayPool<double>.Shared.Return(prices);
        }
    }
}
#endif
