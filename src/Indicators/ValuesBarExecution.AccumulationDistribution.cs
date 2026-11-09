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
        else if (source.Length == 0 || !TryFillAccumulationGrid(output[1].AsSpan(0, source.Length),
            output[2].AsSpan(0, source.Length), cancellation))
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
            FillPublishedAverage(output[2], output[3], output[4], period, cancellation);
        return latest;
    }

    private static bool TryFillAccumulationGrid(ReadOnlySpan<double> flows, Span<double> output,
        CancellationToken cancellation)
    {
        int minimumScale = int.MaxValue, maximumScale = 0;
        for (int i = 0; i < flows.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (flows[i] == 0) continue;
            long bits = BitConverter.DoubleToInt64Bits(flows[i]);
            int scale = Math.Max(0, (int)((bits >> 52) & 2047) - 1);
            minimumScale = Math.Min(minimumScale, scale);
            maximumScale = Math.Max(maximumScale, scale);
        }
        if (minimumScale == int.MaxValue) return false;
        int countBits = flows.Length <= 1 ? 0
            : 32 - System.Numerics.BitOperations.LeadingZeroCount((uint)(flows.Length - 1));
        // Every captured finite flow is an integer with at most 53 bits on its
        // raw binary grid. This bounds the sum of absolute coefficients, so all
        // prefixes and shifts fit well inside signed Int128 without per-add
        // alignment/overflow checks. Wider spans retain the original fallback.
        if (maximumScale - minimumScale + 53 + countBits > 106) return false;
        Int128 total = 0;
        for (int i = 0; i < flows.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long bits = BitConverter.DoubleToInt64Bits(flows[i]);
            int exponent = (int)((bits >> 52) & 2047);
            long magnitude = bits & ((1L << 52) - 1);
            if (exponent != 0) magnitude |= 1L << 52;
            if (magnitude != 0)
            {
                int scale = Math.Max(0, exponent - 1);
                Int128 term = bits < 0 ? -magnitude : magnitude;
                total = unchecked(total + (term << (scale - minimumScale)));
            }
            // Keep the exact integer total even when a published prefix overflows;
            // generic slot-major validation still rejects the earlier output.
            output[i] = JurikDyadic.FromBinaryProduct(total, minimumScale - 1074).Mean(1);
        }
        return true;
    }

    private static double AccumulationFlow(double high, double low, double close, double volume,
        out double multiplier)
    {
        if (high == low) // NOSONAR: S1244 - Only an exactly zero range has zero flow.
        {
            multiplier = 0;
            return 0;
        }
        if (Binary64ArithmeticCertificate.TryDifference(close, low, out double buying)
            && Binary64ArithmeticCertificate.TryDifference(high, close, out double selling)
            && Binary64ArithmeticCertificate.TryDifference(buying, selling, out double balance)
            && Binary64ArithmeticCertificate.TryDifference(high, low, out double range))
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
