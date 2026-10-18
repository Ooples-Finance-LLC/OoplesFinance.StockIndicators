#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillClassicAverage(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var average = indicator as ClassicMovingAverage;
        var oscillator = indicator as ClassicPriceOscillator;
        int period = average?.Period ?? oscillator!.SlowPeriod;
        var method = average?.Method ?? oscillator!.Method;
        bool bounded = period == 1 || method is ClassicAverageMethod.Sma or ClassicAverageMethod.Wma or ClassicAverageMethod.Trima;
        int chunks = bounded && period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;
        bool locked = false;
        double[] prices = ArrayPool<double>.Shared.Rent(source.Length);
        Bar latest = default;
        try
        {
            // Capture each input once before publishing or validating outputs.
            // Workers replay only captured prices; Full retains original OHLCV.
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
                int origin = bounded ? Math.Max(0, start - period + 1) : 0;
                Span<double> values = stackalloc double[2];
                // Every worker owns and disposes its complete engine, including
                // recursive/adaptive engines on the sequential fallback path.
                if (average is not null)
                {
                    using var state = new ClassicMovingAverage.State(average, average.First);
                    for (int i = origin; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var bar = new Bar(default, 0, 0, 0, prices[i], 0);
                        state.Update(in bar, values);
                        if (i >= start) { output[0][i] = values[0]; output[1][i] = values[1]; }
                    }
                }
                else
                {
                    using var state = new ClassicPriceOscillator.State(oscillator!);
                    for (int i = origin; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var bar = new Bar(default, 0, 0, 0, prices[i], 0);
                        state.Update(in bar, values);
                        if (i >= start) { output[0][i] = values[0]; output[1][i] = values[1]; }
                    }
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
