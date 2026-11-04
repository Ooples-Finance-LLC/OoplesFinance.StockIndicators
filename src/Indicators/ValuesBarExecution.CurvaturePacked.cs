#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static void FillPackedCurvatureStatistics(double[] close, double[][] output,
        int start, int end, int period, int grid, CancellationToken cancellation)
    {
        int origin = Math.Max(0, start - period + 1);
        Int128 sum = 0, weighted = 0;
        BigInteger squares = default;
        // |input|<2^83 and P<=4096 give sum<2^95, weighted<2^106,
        // covariance<2^108 and fitted numerators<2^124. Only squared
        // moments need wider coefficients; all rounding stays on the final grid.
        for (int i = origin; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) return;
            if (i - origin >= period)
            {
                Int128 expired = (Int128)close[i - period];
                sum -= expired;
                weighted -= sum;
                var expiredInteger = (BigInteger)expired;
                squares -= expiredInteger * expiredInteger;
            }
            int count = Math.Min(period, i - origin + 1);
            Int128 value = (Int128)close[i];
            weighted += (count - 1L) * value;
            sum += value;
            var addedInteger = (BigInteger)value;
            squares += addedInteger * addedInteger;
            if (i < start) continue;
            output[5][i] = 1;
            if (count < 2) continue;
            Int128 covariance = 2 * weighted - (count - 1L) * sum;
            long spread = (long)count * count - 1, denominator = count * spread;
            Int128 basis = sum * spread, threeCovariance = 3 * covariance;
            output[0][i] = RegressionGridRatio(2 * threeCovariance, denominator, grid);
            output[1][i] = RegressionGridRatio(basis - threeCovariance * (count + 1L), denominator, grid);
            output[4][i] = RegressionGridRatio(basis + threeCovariance * (count - 1L), denominator, grid);
            var sumInteger = (BigInteger)sum;
            var variance = count * squares - sumInteger * sumInteger;
            output[2][i] = variance.IsZero ? 0 : ExactPopulationDeviation.ScaledRootRatio(
                variance, (long)count * count, 2 * grid);
            output[6][i] = output[7][i] = output[9][i] = 1;
            if (variance.IsZero) continue;
            output[3][i] = CurvatureCorrelation(covariance, 0, variance, spread);
            output[8][i] = 1;
        }
    }
}
#endif
