#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

internal static class LaguerreRoundedKernel
{
    private static double SumError(double a, double b, double sum)
    {
        double virtualB = sum - a;
        return (a - (sum - virtualB)) + (b - virtualB);
    }

    internal static bool TryUpdate(ReadOnlySpan<JurikDyadic> previous, double price, double gamma,
        Span<double> next, out double output)
    {
        output = 0;
        double prior = price, current = price;
        for (int stage = 0; stage < 4; stage++)
        {
            if (!previous[stage].TryDouble(out double old)
                || !Binary64ArithmeticCertificate.TryDifference(old, current, out double difference)) return false;
            current = Math.FusedMultiplyAdd(difference, gamma, prior);
            if (!double.IsFinite(current)) return false;
            next[stage] = current;
            prior = old;
        }
        return TryMean(next, out output);
    }

    internal static bool TryMean(ReadOnlySpan<double> stages, out double output)
    {
        output = 0;
        const double minimum = 3.4395525670743494E-136; // 2^-450
        const double maximum = 2.9073548971824276E+135; // 2^450
        foreach (double value in stages)
        {
            double magnitude = Math.Abs(value);
            if (magnitude > maximum || magnitude != 0 && magnitude < minimum) return false;
        }
        // Exact integer scaling and error-free additions retain the weighted
        // numerator as sum + e1 + e2 + e3, without constructing wide structs.
        double b = stages[1] * 2, c = stages[2] * 2;
        double s1 = stages[0] + b, e1 = SumError(stages[0], b, s1);
        double s2 = s1 + c, e2 = SumError(s1, c, s2);
        double sum = s2 + stages[3], e3 = SumError(s2, stages[3], sum);
        double quotient = sum / 6;
        if (e1 == 0 && e2 == 0 && e3 == 0)
        {
            output = quotient == 0 ? 0 : quotient;
            return true;
        }
        if (Math.Abs(quotient) < minimum) return false;
        double tailLower = Math.BitDecrement(Math.BitDecrement(e1 + e2) + e3);
        double tailUpper = Math.BitIncrement(Math.BitIncrement(e1 + e2) + e3);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            // The division residual is exactly representable for the rounded
            // quotient or an adjacent double: integer 6 adds only two low bits,
            // which survive cancellation in this bounded exponent range.
            double residual = Math.FusedMultiplyAdd(-quotient, 6, sum);
            double lower = Math.BitDecrement(tailLower + residual);
            double upper = Math.BitIncrement(tailUpper + residual);
            // Compare numerator residuals to six times the half-ULP cell.
            // Strict inequalities reject ties and uncertain cases.
            if (lower > -3 * (quotient - Math.BitDecrement(quotient))
                && upper < 3 * (Math.BitIncrement(quotient) - quotient))
            {
                output = quotient;
                return true;
            }
            // The omitted exact tail can move the quotient one ULP. Test the
            // neighbour only when the whole residual interval has that sign.
            if (lower > 0) quotient = Math.BitIncrement(quotient);
            else if (upper < 0) quotient = Math.BitDecrement(quotient);
            else return false;
        }
        return false;
    }
}
#endif
