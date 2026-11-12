#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillIdentityAdl(Bar[] source, Node node,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        double[][] output = node.Values;
        Bar latest = default;
        int lowestScale = int.MaxValue, highestScale = 0;
        bool ordinary = true;
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
                // These two run-owned columns are temporary flow storage until
                // every raw input has been validated. Never reread caller bars.
                var flow = MoneyFlowAccumulationWindow.CaptureIdentityBatchFlow(
                    latest.High, latest.Low, latest.Close, latest.Volume);
                output[0][i] = flow.Mantissa;
                output[1][i] = flow.UpperShift;
                if (flow.UpperShift != 0 || !double.IsFinite(flow.Mantissa)) ordinary = false;
                if (flow.Mantissa != 0)
                {
                    int scale = Math.Max(0, (int)((BitConverter.DoubleToInt64Bits(flow.Mantissa) >> 52) & 2047) - 1);
                    lowestScale = Math.Min(lowestScale, scale);
                    highestScale = Math.Max(highestScale, scale);
                }
            }
            catch (Exception error) { node.CaptureFailure(error); }
        }
        if (node.Failure is not null) return latest;
        // A full significand has at most 53 bits. This conservative bound
        // covers the absolute sum of every prefix, including mixed signs.
        int countBits = 32 - BitOperations.LeadingZeroCount((uint)source.Length);
        if (ordinary && (lowestScale == int.MaxValue || highestScale - lowestScale + 53 + countBits <= 106))
            FillIdentityAdlGrid(output, lowestScale == int.MaxValue ? 0 : lowestScale, cancellation);
        else
            FillCapturedIdentityAdl(node, cancellation);
        return latest;
    }

    private static void FillIdentityAdlGrid(double[][] output, int commonScale, CancellationToken cancellation)
    {
        Int128 total = 0;
        for (int i = 0; i < output[0].Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long bits = BitConverter.DoubleToInt64Bits(output[0][i]);
            int exponent = (int)((bits >> 52) & 2047);
            long coefficient = (bits & ((1L << 52) - 1)) | (exponent == 0 ? 0 : 1L << 52);
            if (coefficient != 0)
            {
                if (bits < 0) coefficient = -coefficient;
                total += (Int128)coefficient << (Math.Max(0, exponent - 1) - commonScale);
            }
            // Reuse the exact publisher already used for compact A/D sums.
            // The grid certificate bounds every coefficient below 2^106.
            double value = JurikDyadic.FromBinaryProduct(total, commonScale - 1074).Mean(1);
            output[0][i] = output[1][i] = value;
        }
    }

    private static void FillCapturedIdentityAdl(Node node, CancellationToken cancellation)
    {
        var line = new MoneyFlowAccumulationWindow.IdentityBatchState();
        double[][] output = node.Values;
        for (int i = 0; i < output[0].Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var flow = new RocBankValue(output[0][i], (int)output[1][i]);
                double value = line.AddCaptured(flow);
                output[0][i] = output[1][i] = value;
            }
            catch (Exception error) { node.CaptureFailure(error); break; }
        }
    }
}
#endif
