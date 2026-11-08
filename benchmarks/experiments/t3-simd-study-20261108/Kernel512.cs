using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace T3Study;
internal readonly struct Kernel512(int period)
{
    private readonly ulong d = (ulong)period + 1;
    private readonly ulong reciprocal = (ulong)(((UInt128)1 << 64) / ((ulong)period + 1));
    private static readonly Vector512<ulong> Mask32 = Vector512.Create(0xffffffffUL);
    private static readonly Vector512<ulong> Fraction = Vector512.Create((1UL << 52) - 1);
    private static readonly Vector512<ulong> Hidden = Vector512.Create(1UL << 52);
    private static readonly Vector512<ulong> One = Vector512.Create(1UL);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> Mul32(Vector512<ulong> a, Vector512<ulong> b) =>
        Avx512F.Multiply(a.AsUInt32(), b.AsUInt32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> SmallProduct(Vector512<ulong> a, ulong b) =>
        Mul32(a, Vector512.Create(b)) + (Mul32(a >> 32, Vector512.Create(b)) << 32);

    // Four exact 32x32 products, with the complete middle carry retained.
    // This independently tested primitive also covers unrestricted 64-bit inputs.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<ulong> HighProduct(Vector512<ulong> a, Vector512<ulong> b)
    {
        var lo = Mul32(a, b);
        var cross1 = Mul32(a, b >> 32);
        var cross2 = Mul32(a >> 32, b);
        var middle = (lo >> 32) + (cross1 & Mask32) + (cross2 & Mask32);
        return Mul32(a >> 32, b >> 32) + (cross1 >> 32) + (cross2 >> 32) + (middle >> 32);
    }

    // Same preconditions as the original scalar SmoothSameBinade, per lane:
    // period1..2047 and positive normal operands with identical exponent bits.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vector512<ulong> Smooth(Vector512<ulong> prior, Vector512<ulong> input)
    {
        var a = (prior & Fraction) | Hidden;
        var b = (input & Fraction) | Hidden;
        var denominator = Vector512.Create(d);
        var numerator = SmallProduct(a, d - 2) + (b << 1);
        var quotient = HighProduct(numerator, Vector512.Create(reciprocal));
        var remainder = numerator - SmallProduct(quotient, d);
        var correction = Vector512.GreaterThanOrEqual(remainder, denominator);
        quotient += correction & One;
        remainder -= correction & denominator;
        var complement = denominator - remainder;
        var tieOdd = Vector512.Equals(remainder, complement) & Vector512.Equals(quotient & One, One);
        quotient += (Vector512.GreaterThan(remainder, complement) | tieOdd) & One;
        return (prior & ~Fraction) | (quotient & Fraction);
    }
}
