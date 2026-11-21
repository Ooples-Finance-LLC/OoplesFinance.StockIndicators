namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>
/// The exponential, the logarithm and the three hyperbolic functions of a whole series, four values at a time.
/// </summary>
/// <remarks>
/// The exponential is the classical reduction to a power of two times the exponential of a remainder no
/// larger than half of ln 2, and that as a rational function of the remainder (the coefficients are those
/// of the Cephes library's double-precision exp). It agrees with the runtime's function to a unit or two in
/// the last place, not bit for bit, which is what the fast route promises and not what the exact one does.
/// A group of four holding a value outside the range where that holds - near overflow, or so close to zero
/// that a hyperbolic function loses its leading digits to a subtraction - is taken by the runtime's own
/// function, as is everything on hardware without the instructions this is written for.
/// </remarks>
internal static class FastExponentials
{
    /// <summary>e to the power of each value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Exp(ReadOnlySpan<double> input, Span<double> output)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var limit = System.Runtime.Intrinsics.Vector256.Create(ExpLimit);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                if (AnyAbove(x, limit))
                {
                    for (var j = i; j < i + 4; j++) output[j] = Math.Exp(input[j]);
                    continue;
                }
                System.Runtime.Intrinsics.Vector256.CopyTo(ExpOf(x), output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = Math.Exp(input[i]);
    }

    /// <summary>The hyperbolic sine of each value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Sinh(ReadOnlySpan<double> input, Span<double> output)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var limit = System.Runtime.Intrinsics.Vector256.Create(HyperbolicLimit);
            var floor = System.Runtime.Intrinsics.Vector256.Create(CancellationFloor);
            var half = System.Runtime.Intrinsics.Vector256.Create(0.5);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                var size = System.Runtime.Intrinsics.Vector256.Abs(x);
                if (AnyAbove(x, limit) || AnyBelow(size, floor))
                {
                    for (var j = i; j < i + 4; j++) output[j] = Math.Sinh(input[j]);
                    continue;
                }
                // Of the size, with the sign put back: the function is odd.
                var e = ExpOf(size);
                var value = half * e - half / e;
                System.Runtime.Intrinsics.Vector256.CopyTo(WithSignOf(value, x), output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = Math.Sinh(input[i]);
    }

    /// <summary>The hyperbolic cosine of each value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Cosh(ReadOnlySpan<double> input, Span<double> output)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var limit = System.Runtime.Intrinsics.Vector256.Create(HyperbolicLimit);
            var half = System.Runtime.Intrinsics.Vector256.Create(0.5);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                if (AnyAbove(x, limit))
                {
                    for (var j = i; j < i + 4; j++) output[j] = Math.Cosh(input[j]);
                    continue;
                }
                var e = ExpOf(System.Runtime.Intrinsics.Vector256.Abs(x));
                System.Runtime.Intrinsics.Vector256.CopyTo(half * e + half / e, output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = Math.Cosh(input[i]);
    }

    /// <summary>The hyperbolic tangent of each value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Tanh(ReadOnlySpan<double> input, Span<double> output)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var floor = System.Runtime.Intrinsics.Vector256.Create(CancellationFloor);
            var one = System.Runtime.Intrinsics.Vector256.Create(1.0);
            var two = System.Runtime.Intrinsics.Vector256.Create(2.0);
            // Past this the tangent is one to the last place, and twice the value is kept from overflowing.
            var most = System.Runtime.Intrinsics.Vector256.Create(40.0);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                var size = System.Runtime.Intrinsics.Vector256.Abs(x);
                if (AnyBelow(size, floor))
                {
                    for (var j = i; j < i + 4; j++) output[j] = Math.Tanh(input[j]);
                    continue;
                }
                var e = ExpOf(two * System.Runtime.Intrinsics.Vector256.Min(size, most));
                var value = one - two / (e + one);
                System.Runtime.Intrinsics.Vector256.CopyTo(WithSignOf(value, x), output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = Math.Tanh(input[i]);
    }

