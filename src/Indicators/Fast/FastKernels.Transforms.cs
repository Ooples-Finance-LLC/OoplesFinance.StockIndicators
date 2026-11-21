using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>Ceiling, floor or square root of the close. A negative close has no square root.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Rounding(FastColumns columns, FastOutputs outputs, PriceRoundingOperation operation)
    {
        // A ceiling or a floor is finite only if what it rounds is, so checking the result checks the close.
        // A square root is not: a negative close is replaced before anything could show.
        var checkedByResult = operation != PriceRoundingOperation.SquareRoot;
        ReadOnlySpan<double> close = checkedByResult ? columns.Raw(IndicatorInputFields.Close) : columns.Close;
        Span<double> value = outputs[0].AsSpan(0, close.Length);
        switch (operation)
        {
            case PriceRoundingOperation.Ceiling:
                FastMath.Ceiling(close, value);
                outputs.Flag(1, 0);
                break;
            case PriceRoundingOperation.Floor:
                FastMath.Floor(close, value);
                outputs.Flag(1, 0);
                break;
            default:
                FastMath.Sqrt(close, value);
                MaskBelow(close, value, outputs, 0, inclusive: false);
                break;
        }
        if (!checkedByResult) return;
        if (FastMath.AllFinite(value)) outputs.Verified(0);
        else _ = columns.Close;
    }

    /// <summary>A circular function of the close. The inverse sine and cosine exist only on [-1, 1].</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Circular(ReadOnlySpan<double> close, FastOutputs outputs, PriceCircularOperation operation)
    {
        var n = close.Length;
        Span<double> value = outputs[0].AsSpan(0, n);
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
                FastCircular.Direct(close, value, 0);
                break;
            case PriceCircularOperation.Cosine:
                FastCircular.Direct(close, value, 1);
                break;
            case PriceCircularOperation.Tangent:
                FastCircular.Direct(close, value, 2);
                break;
#endif
            case PriceCircularOperation.ArcTangent:
                FastCircular.Atan(close, value);
                break;
            default:
                var sine = operation == PriceCircularOperation.ArcSine;
                // A series wholly inside the domain is defined on every bar, which needs no column of its own.
                var outside = 0;
                for (var i = 0; i < n; i++)
                {
                    var x = close[i];
                    outside |= x >= -1 && x <= 1 ? 0 : 1;
                }
                if (outside == 0)
                {
                    if (sine)
                        for (var i = 0; i < n; i++) value[i] = Math.Asin(close[i]);
                    else
                        for (var i = 0; i < n; i++) value[i] = Math.Acos(close[i]);
                    break;
                }
                Span<double> defined = outputs[1].AsSpan(0, n);
                for (var i = 0; i < n; i++)
                {
                    var x = close[i];
                    var inside = x >= -1 && x <= 1;
                    value[i] = !inside ? 0 : sine ? Math.Asin(x) : Math.Acos(x);
                    defined[i] = inside ? 1 : 0;
                }
                return;
        }
        outputs.Flag(1, 0);
    }

    /// <summary>A logarithm, exponential or hyperbolic function of the close. Logarithms need a positive close.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Transcendental(ReadOnlySpan<double> close, FastOutputs outputs,
        PriceTranscendentalOperation operation)
    {
        var n = close.Length;
        Span<double> value = outputs[0].AsSpan(0, n);
        switch (operation)
        {
            case PriceTranscendentalOperation.NaturalLogarithm:
            case PriceTranscendentalOperation.CommonLogarithm:
                var common = operation == PriceTranscendentalOperation.CommonLogarithm;
                // A series of positive closes, as prices are, has a logarithm on every bar: no flags to write,
                // and the logarithms are taken four at a time.
                if (n == 0 || FastMath.Min(close) > 0)
                {
                    FastExponentials.Log(close, value, common);
                    break;
                }
                Span<double> defined = outputs[1].AsSpan(0, n);
                for (var i = 0; i < n; i++)
                {
                    var x = close[i];
                    var positive = x > 0;
                    value[i] = !positive ? 0 : common ? Math.Log10(x) : Math.Log(x);
                    defined[i] = positive ? 1 : 0;
                }
                return;
            case PriceTranscendentalOperation.Exponential:
                FastExponentials.Exp(close, value);
                break;
            case PriceTranscendentalOperation.HyperbolicSine:
                FastExponentials.Sinh(close, value);
                break;
            case PriceTranscendentalOperation.HyperbolicCosine:
                FastExponentials.Cosh(close, value);
                break;
            default:
                FastExponentials.Tanh(close, value);
                break;
        }
        outputs.Flag(1, 0);
    }

    // Marks every element defined, then clears the ones whose input is below the bound. The common case of
    // an all-admissible column is decided by one vectorized minimum, and publishes the shared all-ones flag.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void MaskBelow(ReadOnlySpan<double> input, Span<double> value, FastOutputs outputs, double bound,
        bool inclusive)
    {
        if (input.Length == 0 || (inclusive ? FastMath.Min(input) > bound : FastMath.Min(input) >= bound))
        {
            outputs.Flag(1, 0);
            return;
        }
        Span<double> defined = outputs[1].AsSpan(0, input.Length);
        defined.Fill(1);
        for (var i = 0; i < input.Length; i++)
            if (inclusive ? input[i] <= bound : input[i] < bound)
            {
                value[i] = 0;
                defined[i] = 0;
            }
    }
}
