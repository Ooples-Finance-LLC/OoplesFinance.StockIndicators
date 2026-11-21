namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>The sine, cosine and tangent of a whole series and their inverses, four values at a time.</summary>
/// <remarks>
/// The direct functions take the value to within a quarter turn of zero - the nearest multiple of half pi
/// taken off in three parts, each in one fused step - and then the sine and cosine of what is left as
/// polynomials; the inverse tangent folds the value into a small one and takes a rational function of it.
/// The coefficients are those of the Cephes library's double-precision functions. They agree with the
/// runtime's to a few units in the last place, not bit for bit, which is what the fast route promises.
/// A group of four holding a value too large for the reduction, or one that lands so near a multiple of
/// half pi that the reduction's own rounding would show, is taken by the runtime's function, as is
/// everything on hardware without the instructions this is written for.
/// </remarks>
internal static class FastCircular
{
    /// <summary>Operation 0 is the sine, 1 the cosine and 2 the tangent of each value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Direct(ReadOnlySpan<double> input, Span<double> output, int operation)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var most = System.Runtime.Intrinsics.Vector256.Create(Largest);
            var least = System.Runtime.Intrinsics.Vector256.Create(Nearest);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                // The nearest multiple of half pi, taken off in three parts.
                var k = System.Runtime.Intrinsics.X86.Avx.RoundToNearestInteger(x * System.Runtime.Intrinsics.Vector256.Create(0.63661977236758134308));
                var r = System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(k, System.Runtime.Intrinsics.Vector256.Create(1.5707963267948966), x);
                r = System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(k, System.Runtime.Intrinsics.Vector256.Create(6.123233995736766E-17), r);
                r = System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(k, System.Runtime.Intrinsics.Vector256.Create(-1.4973849048591698E-33), r);
                if (System.Runtime.Intrinsics.Vector256.GreaterThanAny(System.Runtime.Intrinsics.Vector256.Abs(x), most) || System.Runtime.Intrinsics.Vector256.LessThanAny(System.Runtime.Intrinsics.Vector256.Abs(r), least))
                {
                    for (var j = i; j < i + 4; j++) output[j] = One(input[j], operation);
                    continue;
                }
                var z = r * r;
                var s = System.Runtime.Intrinsics.Vector256.Create(1.58962301576546568060E-10);
                s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, z, System.Runtime.Intrinsics.Vector256.Create(-2.50507477628578072866E-8));
                s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, z, System.Runtime.Intrinsics.Vector256.Create(2.75573136213857245213E-6));
                s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, z, System.Runtime.Intrinsics.Vector256.Create(-1.98412698295895385996E-4));
                s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, z, System.Runtime.Intrinsics.Vector256.Create(8.33333333332211858878E-3));
                s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, z, System.Runtime.Intrinsics.Vector256.Create(-1.66666666666666307295E-1));
                var sine = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s * z, r, r);
                var c = System.Runtime.Intrinsics.Vector256.Create(-1.13585365213876817300E-11);
                c = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(c, z, System.Runtime.Intrinsics.Vector256.Create(2.08757008419747316778E-9));
                c = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(c, z, System.Runtime.Intrinsics.Vector256.Create(-2.75573141792967388112E-7));
                c = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(c, z, System.Runtime.Intrinsics.Vector256.Create(2.48015872888517045348E-5));
                c = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(c, z, System.Runtime.Intrinsics.Vector256.Create(-1.38888888888730564116E-3));
                c = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(c, z, System.Runtime.Intrinsics.Vector256.Create(4.16666666666665929218E-2));
                var cosine = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(c * z, z, System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(System.Runtime.Intrinsics.Vector256.Create(0.5), z, System.Runtime.Intrinsics.Vector256.Create(1.0)));
                // Which quarter turn the value was in decides which of the two it is, and its sign.
                var quarter = System.Runtime.Intrinsics.X86.Avx2.ConvertToVector256Int64(System.Runtime.Intrinsics.X86.Avx.ConvertToVector128Int32(k));
                var odd = System.Runtime.Intrinsics.Vector256.AsDouble(System.Runtime.Intrinsics.X86.Avx2.CompareEqual(System.Runtime.Intrinsics.X86.Avx2.And(quarter, System.Runtime.Intrinsics.Vector256.Create(1L)), System.Runtime.Intrinsics.Vector256.Create(1L)));
                System.Runtime.Intrinsics.Vector256<double> value;
                if (operation == 2)
                {
                    value = System.Runtime.Intrinsics.Vector256.ConditionalSelect(odd, -cosine / sine, sine / cosine);
                }
                else
                {
                    var turned = operation == 0 ? quarter : System.Runtime.Intrinsics.X86.Avx2.Add(quarter, System.Runtime.Intrinsics.Vector256.Create(1L));
                    var sign = System.Runtime.Intrinsics.Vector256.AsDouble(System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(System.Runtime.Intrinsics.X86.Avx2.And(turned, System.Runtime.Intrinsics.Vector256.Create(2L)), 62));
                    var swap = operation == 0 ? odd : System.Runtime.Intrinsics.Vector256.AsDouble(System.Runtime.Intrinsics.X86.Avx2.CompareEqual(System.Runtime.Intrinsics.X86.Avx2.And(quarter, System.Runtime.Intrinsics.Vector256.Create(1L)), System.Runtime.Intrinsics.Vector256<long>.Zero));
                    value = System.Runtime.Intrinsics.Vector256.Xor(System.Runtime.Intrinsics.Vector256.ConditionalSelect(swap, cosine, sine), sign);
                }
                System.Runtime.Intrinsics.Vector256.CopyTo(value, output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = One(input[i], operation);
    }

    /// <summary>The inverse tangent of each value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Atan(ReadOnlySpan<double> input, Span<double> output)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var one = System.Runtime.Intrinsics.Vector256.Create(1.0);
            var bit = System.Runtime.Intrinsics.Vector256.Create(-0.0);
            for (; i <= input.Length - 4; i += 4)
            {
                var signed = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                var x = System.Runtime.Intrinsics.Vector256.Abs(signed);
                // Past tan(3 pi / 8) the angle is half pi less that of the reciprocal; past 0.66 a quarter pi
                // more that of (x - 1) / (x + 1); below it the value stands.
                var large = System.Runtime.Intrinsics.Vector256.GreaterThan(x, System.Runtime.Intrinsics.Vector256.Create(2.41421356237309504880));
                var middle = System.Runtime.Intrinsics.Vector256.AndNot(System.Runtime.Intrinsics.Vector256.GreaterThan(x, System.Runtime.Intrinsics.Vector256.Create(0.66)), large);
                var t = System.Runtime.Intrinsics.Vector256.ConditionalSelect(large, -one / x, System.Runtime.Intrinsics.Vector256.ConditionalSelect(middle, (x - one) / (x + one), x));
                var angle = System.Runtime.Intrinsics.Vector256.ConditionalSelect(large, System.Runtime.Intrinsics.Vector256.Create(1.57079632679489661923),
                    System.Runtime.Intrinsics.Vector256.ConditionalSelect(middle, System.Runtime.Intrinsics.Vector256.Create(0.78539816339744830962), System.Runtime.Intrinsics.Vector256<double>.Zero));
                var more = System.Runtime.Intrinsics.Vector256.ConditionalSelect(large, System.Runtime.Intrinsics.Vector256.Create(6.123233995736765886130E-17),
                    System.Runtime.Intrinsics.Vector256.ConditionalSelect(middle, System.Runtime.Intrinsics.Vector256.Create(3.061616997868382943065E-17), System.Runtime.Intrinsics.Vector256<double>.Zero));
                var z = t * t;
                var p = System.Runtime.Intrinsics.Vector256.Create(-8.750608600031904122785E-1);
                p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-1.615753718733365076637E1));
                p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-7.500855792314704667340E1));
                p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-1.228866684490136173410E2));
                p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-6.485021904942025371773E1));
                var q = z + System.Runtime.Intrinsics.Vector256.Create(2.485846490142306297962E1);
                q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(1.650270098316988542046E2));
                q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(4.328810604912902668951E2));
                q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(4.853903996359136964868E2));
                q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(1.945506571482613964425E2));
                var value = angle + (System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(z * p / q, t, t) + more);
                System.Runtime.Intrinsics.Vector256.CopyTo(System.Runtime.Intrinsics.Vector256.BitwiseOr(value, System.Runtime.Intrinsics.Vector256.BitwiseAnd(signed, bit)), output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = Math.Atan(input[i]);
    }

    /// <summary>The inverse sine, or the inverse cosine, of each value. Every value must be in [-1, 1].</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Inverse(ReadOnlySpan<double> input, Span<double> output, bool sine)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (Supported)
        {
            var half = System.Runtime.Intrinsics.Vector256.Create(0.5);
            var quarterPi = System.Runtime.Intrinsics.Vector256.Create(0.78539816339744830962);
            var more = System.Runtime.Intrinsics.Vector256.Create(6.123233995736765886130E-17);
            for (; i <= input.Length - 4; i += 4)
            {
                var x = System.Runtime.Intrinsics.Vector256.Create<double>(input.Slice(i, 4));
                if (sine)
                {
                    System.Runtime.Intrinsics.Vector256.CopyTo(AsinOf(x), output.Slice(i, 4));
                    continue;
                }
                // Past a half the angle is twice the inverse sine of sqrt((1 - x) / 2), which keeps the digits
                // of an angle near zero; below it, a quarter turn less the inverse sine.
                var near = System.Runtime.Intrinsics.Vector256.GreaterThan(x, half);
                var angle = AsinOf(System.Runtime.Intrinsics.Vector256.ConditionalSelect(near, System.Runtime.Intrinsics.Vector256.Sqrt(System.Runtime.Intrinsics.X86.Fma.MultiplyAddNegated(half, x, half)), x));
                var far = quarterPi - angle + more + quarterPi;
                System.Runtime.Intrinsics.Vector256.CopyTo(System.Runtime.Intrinsics.Vector256.ConditionalSelect(near, angle + angle, far), output.Slice(i, 4));
            }
        }
