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
            FillPublishedAverage(output[2], output[3], output[4], period, cancellation);
        return latest;
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
