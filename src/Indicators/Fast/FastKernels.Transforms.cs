namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>Ceiling, floor or square root of the close. A negative close has no square root.</summary>
    internal static void Rounding(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        PriceRoundingOperation operation)
    {
        value = value.Slice(0, close.Length);
        defined = defined.Slice(0, close.Length);
        switch (operation)
        {
            case PriceRoundingOperation.Ceiling:
                FastMath.Ceiling(close, value);
                defined.Fill(1);
                break;
            case PriceRoundingOperation.Floor:
                FastMath.Floor(close, value);
                defined.Fill(1);
                break;
            default:
                FastMath.Sqrt(close, value);
                MaskBelow(close, value, defined, 0, inclusive: false);
                break;
        }
    }

    /// <summary>A circular function of the close. The inverse sine and cosine exist only on [-1, 1].</summary>
    internal static void Circular(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        PriceCircularOperation operation)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        switch (operation)
        {
#if NETFRAMEWORK
            // .NET Framework's own circular functions lose accuracy on large arguments, so the exact state
            // uses this replacement there, and so does this kernel.
            case PriceCircularOperation.Sine:
                for (var i = 0; i < n; i++) value[i] = Helpers.FrameworkCircularMath.Value(close[i], 0);
                break;
            case PriceCircularOperation.Cosine:
                for (var i = 0; i < n; i++) value[i] = Helpers.FrameworkCircularMath.Value(close[i], 1);
                break;
            case PriceCircularOperation.Tangent:
                for (var i = 0; i < n; i++) value[i] = Helpers.FrameworkCircularMath.Value(close[i], 2);
                break;
#else
            case PriceCircularOperation.Sine:
                for (var i = 0; i < n; i++) value[i] = Math.Sin(close[i]);
                break;
            case PriceCircularOperation.Cosine:
                for (var i = 0; i < n; i++) value[i] = Math.Cos(close[i]);
                break;
            case PriceCircularOperation.Tangent:
                for (var i = 0; i < n; i++) value[i] = Math.Tan(close[i]);
                break;
#endif
            case PriceCircularOperation.ArcTangent:
                for (var i = 0; i < n; i++) value[i] = Math.Atan(close[i]);
                break;
            default:
                var sine = operation == PriceCircularOperation.ArcSine;
                for (var i = 0; i < n; i++)
                {
                    var x = close[i];
                    var inside = x >= -1 && x <= 1;
                    value[i] = !inside ? 0 : sine ? Math.Asin(x) : Math.Acos(x);
                    defined[i] = inside ? 1 : 0;
                }
                return;
        }
        defined.Fill(1);
    }

    /// <summary>A logarithm, exponential or hyperbolic function of the close. Logarithms need a positive close.</summary>
    internal static void Transcendental(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        PriceTranscendentalOperation operation)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        switch (operation)
        {
            case PriceTranscendentalOperation.NaturalLogarithm:
            case PriceTranscendentalOperation.CommonLogarithm:
                var common = operation == PriceTranscendentalOperation.CommonLogarithm;
                for (var i = 0; i < n; i++)
                {
                    var x = close[i];
                    var positive = x > 0;
                    value[i] = !positive ? 0 : common ? Math.Log10(x) : Math.Log(x);
                    defined[i] = positive ? 1 : 0;
                }
                return;
            case PriceTranscendentalOperation.Exponential:
                for (var i = 0; i < n; i++) value[i] = Math.Exp(close[i]);
                break;
            case PriceTranscendentalOperation.HyperbolicSine:
                for (var i = 0; i < n; i++) value[i] = Math.Sinh(close[i]);
                break;
            case PriceTranscendentalOperation.HyperbolicCosine:
                for (var i = 0; i < n; i++) value[i] = Math.Cosh(close[i]);
                break;
            default:
                for (var i = 0; i < n; i++) value[i] = Math.Tanh(close[i]);
                break;
        }
        defined.Fill(1);
    }

    // Marks every element defined, then clears the ones whose input is below the bound. The common case of
    // an all-admissible column is decided by one vectorized minimum.
    private static void MaskBelow(ReadOnlySpan<double> input, Span<double> value, Span<double> defined, double bound,
        bool inclusive)
    {
        defined.Fill(1);
        if (input.Length == 0) return;
        var minimum = FastMath.Min(input);
        if (inclusive ? minimum > bound : minimum >= bound) return;
        for (var i = 0; i < input.Length; i++)
            if (inclusive ? input[i] <= bound : input[i] < bound)
            {
                value[i] = 0;
                defined[i] = 0;
            }
    }
}
