#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillOnBalanceVolume(Bar[] source, double[][] output, SeededOnBalanceVolume indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var contributions = output[0];
        var summary = new Core.SmaCpuKernel.GridSummary();
        Bar latest = default;
        double previous = 0;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            latest = source[i];
            if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
            if (owned is not null) owned[i] = latest;
            double contribution = i == 0 ? indicator.Seed == ObvSeed.FirstVolume ? latest.Volume : 0
                : latest.Close > previous ? latest.Volume : latest.Close < previous ? -latest.Volume : 0;
            previous = latest.Close;
            contributions[i] = contribution;
            summary.Include(contribution);
            output[1][i] = 0;
            output[2][i] = 0;
        }
        // The full-count certificate bounds every signed prefix. All input has
        // been validated and captured before replacing contributions with sums.
        // Generic slot-major publication retains overflow validation afterward.
        if (source.Length != 0 && summary.Certifies(source.Length))
        {
            double total = 0;
            for (int i = 0; i < contributions.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                total += contributions[i];
                contributions[i] = total == 0 ? 0 : total;
            }
        }
        else
        {
            var total = new ExactMeanAccumulator();
            for (int i = 0; i < contributions.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                total.Add(contributions[i]);
                contributions[i] = total.Mean(1);
            }
        }
        if (indicator.AveragePeriod is int period && period < contributions.Length)
            FillOnBalanceVolumeAverage(contributions.AsSpan(1), output[1].AsSpan(1), output[2].AsSpan(1), period, cancellation);
        return latest;
    }

    private static void FillOnBalanceVolumeAverage(ReadOnlySpan<double> values, Span<double> average,
        Span<double> presence, int period, CancellationToken cancellation)
    {
        // The average excludes the seed and uses rounded published OBV values.
        // Its independent certificate must bound those values, not input volumes.
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
            var mean = new ExactMeanAccumulator();
            for (int i = 0; i < values.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (i >= period) mean.Add(values[i - period], -1);
                mean.Add(values[i]);
                if (i < period - 1) continue;
                average[i] = mean.Mean(period);
                presence[i] = 1;
            }
        }
    }
}
#endif