    /// <summary>The natural or the common logarithm of each value. Every value must be positive.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Log(ReadOnlySpan<double> input, Span<double> output, bool common)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            // Below the smallest normal number the exponent is not where it is read from.
            var least = System.Runtime.Intrinsics.Vector256.Create(2.2250738585072014E-308);
            var scale = System.Runtime.Intrinsics.Vector256.Create(0.43429448190325182765);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                if (AnyBelow(x, least))
                {
                    for (var j = i; j < i + 4; j++) output[j] = common ? Math.Log10(input[j]) : Math.Log(input[j]);
                    continue;
                }
                var value = LogOf(x);
                System.Runtime.Intrinsics.Vector256.CopyTo(common ? value * scale : value, output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = common ? Math.Log10(input[i]) : Math.Log(input[i]);
    }

#if !NETFRAMEWORK
    // Beyond these a group is left to the runtime: the exponential nears overflow or the subnormal range, and
    // below the floor a hyperbolic sine or tangent is a difference of two nearly equal numbers.
    private const double ExpLimit = 700, HyperbolicLimit = 700, CancellationFloor = 0.5;

    private static bool Supported => System.Runtime.Intrinsics.X86.Avx2.IsSupported && System.Runtime.Intrinsics.X86.Fma.IsSupported;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static bool AnyAbove(System.Runtime.Intrinsics.Vector256<double> x, System.Runtime.Intrinsics.Vector256<double> limit) =>
        System.Runtime.Intrinsics.Vector256.GreaterThanAny(System.Runtime.Intrinsics.Vector256.Abs(x), limit);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static bool AnyBelow(System.Runtime.Intrinsics.Vector256<double> size, System.Runtime.Intrinsics.Vector256<double> floor) =>
        System.Runtime.Intrinsics.Vector256.LessThanAny(size, floor);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static System.Runtime.Intrinsics.Vector256<double> WithSignOf(System.Runtime.Intrinsics.Vector256<double> value,
        System.Runtime.Intrinsics.Vector256<double> sign)
    {
        var bit = System.Runtime.Intrinsics.Vector256.Create(-0.0);
        return System.Runtime.Intrinsics.Vector256.BitwiseOr(value, System.Runtime.Intrinsics.Vector256.BitwiseAnd(sign, bit));
    }

