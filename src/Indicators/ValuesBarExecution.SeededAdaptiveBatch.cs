#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines.Simd;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed partial class SeededAdaptiveValueState
    {
        private const long BatchPriceLimit = (1L << 53) - 1;
        private const long BatchChangeLimit = 1L << 24;

        // Finite single-owner batch only. Execute disposes the node after it has
        // published arrays/history; streaming and mixed graphs retain Update.
        internal Bar FillBatch(Bar[] source, double[][] values, CancellationToken cancellation,
            OwnedBarBuffer? owned, Action<Exception> captureFailure)
        {
            double[] average = values[0], efficiency = values[1];
            int grid = int.MaxValue;
            double maximum = 0;
            Bar latest = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var bar = source[i];
                latest = bar;
                if (owned is not null) owned[i] = bar;
                if (!AllFieldsFinite(in bar)) IndicatorInputDomain.Finite.Validate(in bar);
                // Retain closes until the whole numeric certificate succeeds,
                // so fallback never rereads caller bars or loses their history.
                average[i] = bar.Close;
                IncludeIntegerGrid(bar.Close, ref grid, ref maximum);
            }
            if (grid == int.MaxValue) grid = 0;
            if (Math.ScaleB(maximum, -grid) > BatchPriceLimit)
            {
                ReplayCaptured(average, values, cancellation, captureFailure);
                return latest;
            }
            var units = new long[source.Length];
            var prefix = new float[source.Length];
            long total = 0, previous = 0;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                double scaled = Math.ScaleB(average[i], -grid);
                long unit = (long)scaled;
                if (scaled != unit || Math.ScaleB((double)unit, grid) != average[i])
                {
                    ReplayCaptured(average, values, cancellation, captureFailure);
                    return latest;
                }
                units[i] = unit;
                long change = i == 0 ? 0 : Math.Abs(unit - previous);
                if (change > BatchChangeLimit - total)
                {
                    ReplayCaptured(average, values, cancellation, captureFailure);
                    return latest;
                }
                total += change;
                prefix[i] = change;
                previous = unit;
            }
            // Every input, vector intermediate, block carry and tail sum is a
            // nonnegative integer <=2^24, exactly representable in binary32.
            ScanKernels.PrefixSumFloat(prefix, prefix);
            int count = source.Length - owner.Period;
            int chunks = Math.Min(WorkerCount(), 1 + (count - 1) / 1024);
            try
            {
                CpuParallelSettings.LightweightParallel(chunks, chunk =>
                {
                    int first = owner.Period + (int)((long)count * chunk / chunks);
                    int end = owner.Period + (int)((long)count * (chunk + 1) / chunks);
                    for (int i = first; i < end; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        // Prefix subtraction is another exact bounded integer.
                        int volatility = (int)(prefix[i] - prefix[i - owner.Period]);
                        long distance = Math.Abs(units[i] - units[i - owner.Period]);
                        double er = volatility == 0 ? owner.FastFlat ? 1 : 0
                            : (double)distance / volatility;
                        var rate = JurikDyadic.SumProduct(_rates.Slow, _rates.Difference, er).Mean(1);
                        // No coefficient/rate reassociation: retain its original
                        // exact interpolation and separately rounded square.
                        average[i] = rate * rate;
                        efficiency[i] = er;
                    }
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                cancellation.ThrowIfCancellationRequested();
                throw;
            }
            catch (AggregateException error) when (cancellation.IsCancellationRequested
                && error.Flatten().InnerExceptions.All(e => e is OperationCanceledException))
            {
                cancellation.ThrowIfCancellationRequested();
                throw;
            }
            // Rates are independent; the once-rounded adaptive recurrence is
            // not. Consume rates in order, overwriting only the consumed cell.
            JurikDyadic current = default;
            double[] averageDefined = values[2], efficiencyDefined = values[3];
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var price = JurikDyadic.FromDouble(Math.ScaleB((double)units[i], grid));
                if (i < owner.Period || owner.ResetFlat && prefix[i] == prefix[i - owner.Period])
                    current = price;
                else current = Blend(current, price, average[i]);
                bool present = i >= owner.FirstOutputIndex;
                average[i] = present ? current.Mean(1) : 0;
                averageDefined[i] = present ? 1 : 0;
                efficiencyDefined[i] = i >= owner.Period ? 1 : 0;
            }
            return latest;
        }

        private void ReplayCaptured(double[] closes, double[][] values, CancellationToken cancellation,
            Action<Exception> captureFailure)
        {
            Span<double> scratch = stackalloc double[4];
            bool failed = false;
            for (int i = 0; i < closes.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (failed) continue;
                // This state reads only Close. Full raw bars have already been
                // validated and captured once, before any arithmetic failure.
                var bar = new Bar(default, 0, 0, 0, closes[i], 0);
                try
                {
                    Update(in bar, scratch);
                    values[0][i] = scratch[0];
                    values[1][i] = scratch[1];
                    values[2][i] = scratch[2];
                    values[3][i] = scratch[3];
                }
                catch (Exception error)
                {
                    captureFailure(error);
                    failed = true;
                }
            }
        }
    }
}
#endif
