using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

// Validation only: forward fixed-point series, independent of runtime intrinsics.
internal static class TranscendentalReference
{
    internal static double Value(double x, PriceTranscendentalOperation operation)
    {
        var fraction = ReferenceFraction.FromDouble(x);
        if (operation == PriceTranscendentalOperation.NaturalLogarithm)
            return fraction.LogToDouble();
        if (operation == PriceTranscendentalOperation.CommonLogarithm)
            return fraction.LogToDouble() / new ReferenceFraction(10).LogToDouble();
        if (operation == PriceTranscendentalOperation.HyperbolicTangent)
            return fraction.TanhToDouble();
        var magnitude = Math.Abs(x);
        if (
            operation == PriceTranscendentalOperation.HyperbolicSine
            && magnitude < (1d / 134217728d)
        )
            return x;
        if (magnitude > 800)
            return operation == PriceTranscendentalOperation.Exponential && x < 0 ? 0
                : operation == PriceTranscendentalOperation.HyperbolicSine && x < 0
                    ? double.NegativeInfinity
                : double.PositiveInfinity;
        var (numerator, denominator) = fraction.Abs().Components;
        var scale = BigInteger.One << 192;
        var argument = numerator * scale / (denominator * 1024);
        var term = scale;
        var exponential = scale;
        // |argument| < .782; 100 terms and ten squarings leave ample margin
        // beyond binary64, including the reciprocal used for subnormal exp(x).
        for (var n = 1; n <= 100; n++)
        {
            term = term * argument / (scale * n);
            exponential += term;
        }
        for (var n = 0; n < 10; n++)
            exponential = exponential * exponential / scale;
        if (operation == PriceTranscendentalOperation.Exponential)
            return x < 0
                ? ReferenceFraction.RatioToDouble(scale, exponential)
                : ReferenceFraction.RatioToDouble(exponential, scale);
        var square = exponential * exponential;
        var unit = scale * scale;
        return operation == PriceTranscendentalOperation.HyperbolicCosine
            ? ReferenceFraction.RatioToDouble(square + unit, 2 * exponential * scale)
            : ReferenceFraction.RatioToDouble(
                Math.Sign(x) * (square - unit),
                2 * exponential * scale
            );
    }
}