    // e to the power of four values, each no larger in size than the limit above.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static System.Runtime.Intrinsics.Vector256<double> ExpOf(System.Runtime.Intrinsics.Vector256<double> x)
    {
        // x = k ln 2 + r, with ln 2 in two parts so that the remainder keeps its low bits.
        var k = System.Runtime.Intrinsics.X86.Avx.RoundToNearestInteger(
            x * System.Runtime.Intrinsics.Vector256.Create(1.4426950408889634073599));
        var r = System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(k,
            System.Runtime.Intrinsics.Vector256.Create(6.93145751953125E-1), x);
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(k,
            System.Runtime.Intrinsics.Vector256.Create(1.42860682030941723212E-6), r);
        // e^r = 1 + 2 r P(r^2) / (Q(r^2) - r P(r^2)).
        var square = r * r;
        var p = System.Runtime.Intrinsics.Vector256.Create(1.26177193074810590878E-4);
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, square, System.Runtime.Intrinsics.Vector256.Create(3.02994407707441961300E-2));
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, square, System.Runtime.Intrinsics.Vector256.Create(9.99999999999999999910E-1));
        p *= r;
        var q = System.Runtime.Intrinsics.Vector256.Create(3.00198505138664455042E-6);
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, square, System.Runtime.Intrinsics.Vector256.Create(2.52448340349684104192E-3));
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, square, System.Runtime.Intrinsics.Vector256.Create(2.27265548208155028766E-1));
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, square, System.Runtime.Intrinsics.Vector256.Create(2.0));
        var fraction = p / (q - p);
        var value = fraction + fraction + System.Runtime.Intrinsics.Vector256.Create(1.0);
        // Times 2^k, as two powers of about half the size each: neither can leave the range of a double.
        var whole = System.Runtime.Intrinsics.X86.Avx.ConvertToVector128Int32(k);
        var first = System.Runtime.Intrinsics.X86.Sse2.ShiftRightArithmetic(whole, 1);
        var second = System.Runtime.Intrinsics.X86.Sse2.Subtract(whole, first);
        return value * PowerOfTwo(first) * PowerOfTwo(second);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static System.Runtime.Intrinsics.Vector256<double> PowerOfTwo(System.Runtime.Intrinsics.Vector128<int> exponent)
    {
        var biased = System.Runtime.Intrinsics.X86.Avx2.Add(
            System.Runtime.Intrinsics.X86.Avx2.ConvertToVector256Int64(exponent), System.Runtime.Intrinsics.Vector256.Create(1023L));
        return System.Runtime.Intrinsics.Vector256.AsDouble(System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(biased, 52));
    }

    // The natural logarithm of four positive normal values: x = m 2^e with m in [1/2, 1), the fraction brought
    // to within a square root of two of one, and then ln m as a rational function of (m - 1) / (m + 1)
    // (the Cephes library's double-precision log, in the form it keeps for every exponent).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static System.Runtime.Intrinsics.Vector256<double> LogOf(System.Runtime.Intrinsics.Vector256<double> x)
    {
        var bits = System.Runtime.Intrinsics.Vector256.AsInt64(x);
        var raised = System.Runtime.Intrinsics.X86.Avx2.ShiftRightLogical(bits, 52);
        var exponents = System.Runtime.Intrinsics.X86.Avx2.PermuteVar8x32(System.Runtime.Intrinsics.Vector256.AsInt32(raised), System.Runtime.Intrinsics.Vector256.Create(0, 2, 4, 6, 0, 0, 0, 0));
        var e = System.Runtime.Intrinsics.X86.Avx.ConvertToVector256Double(System.Runtime.Intrinsics.Vector256.GetLower(exponents)) - System.Runtime.Intrinsics.Vector256.Create(1022.0);
        var m = System.Runtime.Intrinsics.Vector256.AsDouble(System.Runtime.Intrinsics.X86.Avx2.Or(System.Runtime.Intrinsics.X86.Avx2.And(bits, System.Runtime.Intrinsics.Vector256.Create(0x000FFFFFFFFFFFFFL)), System.Runtime.Intrinsics.Vector256.Create(0x3FE0000000000000L)));
        var half = System.Runtime.Intrinsics.Vector256.Create(0.5);
        var small = System.Runtime.Intrinsics.Vector256.LessThan(m, System.Runtime.Intrinsics.Vector256.Create(0.70710678118654752440));
        e -= System.Runtime.Intrinsics.Vector256.ConditionalSelect(small, System.Runtime.Intrinsics.Vector256.Create(1.0), System.Runtime.Intrinsics.Vector256<double>.Zero);
        var z = m - half;
        var numerator = System.Runtime.Intrinsics.Vector256.ConditionalSelect(small, z, z - half);
        var denominator = System.Runtime.Intrinsics.Vector256.ConditionalSelect(small, half * z + half, half * m + half);
        var t = numerator / denominator;
        var square = t * t;
        var r = System.Runtime.Intrinsics.Vector256.Create(-7.89580278884799154124E-1);
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(r, square, System.Runtime.Intrinsics.Vector256.Create(1.63866645699558079767E1));
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(r, square, System.Runtime.Intrinsics.Vector256.Create(-6.41409952958715622951E1));
        var q = square + System.Runtime.Intrinsics.Vector256.Create(-3.56722798256324312549E1);
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, square, System.Runtime.Intrinsics.Vector256.Create(3.12093766372244180303E2));
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, square, System.Runtime.Intrinsics.Vector256.Create(-7.69691943550460008604E2));
        var value = t * (square * r / q);
        // e ln 2, with ln 2 in two parts so that the larger one is exact.
        value = System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(e, System.Runtime.Intrinsics.Vector256.Create(2.121944400546905827679e-4), value);
        value += t;
        return System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(e, System.Runtime.Intrinsics.Vector256.Create(0.693359375), value);
    }
#endif
}