#endif
        for (; i < input.Length; i++) output[i] = sine ? Math.Asin(input[i]) : Math.Acos(input[i]);
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double One(double x, int operation) => operation == 0 ? Math.Sin(x) : operation == 1 ? Math.Cos(x) : Math.Tan(x);

#if !NETFRAMEWORK
    // Past the first the multiple of half pi no longer fits what takes it off exactly; under the second the
    // rounding of the reduction is no longer far below what is left.
    private const double Largest = 1e5, Nearest = 1e-8;

    private static bool Supported => System.Runtime.Intrinsics.X86.Avx2.IsSupported && System.Runtime.Intrinsics.X86.Fma.IsSupported;

    // The inverse sine of four values in [-1, 1]: a rational function of the square below 0.625, and above it
    // a quarter turn twice over less a rational function of 1 - |x| times sqrt(2 (1 - |x|)).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static System.Runtime.Intrinsics.Vector256<double> AsinOf(System.Runtime.Intrinsics.Vector256<double> signed)
    {
        var a = System.Runtime.Intrinsics.Vector256.Abs(signed);
        var quarterPi = System.Runtime.Intrinsics.Vector256.Create(0.78539816339744830962);
        var w = System.Runtime.Intrinsics.Vector256.Create(1.0) - a;
        var r = System.Runtime.Intrinsics.Vector256.Create(2.967721961301243206100E-3);
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(r, w, System.Runtime.Intrinsics.Vector256.Create(-5.634242780008963776856E-1));
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(r, w, System.Runtime.Intrinsics.Vector256.Create(6.968710824104713396794E0));
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(r, w, System.Runtime.Intrinsics.Vector256.Create(-2.556901049652824852289E1));
        r = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(r, w, System.Runtime.Intrinsics.Vector256.Create(2.853665548261061424989E1));
        var s = w + System.Runtime.Intrinsics.Vector256.Create(-2.194779531642920639778E1);
        s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, w, System.Runtime.Intrinsics.Vector256.Create(1.470656354026814941758E2));
        s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, w, System.Runtime.Intrinsics.Vector256.Create(-3.838770957603691357202E2));
        s = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(s, w, System.Runtime.Intrinsics.Vector256.Create(3.424398657913078477438E2));
        var root = System.Runtime.Intrinsics.Vector256.Sqrt(w + w);
        var upper = quarterPi - root;
        upper -= System.Runtime.Intrinsics.X86.Fma.MultiplySubtract(root, w * r / s, System.Runtime.Intrinsics.Vector256.Create(6.123233995736765886130E-17));
        upper += quarterPi;
        var z = a * a;
        var p = System.Runtime.Intrinsics.Vector256.Create(4.253011369004428248960E-3);
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-6.019598008014123785661E-1));
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(5.444622390564711410273E0));
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-1.626247967210700244449E1));
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(1.956261983317594739197E1));
        p = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(p, z, System.Runtime.Intrinsics.Vector256.Create(-8.198089802484824371615E0));
        var q = z + System.Runtime.Intrinsics.Vector256.Create(-1.474091372988853791896E1);
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(7.049610280856842141659E1));
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(-1.471791292232726029859E2));
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(1.395105614657485689735E2));
        q = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(q, z, System.Runtime.Intrinsics.Vector256.Create(-4.918853881490881290097E1));
        var lower = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(z * p / q, a, a);
        var value = System.Runtime.Intrinsics.Vector256.ConditionalSelect(System.Runtime.Intrinsics.Vector256.GreaterThan(a, System.Runtime.Intrinsics.Vector256.Create(0.625)), upper, lower);
        return System.Runtime.Intrinsics.Vector256.BitwiseOr(value, System.Runtime.Intrinsics.Vector256.BitwiseAnd(signed, System.Runtime.Intrinsics.Vector256.Create(-0.0)));
    }
#endif
}
