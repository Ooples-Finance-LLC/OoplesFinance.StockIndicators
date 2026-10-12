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
        return latest;
    }
}
#endif
