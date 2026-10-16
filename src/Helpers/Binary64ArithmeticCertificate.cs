#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Framework JITs may retain extended intermediates; callers there keep their
// accumulator arithmetic instead of assuming binary64 evaluation.
internal static class Binary64ArithmeticCertificate
{
    internal static bool TryDifference(double left, double right, out double difference)
    {
        difference = left - right;
        if (!double.IsFinite(difference)) return false;
        // Knuth's error-free subtraction transform, including gradual underflow.
        var virtualRight = left - difference;
        var virtualLeft = difference + virtualRight;
        var rightError = virtualRight - right;
        var leftError = left - virtualLeft;
        return leftError + rightError == 0; // NOSONAR: S1244 - An exactly zero residual certifies the difference.
    }

    internal static bool TryProduct(double left, double right, out double product)
    {
        product = left * right;
        // A binary64 product has at most 106 significand bits. At/above 2^-968,
        // even its lowest possible nonzero residual is representable. Below
        // this conservative bound FMA could round a nonzero residual to zero.
        const double minimumCertifiedProduct = 4.008336720017946e-292; // 2^-968
        return Math.Abs(product) >= minimumCertifiedProduct && double.IsFinite(product)
            && Math.FusedMultiplyAdd(left, right, -product) == 0; // NOSONAR: S1244 - An exactly zero residual certifies the product.
    }
}
#endif
