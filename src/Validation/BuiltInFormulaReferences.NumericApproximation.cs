using System.Numerics;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    // Reference-only approximations retain their original normalization,
    // refinement and rounding; production uses separate algorithms.
    private static ReferenceFraction CompactReferenceFraction(ReferenceFraction value)
    {
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        if (value.Sign == 0) return R(0);
        // Bound only recursive memory, using four independent binary64
        // residuals (about 212 bits), beyond production's 106-bit state.
        // Normalize first to preserve both exponent extremes. Finite-window
        // ratios, products, means and the current result remain rational.
        var factor = new ReferenceFraction(BigInteger.One << 512); var scale = R(1);
        var magnitude = Math.Abs(value.ToDouble());
        while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { value /= factor; scale *= factor; magnitude = Math.Abs(value.ToDouble()); }
        while (magnitude < Math.Pow(2, -256)) { value *= factor; scale /= factor; magnitude = Math.Abs(value.ToDouble()); }
        var total = R(0);
        for (var part = 0; part < 4; part++) { var component = R(value.ToDouble()); total += component; value -= component; }
        return total * scale;
    }

    private static ReferenceFraction RefinedReferenceRoot(ReferenceFraction square)
    {
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        if (square.Sign == 0) return R(0);
        // Normalize the validation fraction, then correct a binary64 root
        // with its exact rational residual. Independent of the production
        // integer-root and 106-bit rounding algorithm.
        var factor = new ReferenceFraction(BigInteger.One << 512); var scale = R(1); var estimate = square.ToDouble();
        while (double.IsInfinity(estimate) || estimate >= Math.Pow(2, 512)) { square /= factor * factor; scale *= factor; estimate = square.ToDouble(); }
        while (estimate < Math.Pow(2, -512)) { square *= factor * factor; scale /= factor; estimate = square.ToDouble(); }
        var high = Math.Sqrt(estimate);
        var seed = R(high); var correction = (square - seed * seed) / (R(2) * seed);
        var improved = seed + correction;
        var refined = (improved + square / improved) / R(2);
        var low = (refined - seed).ToDouble();
        return (seed + R(low)) * scale;
    }

    private static ReferenceFraction DeviationReferenceRoot(ReferenceFraction variance)
    {
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        var one = R(1); var zero = R(0);
        if (variance.Sign == 0) return zero; var scale = one;
        var squareChunk = chunk * chunk; var highSquare = upper * upper; var lowSquare = lower * lower;
        while (variance.CompareTo(lowSquare) < 0) { variance *= squareChunk; scale /= chunk; }
        while (variance.CompareTo(highSquare) >= 0) { variance /= squareChunk; scale *= chunk; }
        return R(variance.SqrtToDouble()) * scale;
    }

    private static ReferenceFraction DeviationReferenceRound(ReferenceFraction value)
    {
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
        while (value.Abs().CompareTo(lower) < 0) { value *= chunk; scale /= chunk; }
        while (value.Abs().CompareTo(upper) >= 0) { value /= chunk; scale *= chunk; }
        return R(value.ToDouble()) * scale;
    }
}
