#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsCurvatureChain(IReadOnlyList<IIndicator> indicators) => indicators.Count == 1
        && indicators[0] is WindowRegressionStatistics { Components.Count: 0,
            Source: WindowLinearRegression { Output: WindowRegressionOutput.Slope, Source: null, Components.Count: 0 } };

    private static IIndicatorRun ExecuteCurvatureChain(Bar[] source, WindowRegressionStatistics last,
        CancellationToken cancellation, OwnedBarHistory? history)
    {
        var first = (WindowLinearRegression)last.Source!;
        int count = source.Length;
        var owned = history is null ? null : new OwnedBarBuffer(count);
        var values = new double[10][];
        for (int slot = 0; slot < 10; slot++)
            values[slot] = slot is 7 or 9 ? values[6] : new double[count];
        double[] close = ArrayPool<double>.Shared.Rent(count);
        double[]? slopes = null;
        bool locked = false;
        Bar latest = default;
        try
        {
            // Capture all input before any output failure, preserving original
            // OHLCV independently of either normalized arithmetic workspace.
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                close[i] = latest.Close;
                if (owned is not null) owned[i] = latest;
            }
            slopes = ArrayPool<double>.Shared.Rent(count);
            int period = Math.Max(first.Period, last.Period);
            int chunks = period <= 4096 ? Math.Min(WorkerCount(), count / period) : 1;
            if (chunks >= 2 && CanParallelize(count, 512)) locked = Monitor.TryEnter(ParallelBarGate);
            if (!locked) chunks = 1;
            bool compactFirst = TryNormalizeCurvatureInput(close, count, first.Period, cancellation, out int firstGrid);
            void First(int chunk)
            {
                int start = (int)((long)count * chunk / chunks), end = (int)((long)count * (chunk + 1) / chunks);
                int origin = Math.Max(0, start - first.Period + 1);
                if (compactFirst)
                {
                    long sum = 0, weighted = 0;
                    for (int i = origin; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        if (i - origin >= first.Period)
                        {
                            sum -= (long)close[i - first.Period];
                            weighted -= sum;
                        }
                        int n = Math.Min(first.Period, i - origin + 1);
                        long value = (long)close[i];
                        weighted += (n - 1L) * value;
                        sum += value;
                        if (i >= start) slopes[i] = n < 2 ? 0
                            : RegressionGridRatio(6 * (2 * weighted - (n - 1L) * sum),
                                (long)n * ((long)n * n - 1), firstGrid);
                    }
                }
                else
                {
                    var state = (IIndicatorState)first.CreateState();
                    using var lifetime = state as IDisposable;
                    for (int i = origin; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var bar = new Bar(default, 0, 0, 0, close[i], 0);
                        double value = state.Update(in bar);
                        if (i >= start) slopes[i] = value;
                    }
                }
            }
            if (chunks == 1) First(0);
            else CpuParallelSettings.LightweightParallel(chunks, chunks, First);
            // The graph validates the complete selected source output before
            // any outer statistics. Unselected inner statistics never reject.
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!double.IsFinite(slopes[i])) IndicatorOutputPolicy.Validate(first, 0, i, slopes[i]);
            }
            bool compactLast = TryNormalizeCurvatureInput(slopes, count, last.Period, cancellation, out int lastGrid);
            bool wideLast = !compactLast && TryNormalizeCurvatureInput(slopes, count, last.Period,
                cancellation, out lastGrid, wide: true);
            void Last(int chunk)
            {
                int start = (int)((long)count * chunk / chunks), end = (int)((long)count * (chunk + 1) / chunks);
                if (compactLast)
                {
                    FillCertifiedRegressionStatistics(slopes, values, start, end, last.Period, lastGrid,
                        cancellation, false, count);
                    return;
                }
                if (wideLast)
                {
                    FillWideCurvatureStatistics(slopes, values, start, end, last.Period, lastGrid, cancellation);
                    return;
                }
                using var state = new WindowRegressionStatistics.State(last.Period);
                Span<double> scratch = stackalloc double[10];
                for (int i = Math.Max(0, start - last.Period + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var bar = new Bar(default, 0, 0, 0, slopes[i], 0);
                    state.Update(in bar, scratch);
                    if (i >= start)
                        for (int slot = 0; slot < 10; slot++) values[slot][i] = scratch[slot];
                }
            }
            if (chunks == 1) Last(0);
            else CpuParallelSettings.LightweightParallel(chunks, chunks, Last);
            var published = new Dictionary<IIndicatorOutput, double[]>(10);
            for (int slot = 0; slot < 10; slot++)
            {
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!double.IsFinite(values[slot][i])) IndicatorOutputPolicy.Validate(last, slot, i, values[slot][i]);
                }
                published.Add(last.Outputs[slot], values[slot]);
            }
            cancellation.ThrowIfCancellationRequested();
            int warmup = Math.Max(first.WarmupBars, last.WarmupBars);
            if (history is not null)
            {
                owned!.TransferTo(history);
                return new IndicatorRun(null, published, history, 0, warmup);
            }
            IBarSnapshot? snapshot = count == 0 ? null : new BarSnapshot(latest, count - 1, published, count >= warmup);
            return new LatestOnlyIndicatorRun(published, count, snapshot);
        }
        finally
        {
            if (locked) Monitor.Exit(ParallelBarGate);
            ArrayPool<double>.Shared.Return(close);
            if (slopes is not null) ArrayPool<double>.Shared.Return(slopes);
        }
    }

    private static bool TryNormalizeCurvatureInput(double[] input, int count, int period,
        CancellationToken cancellation, out int grid, bool wide = false)
    {
        grid = 0;
        if (period > 4096) return false;
        int minimum = int.MaxValue;
        double maximum = 0;
        for (int i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            maximum = Math.Max(maximum, Math.Abs(input[i]));
            var value = ExactMeanAccumulator.DecomposeFinite(input[i]);
            if (value.Integer != 0) minimum = Math.Min(minimum, value.Exponent);
        }
        grid = minimum == int.MaxValue ? 0 : minimum;
        // Narrow moments use the standalone P*B<=2^30 certificate. The wider
        // curvature stage accepts exactly representable signed Int64 inputs;
        // its squared moments promote independently when UInt128 is too small.
        double bound = Math.ScaleB(maximum, -grid);
        if (wide ? bound >= 9223372036854775808d : bound > (1L << 30) / period) return false;
        for (int i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            input[i] = Math.ScaleB(input[i], -grid);
        }
        return true;
    }
}
#endif
