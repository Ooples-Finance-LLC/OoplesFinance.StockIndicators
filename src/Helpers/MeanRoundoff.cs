namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Outward bounds for a running sum of finite binary64 observations.</summary>
internal static class MeanRoundoff
{
    // u/(1-u), rounded upward, where u=2^-53. Error is measured against exact input doubles.
    private const double AdditionFactor = 1.1102230246251568e-16;
    private const double MinimumNormal = 2.2250738585072014e-308;

    internal static double AfterAddition(double previousBound, double roundedSum)
    {
        if (double.IsNaN(roundedSum) || double.IsInfinity(roundedSum)) return double.PositiveInfinity;
        // An addition/subtraction of binary64 inputs that rounds to zero is exact on their dyadic grid.
        if (roundedSum == 0) return previousBound;
        return Up(previousBound + Up(Math.Abs(roundedSum) * AdditionFactor));
    }

    internal static bool RequiresExact(double sum, int count, double bound)
    {
        if (double.IsNaN(sum) || double.IsInfinity(sum) || double.IsInfinity(bound)) return true;
        if (sum == 0) return bound != 0;
        // Reserve a factor-ten margin below the declared 1e-9 relative comparison budget,
        // including the final normal division. Subnormal means are rounded by the exact path.
        return Math.Abs(sum / count) < MinimumNormal || bound > 1e-10 * Math.Abs(sum);
    }

    private static double Up(double nonnegative)
        => double.IsInfinity(nonnegative) ? nonnegative
            : BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(nonnegative) + 1);
}
