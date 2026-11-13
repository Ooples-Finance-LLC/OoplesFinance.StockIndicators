#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillAccumulationDistribution(Bar[] source, double[][] output,
        AccumulationDistributionWithAverage indicator, CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        Bar latest = default;
        // Reuse the final owned columns as capture storage. All fields are
        // validated before arithmetic, and caller-owned input is never reread.
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            latest = source[i];
            if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
            if (owned is not null) owned[i] = latest;
            output[0][i] = latest.High;
            output[1][i] = latest.Low;
            output[2][i] = latest.Close;
            output[3][i] = latest.Volume;
        }
        var summary = new Core.SmaCpuKernel.GridSummary();
        bool finiteFlows = true;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            double high = output[0][i], low = output[1][i], close = output[2][i], volume = output[3][i];
            double flow = AccumulationFlow(high, low, close, volume, out double multiplier);
            output[0][i] = multiplier;
            output[1][i] = flow;
            output[3][i] = 0;
            if (double.IsFinite(flow)) summary.Include(flow);
            else finiteFlows = false;
        }
        // Generic slot-major validation reports a multiplier or flow overflow
        // before inspecting cumulative/average columns; no run is published.
        if (!finiteFlows) return latest;
        if (source.Length != 0 && summary.Certifies(source.Length))
        {
            double total = 0;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                total += output[1][i];
                output[2][i] = total == 0 ? 0 : total;
            }
        }
        else if (source.Length >= 8192 && finiteFlows && summary.CertifiesCompensatedPrefix(source.Length))
        {
            // Preserve the published flow column. The existing pair writer reads
            // each captured flow once, then overwrites its two destinations.
            output[1].AsSpan().CopyTo(output[2]);
            FillIdentityAdlPair([output[2], output[2]], cancellation);
        }
        else
        {
            var total = default(CompactMacdValue);
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                total += CompactMacdValue.Of(output[1][i]);
                output[2][i] = total.PublishRoundedSum();
            }
        }
        if (indicator.AveragePeriod is int period && period <= source.Length)
            FillCachedAccumulationAverage(output[2], output[3], output[4], period, cancellation);
        return latest;
    }

    private static void FillCachedAccumulationAverage(ReadOnlySpan<double> values, Span<double> average,
        Span<double> presence, int period, CancellationToken cancellation)
    {
        // Keep the certified mean route unchanged. Only uncertified captured
        // A/D windows opt into the per-run cached word divisor.
        var summary = new Core.SmaCpuKernel.GridSummary();
        foreach (double value in values)
        {
            cancellation.ThrowIfCancellationRequested();
            // Value-column validation reports overflow first, after all input.
            if (!double.IsFinite(value)) return;
            summary.Include(value);
        }
        if (summary.Certifies(period))
        {
            var reader = new Core.SmaCpuKernel.DoubleReader();
            var consumer = new Core.SmaCpuKernel.Identity();
            Core.SmaCpuKernel.ProcessCertified<double, Core.SmaCpuKernel.DoubleReader, Core.SmaCpuKernel.Identity>(
                values, average, period, ref reader, ref consumer, cancellation);
            for (int i = period - 1; i < values.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                presence[i] = 1;
            }
        }
        else
        {
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(period);
            var mean = new ExactMeanAccumulator();
            for (int i = 0; i < values.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (i >= period) mean.Add(values[i - period], -1);
                mean.Add(values[i]);
                if (i < period - 1) continue;
                average[i] = mean.MeanCachedWord(in divisor);
                presence[i] = 1;
            }
        }
    }

    private static bool TryBoundedAccumulationDifferences(double high, double low, double close,
        out double balance, out double range)
    {
        // Sterbenz makes both legs and the range exact. On g=ULP(low), all
        // prices are integers; |balance| <= range <= low < 2^53*g, so the
        // subtraction of the legs is exact too. Exclude subnormal prices.
        if (high >= 4.450147717014403E-308 && double.IsFinite(high)
            && low >= high * .5 && low <= close && close <= high)
        {
            balance = (close - low) - (high - close);
            range = high - low;
            return true;
        }
        balance = range = 0;
        return false;
    }

    private static double AccumulationFlow(double high, double low, double close, double volume,
        out double multiplier)
    {
        if (high == low) // NOSONAR: S1244 - Only an exactly zero range has zero flow.
        {
            multiplier = 0;
            return 0;
        }
        if (TryBoundedAccumulationDifferences(high, low, close, out double balance, out double range)
            || (Binary64ArithmeticCertificate.TryDifference(close, low, out double buying)
                && Binary64ArithmeticCertificate.TryDifference(high, close, out double selling)
                && Binary64ArithmeticCertificate.TryDifference(buying, selling, out balance)
                && Binary64ArithmeticCertificate.TryDifference(high, low, out range)))
        {
            // Flow rounds the complete product/ratio independently of multiplier.
            multiplier = balance == 0 ? 0 : balance / range;
            if (balance == 0 || volume == 0) return 0;
            if (Binary64ArithmeticCertificate.TryProduct(balance, volume, out double product))
            {
                double flow = product / range;
                if (double.IsFinite(flow)) return new RocBankValue(flow).Publish();
            }
        }
        else
        {
            var numerator = new ExactMeanAccumulator();
            numerator.Add(close, 2); numerator.Add(high, -1); numerator.Add(low, -1);
            var denominator = new ExactMeanAccumulator();
            denominator.Add(high); denominator.Add(low, -1);
            multiplier = numerator.Ratio(denominator);
        }
        return MoneyFlowAccumulationWindow.Flow(high, low, close, volume).Publish();
    }
}
#endif
