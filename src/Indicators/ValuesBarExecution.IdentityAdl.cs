#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillIdentityAdl(Bar[] source, Node node,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var line = new MoneyFlowAccumulationWindow.IdentityBatchState();
        double[][] output = node.Values;
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            latest = source[i];
            if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
            if (owned is not null) owned[i] = latest;
            // Like Node.Append, defer arithmetic failures until all remaining
            // raw input has been checked. Output validation remains slot-major.
            if (node.Failure is not null) continue;
            try
            {
                double value = line.Next(latest.High, latest.Low, latest.Close, latest.Volume);
                output[0][i] = value;
                output[1][i] = value;
            }
            catch (Exception error) { node.CaptureFailure(error); }
        }
        return latest;
    }
}
#endif
