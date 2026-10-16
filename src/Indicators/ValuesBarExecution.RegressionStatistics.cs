#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillRegressionStatistics(Bar[] source, double[][] output,
        IIndicator indicator, CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        cancellation.ThrowIfCancellationRequested();
        bool historical = indicator is RegressionWithFinalLine;
        int period = indicator switch
        {
            WindowRegressionStatistics statistics => statistics.Period,
            RegressionWithFinalLine final => final.Period,
            _ => throw new InvalidOperationException("Unqualified regression statistics plan.")
        };
        int chunks = period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;
        // Ten exact moment-derived columns justify the existing expensive-window
        // scheduling threshold. The overlap remains bounded by the fit period.
        if (chunks < 2 || !CanParallelize(source.Length, 512) || !Monitor.TryEnter(ParallelBarGate))
        {
            if (historical) return FillHistoricalRegressionSerial(source, output, period, cancellation, owned);
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
            // Keep original owned OHLCV separate from the normalized close grid.
            close = ArrayPool<double>.Shared.Rent(source.Length);
            Bar last = default;
            int grid = int.MaxValue;
            double maximum = 0;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                last = source[i];
                if (!AllFieldsFinite(in last)) IndicatorInputDomain.Finite.Validate(in last);
                close[i] = last.Close;
                if (owned is not null) owned[i] = last;
                maximum = Math.Max(maximum, Math.Abs(last.Close));
                var value = ExactMeanAccumulator.DecomposeFinite(last.Close);
                if (value.Integer != 0) grid = Math.Min(grid, value.Exponent);
            }
            if (grid == int.MaxValue) grid = 0;
            // P*max|integer| <= 2^30 bounds centered variance by 2^60.
            // P<=4096 also bounds covariance by 2^43 and fitted numerators
            // below 2^58. R-squared's products fit signed Int128 (<2^88).
            bool certified = Math.ScaleB(maximum, -grid) <= (1L << 30) / period;
            if (certified)
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    close[i] = Math.ScaleB(close[i], -grid);
                }
            CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                if (certified)
                {
                    FillCertifiedRegressionStatistics(close, output, start, end, period, grid, cancellation,
                        historical, source.Length);
                    return;
                }
                using var state = new WindowRegressionStatistics.State(period);
                Span<double> values = stackalloc double[10];
                // Window-local coordinates and exact moments need only P-1
                // preceding observations, including changing/constant windows.
                for (int i = Math.Max(0, start - period + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var bar = new Bar(default, 0, 0, 0, close[i], 0);
                    state.Update(in bar, values);
                    if (historical)
                    {
                        if (i < start || i < period - 1) continue;
                        output[0][i] = values[0];
                        output[1][i] = state.GlobalInterceptAt(i + 1);
                        output[2][i] = values[2]; output[3][i] = values[3];
                        output[5][i] = output[6][i] = output[7][i] = 1;
                        output[8][i] = values[8];
                    }
                    else if (i >= start)
                        for (int slot = 0; slot < 10; slot++) output[slot][i] = values[slot];
                }
                if (historical && end == source.Length)
                    for (int position = 0; position < period; position++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        int row = end - period + position;
                        output[4][row] = state.LineAt(position);
                        output[9][row] = 1;
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

    private static void FillCertifiedRegressionStatistics(double[] close, double[][] output,
        int start, int end, int period, int grid, CancellationToken cancellation, bool historical, int totalCount)
    {
        int origin = Math.Max(0, start - period + 1);
        long sum = 0, squares = 0, weighted = 0;
        for (int i = origin; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) return;
            if (i - origin >= period)
            {
                long expired = (long)close[i - period];
                sum -= expired;
                weighted -= sum;
                squares -= expired * expired;
            }
            int count = Math.Min(period, i - origin + 1);
            long value = (long)close[i];
            weighted += (count - 1L) * value;
            sum += value; squares += value * value;
            if (i < start || historical && count < period) continue;
            output[5][i] = 1;
            if (count < 2) continue;
            long variance = count * squares - sum * sum;
            long covariance = 2 * weighted - (count - 1L) * sum;
            long spread = (long)count * count - 1, denominator = count * spread;
            long basis = sum * spread, threeCovariance = 3 * covariance;
            output[0][i] = RegressionGridRatio(2 * threeCovariance, denominator, grid);
            // Global intercepts use the original one-based observation count,
            // not the worker's replay origin. With i<int.MaxValue, this product
            // fits Int128 (<2^78), even when it no longer fits Int64.
            output[1][i] = historical
                ? RegressionGridRatio((Int128)basis + (Int128)threeCovariance * (count - 2L * i - 3), denominator, grid)
                : RegressionGridRatio(basis - threeCovariance * (count + 1L), denominator, grid);
            if (!historical)
                output[4][i] = RegressionGridRatio(basis + threeCovariance * (count - 1L), denominator, grid);
            if (!ExactPopulationDeviation.TryCompactScaledRoot((UInt128)variance,
                (ulong)((long)count * count), 2 * grid, out double deviation))
            {
                var exactVariance = JurikDyadic.FromInteger(variance);
                exactVariance.ScaleByPowerOfTwo(2 * grid);
                deviation = exactVariance.SqrtMean((long)count * count);
            }
            output[2][i] = deviation;
            output[6][i] = output[7][i] = 1;
            if (!historical) output[9][i] = 1;
            if (variance != 0)
            {
                output[3][i] = JurikDyadic.IntegerRatio((Int128)threeCovariance * covariance,
                    (Int128)variance * spread);
                output[8][i] = 1;
            }
            if (historical && i == totalCount - 1)
                for (int position = 0; position < period; position++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    int row = totalCount - period + position;
                    output[4][row] = RegressionGridRatio(basis + threeCovariance * (2L * position - (period - 1L)), denominator, grid);
                    output[9][row] = 1;
                }
        }
    }

    private static double RegressionGridRatio(Int128 numerator, long denominator, int grid)
    {
        if (numerator == 0) return 0;
        double value = Math.ScaleB(JurikDyadic.IntegerRatio(numerator, denominator), grid);
        if (double.IsNormal(value)) return value;
        // Subnormals and overflow require rounding on the final grid, avoiding
        // double rounding and preserving negative underflow zero.
        var exact = JurikDyadic.FromInteger(numerator);
        exact.ScaleByPowerOfTwo(grid);
        return exact.Ratio(JurikDyadic.FromInteger(denominator));
    }

    private static Bar FillHistoricalRegressionSerial(Bar[] source, double[][] output, int period,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        using var writer = new RegressionWithFinalLine.Writer(period, source.Length);
        Span<double> values = stackalloc double[10];
        Bar last = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            last = source[i];
            if (!AllFieldsFinite(in last)) IndicatorInputDomain.Finite.Validate(in last);
            if (owned is not null) owned[i] = last;
            writer.Append(in last, i, output, values);
        }
        writer.Complete(output, cancellation);
        return last;
    }
}
#endif
