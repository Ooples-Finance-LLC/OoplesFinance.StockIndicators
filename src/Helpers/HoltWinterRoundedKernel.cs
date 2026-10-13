#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

internal static class HoltWinterRoundedKernel
{
    private static double SumError(double a, double b, double sum)
    {
        double virtualB = sum - a;
        return (a - (sum - virtualB)) + (b - virtualB);
    }

    private static bool TrySum(ReadOnlySpan<double> values, ReadOnlySpan<double> factors, out double result)
    {
        const double minimum = 3.4395525670743494E-136; // 2^-450
        const double maximum = 2.9073548971824276E+135; // 2^450
        double sum = 0, lower = 0, upper = 0;
        bool exact = true;
        result = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double product = values[i] * factors[i];
            double magnitude = Math.Abs(product);
            // These bounds keep both the product residual and all TwoSum
            // errors representable; a rounded-away product is never certified.
            if (magnitude == 0 ? values[i] != 0 && factors[i] != 0
                : !double.IsFinite(product) || magnitude < minimum || magnitude > maximum) return false;
            double productError = Math.FusedMultiplyAdd(values[i], factors[i], -product);
            double next = sum + product, error = SumError(sum, product, next);
            exact &= productError == 0 && error == 0;
            lower = Math.BitDecrement(Math.BitDecrement(lower + productError) + error);
            upper = Math.BitIncrement(Math.BitIncrement(upper + productError) + error);
            sum = next;
        }
        if (exact) { result = sum == 0 ? 0 : sum; return true; }
        double correction = lower * .5 + upper * .5;
        double rounded = sum + correction;
        if (Math.Abs(rounded) < minimum || !double.IsFinite(rounded)) return false;
        double roundingError = SumError(sum, correction, rounded);
        double residualLower = Math.BitDecrement(Math.BitDecrement(lower - correction) + roundingError);
        double residualUpper = Math.BitIncrement(Math.BitIncrement(upper - correction) + roundingError);
        if (!(residualLower > -.5 * (rounded - Math.BitDecrement(rounded))
            && residualUpper < .5 * (Math.BitIncrement(rounded) - rounded))) return false;
        result = rounded;
        return true;
    }

    internal static bool TryUpdate(double oldLevel, double oldVelocity, double oldAcceleration,
        double price, double a, double b, double c, out double level, out double velocity,
        out double acceleration, out double output)
    {
        level = velocity = acceleration = output = 0;
        // Halving must retain the exact unpublished term, including subnormals.
        double half = oldAcceleration * .5;
        if (half * 2 != oldAcceleration) return false; // NOSONAR: S1244 - The certificate requires halving to round-trip exactly, including subnormals.
        if (!TrySum([oldLevel, oldVelocity, half, oldLevel, oldVelocity, half, price],
            [1, 1, 1, -a, -a, -a, a], out level)) return false;
        if (!TrySum([oldVelocity, oldAcceleration, oldVelocity, oldAcceleration, level, oldLevel],
            [1, 1, -b, -b, b, -b], out velocity)) return false;
        if (!TrySum([oldAcceleration, oldAcceleration, velocity, oldVelocity],
            [1, -c, c, -c], out acceleration)) return false;
        half = acceleration * .5;
        if (half * 2 != acceleration) return false; // NOSONAR: S1244 - The certificate requires halving to round-trip exactly, including subnormals.
        return TrySum([level, velocity, half], [1, 1, 1], out output);
    }
}
#endif
