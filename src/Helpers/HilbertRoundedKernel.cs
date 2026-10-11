#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Local certificates for Hilbert's exact-sum-then-round boundaries. Failure
// means use the dyadic path, never accept an approximately rounded result.
internal static class HilbertRoundedKernel
{
    private const double MinimumProduct = 3.4395525670743494E-136; // 2^-450
    private const double MaximumProduct = 2.9073548971824276E+135; // 2^450

    private static double SumError(double a, double b, double sum)
    {
        double virtualB = sum - a;
        return (a - (sum - virtualB)) + (b - virtualB);
    }

    internal static bool TryExactSum(double a, double b, out double sum)
    {
        sum = a + b;
        // Bounds also prevent overflow in TwoSum's intermediate subtractions.
        return Math.Abs(a) <= MaximumProduct && Math.Abs(b) <= MaximumProduct
            && SumError(a, b, sum) == 0;
    }

    private static bool SafeProduct(double a, double b, double product)
    {
        double magnitude = Math.Abs(product);
        return magnitude == 0 ? a == 0 || b == 0
            : magnitude >= MinimumProduct && magnitude <= MaximumProduct;
    }

    internal static bool TryProductSum(double a, double b, double c, double d, out double result)
    {
        result = 0;
        double p = a * b, q = c * d;
        if (!SafeProduct(a, b, p) || !SafeProduct(c, d, q)) return false;

        // A product has at most 106 significant bits. The exponent bounds
        // keep its exact FMA residual, and all TwoSum residuals below, normal
        // (or zero). Thus no unobserved underflow can certify an inexact tail.
        double ep = Math.FusedMultiplyAdd(a, b, -p);
        double eq = Math.FusedMultiplyAdd(c, d, -q);
        double sum = p + q;
        double error = SumError(p, q, sum);
        if (!TryExactSum(ep, eq, out double tail)
            || !TryExactSum(tail, error, out tail)) return false;

        // Exact target = sum + tail. This is its only rounding, including ties.
        result = sum + tail;
        if (result == 0) result = 0; // Dyadic zero is canonical positive zero.
        return true;
    }

    internal static bool TrySmooth(double price, double a, double b, double c, out double result)
    {
        result = 0;
        double p = price * 4, q = a * 3, r = b * 2;
        // Integer scaling cannot lose a bit below the binary64 subnormal grid.
        if (!double.IsFinite(q) || Math.FusedMultiplyAdd(a, 3, -q) != 0
            || !TryExactSum(p, q, out double sum)
            || !TryExactSum(sum, r, out sum)
            || !TryExactSum(sum, c, out sum)) return false;
        result = sum / 10;
        if (result == 0) result = 0;
        return true;
    }
}
#endif
