#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static void FillWideCurvatureStatistics(double[] close, double[][] output,
        int start, int end, int period, int grid, CancellationToken cancellation)
    {
        int origin = Math.Max(0, start - period + 1);
        Int128 sum = 0, weighted = 0;
        UInt128 squares = 0;
        BigInteger largeSquares = default;
        // |input|<2^63 and P<=4096 bound sum<2^75, weighted<2^87,
        // covariance<2^89 and fitted numerators<2^104. Squared moments may
        // exceed128 bits; only those moments promote, and demote when possible.
        for (int i = origin; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) return;
            if (i - origin >= period)
            {
                Int128 expired = (long)close[i - period];
                sum -= expired;
                weighted -= sum;
                UInt128 square = (UInt128)(expired * expired);
                if (largeSquares.IsZero) squares -= square;
                else
                {
                    largeSquares -= (BigInteger)square;
                    if (largeSquares <= (BigInteger)UInt128.MaxValue)
                    { squares = (UInt128)largeSquares; largeSquares = default; }
                }
            }
            int count = Math.Min(period, i - origin + 1);
            Int128 value = (long)close[i];
            weighted += (count - 1L) * value;
            sum += value;
            UInt128 added = (UInt128)(value * value);
            if (!largeSquares.IsZero) largeSquares += (BigInteger)added;
            else if (squares <= UInt128.MaxValue - added) squares += added;
            else largeSquares = (BigInteger)squares + (BigInteger)added;
            if (i < start) continue;
            output[5][i] = 1;
            if (count < 2) continue;
            Int128 covariance = 2 * weighted - (count - 1L) * sum;
            long spread = (long)count * count - 1, denominator = count * spread;
            Int128 basis = sum * spread, threeCovariance = 3 * covariance;
            output[0][i] = RegressionGridRatio(2 * threeCovariance, denominator, grid);
            output[1][i] = RegressionGridRatio(basis - threeCovariance * (count + 1L), denominator, grid);
            output[4][i] = RegressionGridRatio(basis + threeCovariance * (count - 1L), denominator, grid);

            UInt128 magnitude = (UInt128)(sum < 0 ? -sum : sum);
            UInt128 variance = 0;
            BigInteger largeVariance = default;
            if (largeSquares.IsZero && squares <= UInt128.MaxValue / (uint)count && magnitude <= ulong.MaxValue)
                variance = (uint)count * squares - magnitude * magnitude;
            else
            {
                largeVariance = count * (largeSquares.IsZero ? (BigInteger)squares : largeSquares)
                    - (BigInteger)sum * (BigInteger)sum;
                if (largeVariance <= (BigInteger)UInt128.MaxValue)
                { variance = (UInt128)largeVariance; largeVariance = default; }
            }
            long countSquared = (long)count * count;
            if (!largeVariance.IsZero)
                output[2][i] = ExactPopulationDeviation.ScaledRootRatio(largeVariance, countSquared, 2 * grid);
            else if (ExactPopulationDeviation.TryCompactScaledRoot(variance, (ulong)countSquared, 2 * grid, out double deviation))
                output[2][i] = deviation;
            else
                output[2][i] = ExactPopulationDeviation.ScaledRootRatio((BigInteger)variance, countSquared, 2 * grid);
            output[6][i] = output[7][i] = output[9][i] = 1;
            if (variance == 0 && largeVariance.IsZero) continue;
            output[3][i] = CurvatureCorrelation(covariance, variance, largeVariance, spread);
            output[8][i] = 1;
        }
    }

    private static double CurvatureCorrelation(Int128 covariance, UInt128 variance, BigInteger largeVariance, long spread)
    {
        if (covariance == 0) return 0;
        if (largeVariance.IsZero)
        {
            int covarianceZeros = (int)Int128.TrailingZeroCount(covariance);
            int varianceZeros = (int)UInt128.TrailingZeroCount(variance);
            Int128 normalized = covariance >> covarianceZeros;
            UInt128 magnitude = (UInt128)(normalized < 0 ? -normalized : normalized);
            UInt128 normalizedVariance = variance >> varianceZeros;
            if (magnitude <= ulong.MaxValue && magnitude * magnitude <= (UInt128)Int128.MaxValue / 3
                && normalizedVariance <= (UInt128)Int128.MaxValue / (ulong)spread)
                return Math.ScaleB(JurikDyadic.IntegerRatio((Int128)(3 * magnitude * magnitude),
                    (Int128)(normalizedVariance * (ulong)spread)), 2 * covarianceZeros - varianceZeros);
        }
        var value = (BigInteger)covariance;
        var numerator = 3 * value * value;
        var denominator = (largeVariance.IsZero ? (BigInteger)variance : largeVariance) * spread;
        // This positive ratio is at most one by Cauchy-Schwarz. Its integer
        // operands are below2^180 under the input/period bounds, so every
        // nonzero result is normal. Round directly on its final binary64 grid,
        // avoiding arbitrary-grid accumulator wrappers for these known integers.
        int exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        if (exponent >= 0 ? numerator < denominator << exponent : numerator << -exponent < denominator)
            exponent--;
        int grid = exponent - 52;
        var significand = BigInteger.DivRem(numerator << -grid, denominator, out var remainder);
        int comparison = (remainder << 1).CompareTo(denominator);
        if (comparison > 0 || comparison == 0 && !significand.IsEven) significand++;
        return Math.ScaleB((double)significand, grid);
    }
}
#endif
