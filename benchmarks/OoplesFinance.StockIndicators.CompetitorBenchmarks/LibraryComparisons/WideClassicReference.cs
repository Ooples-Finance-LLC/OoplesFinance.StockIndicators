using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WideClassicReference
{
    internal static BigInteger?[] Values(
        BigInteger[] input,
        int period,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        if (period == 1)
            return input.Select(v => (BigInteger?)v).ToArray();
        if (
            method
            is ClassicAverageMethod.Ema
                or ClassicAverageMethod.Dema
                or ClassicAverageMethod.Tema
        )
            return ClassicAverageComparison.ExponentialUnits(
                input,
                period,
                method,
                first,
                suppression
            );
        if (method == ClassicAverageMethod.T3)
            return TillsonComparison.ExtendedReference(
                input,
                period,
                .7,
                TillsonSeed.CascadedMeans,
                suppression
            );
        if (method == ClassicAverageMethod.Mama)
            return SeededPhaseComparison.GridExtendedReference(
                input,
                .5,
                .05,
                false,
                true,
                suppression
            )[0];
        if (method == ClassicAverageMethod.Kama)
            return Adaptive(input, period, suppression);
        var result = new BigInteger?[input.Length];
        for (var i = period - 1; i < input.Length; i++)
        {
            BigInteger numerator = 0;
            long mass = 0;
            for (var lag = 0; lag < period; lag++)
            {
                var weight =
                    method == ClassicAverageMethod.Wma ? period - lag
                    : method == ClassicAverageMethod.Trima ? Math.Min(lag + 1, period - lag)
                    : 1;
                numerator += input[i - lag] * weight;
                mass += weight;
            }
            result[i] = DirectionalComparison.RoundedUnits(numerator, mass);
        }
        return result;
    }

    private static BigInteger?[] Adaptive(BigInteger[] input, int period, int suppression)
    {
        var fast = DirectionalComparison.RoundedUnits(2 * Grid, 3);
        var slow = DirectionalComparison.RoundedUnits(2 * Grid, 31);
        var result = new BigInteger?[input.Length];
        var value = BigInteger.Zero;
        for (var i = 0; i < input.Length; i++)
        {
            if (i < period)
                value = input[i];
            else
            {
                var path = Enumerable
                    .Range(i - period + 1, period)
                    .Aggregate(
                        BigInteger.Zero,
                        (sum, j) => sum + BigInteger.Abs(input[j] - input[j - 1])
                    );
                var efficiency = path.IsZero
                    ? Grid
                    : DirectionalComparison.RoundedUnits(
                        BigInteger.Abs(input[i] - input[i - period]) * Grid,
                        path
                    );
                var gain = DirectionalComparison.RoundedUnits(
                    slow * Grid + (fast - slow) * efficiency,
                    Grid
                );
                var alpha = DirectionalComparison.RoundedUnits(gain * gain, Grid);
                value = DirectionalComparison.RoundedUnits(
                    value * Grid + alpha * (input[i] - value),
                    Grid
                );
            }
            if (i >= (long)period + suppression)
                result[i] = value;
        }
        return result;
    }
}
