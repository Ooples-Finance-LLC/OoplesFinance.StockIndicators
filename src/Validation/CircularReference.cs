using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

// Validation only. Machin's identity at 2560 bits keeps the phase error far
// below a subnormal ulp even after reduction of the largest binary64 argument.
// Forward series at 320 bits evaluate reduced arguments; tiny residuals retain
// the full reduction precision so signs and subnormals cannot be erased.
internal static class CircularReference
{
    private const int Precision = 2560,
        Evaluation = 320;
    private static readonly BigInteger Scale = BigInteger.One << Precision;
    private static readonly BigInteger Pi = 16 * AtanReciprocal(5) - 4 * AtanReciprocal(239);

    private static BigInteger AtanReciprocal(int denominator)
    {
        var power = Scale / denominator;
        BigInteger sum = 0;
        for (var n = 0; !power.IsZero; n++)
        {
            sum += (n % 2 == 0 ? power : -power) / (2 * n + 1);
            power /= denominator * denominator;
        }
        return sum;
    }

    internal static double Value(double value, PriceCircularOperation operation)
    {
        var (numerator, denominator) = ReferenceFraction.FromDouble(value).Components;
        var x = numerator * Scale / denominator;
        if (
            operation
            is PriceCircularOperation.Sine
                or PriceCircularOperation.Cosine
                or PriceCircularOperation.Tangent
        )
        {
            var (sin, cos) = SinCos(x);
            return operation == PriceCircularOperation.Tangent
                ? ReferenceFraction.RatioToDouble(sin, cos)
                : ReferenceFraction.RatioToDouble(
                    operation == PriceCircularOperation.Sine ? sin : cos,
                    Scale
                );
        }
        if (operation == PriceCircularOperation.ArcTangent)
            return ReferenceFraction.RatioToDouble(Atan(x), Scale);
        if (BigInteger.Abs(x) > Scale)
            throw new ArgumentOutOfRangeException(nameof(value));
        var root = Sqrt(Scale * Scale - x * x);
        var asin = root.IsZero ? x.Sign * (Pi / 2) : Atan(x * Scale / root);
        return ReferenceFraction.RatioToDouble(
            operation == PriceCircularOperation.ArcSine ? asin : Pi / 2 - asin,
            Scale
        );
    }

    private static (BigInteger Sin, BigInteger Cos) SinCos(BigInteger x)
    {
        var sign = x.Sign;
        x = BigInteger.Abs(x);
        var halfPi = Pi / 2;
        var quadrant = (x + halfPi / 2) / halfPi;
        var residual = x - quadrant * halfPi;
        BigInteger sin,
            cos;
        if (BigInteger.Abs(residual) < (Scale >> 28))
        {
            sin = residual;
            cos = Scale;
        }
        else
        {
            var unit = BigInteger.One << Evaluation;
            var z = residual / (BigInteger.One << (Precision - Evaluation));
            var square = z * z;
            var sineTerm = z;
            var cosineTerm = unit;
            sin = z;
            cos = unit;
            for (var n = 1; n <= 80; n++)
            {
                sineTerm = -sineTerm * square / (unit * unit * (2 * n) * (2 * n + 1));
                cosineTerm = -cosineTerm * square / (unit * unit * (2 * n - 1) * (2 * n));
                sin += sineTerm;
                cos += cosineTerm;
            }
            sin <<= Precision - Evaluation;
            cos <<= Precision - Evaluation;
        }
        (sin, cos) = (int)(quadrant % 4) switch
        {
            0 => (sin, cos),
            1 => (cos, -sin),
            2 => (-sin, -cos),
            _ => (-cos, sin),
        };
        return (sign < 0 ? -sin : sin, cos);
    }

    private static BigInteger Atan(BigInteger x)
    {
        if (x.Sign < 0)
            return -Atan(-x);
        if (x > Scale)
            return Pi / 2 - Atan(Scale * Scale / x);
        if (x < (Scale >> 28))
            return x;
        // Two half-angle reductions bound the series argument by tan(pi/16).
        for (var i = 0; i < 2; i++)
            x = x * Scale / (Scale + Sqrt(Scale * Scale + x * x));
        var unit = BigInteger.One << Evaluation;
        var z = x / (BigInteger.One << (Precision - Evaluation));
        var square = z * z;
        var power = z;
        var sum = z;
        for (var n = 1; n <= 160; n++)
        {
            power = -power * square / (unit * unit);
            sum += power / (2 * n + 1);
        }
        return (4 * sum) << (Precision - Evaluation);
    }

    private static BigInteger Sqrt(BigInteger value)
    {
        if (value.IsZero)
            return 0;
        var root = BigInteger.One << (int)((value.GetBitLength() + 1) / 2);
        while (true)
        {
            var next = (root + value / root) / 2;
            if (next >= root)
                return root;
            root = next;
        }
    }
}
