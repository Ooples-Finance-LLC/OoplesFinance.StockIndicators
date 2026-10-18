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
            output[0][i] = AccumulationMultiplier(high, low, close);
            double flow = MoneyFlowAccumulationWindow.Flow(high, low, close, volume).Publish();
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
            var total = new ExactMeanAccumulator();
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                total.Add(output[1][i]);
                output[2][i] = total.Mean(1);
            }
        }
        if (indicator.AveragePeriod is int period && period <= source.Length)
            FillPublishedAverage(output[2], output[3], output[4], period, cancellation);
        return latest;
    }

    private static double AccumulationMultiplier(double high, double low, double close)
    {
        if (high == low) return 0; // NOSONAR: S1244 - Only an exactly zero range has zero flow.
        if (Binary64ArithmeticCertificate.TryDifference(close, low, out double buying)
            && Binary64ArithmeticCertificate.TryDifference(high, close, out double selling)
            && Binary64ArithmeticCertificate.TryDifference(buying, selling, out double balance)
            && Binary64ArithmeticCertificate.TryDifference(high, low, out double range))
            return balance == 0 ? 0 : balance / range;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(close, 2); numerator.Add(high, -1); numerator.Add(low, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high); denominator.Add(low, -1);
        return numerator.Ratio(denominator);
    }
}
#endif
