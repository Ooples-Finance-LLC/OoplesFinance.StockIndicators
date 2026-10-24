using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// APIs used by the comparison indicators must also work on .NET Framework 4.6.1.
internal static class FrameworkCompatibility
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool IsFinite(double value) =>
#if NETFRAMEWORK
        !double.IsNaN(value) && !double.IsInfinity(value);
#else
        double.IsFinite(value);
#endif

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static double BitIncrement(double value) =>
#if NETFRAMEWORK
        BitIncrementPortable(value);
#else
        Math.BitIncrement(value);
#endif

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static double BitDecrement(double value) =>
#if NETFRAMEWORK
        BitDecrementPortable(value);
#else
        Math.BitDecrement(value);
#endif

    internal static double BitIncrementPortable(double value)
    {
        if (double.IsNaN(value) || double.IsPositiveInfinity(value)) return value;
        if (value == 0) return double.Epsilon;
        var bits = BitConverter.DoubleToInt64Bits(value);
        return BitConverter.Int64BitsToDouble(bits + (value > 0 ? 1 : -1));
    }

    internal static double BitDecrementPortable(double value)
    {
        if (double.IsNaN(value) || double.IsNegativeInfinity(value)) return value;
        if (value == 0) return -double.Epsilon;
        var bits = BitConverter.DoubleToInt64Bits(value);
        return BitConverter.Int64BitsToDouble(bits + (value > 0 ? -1 : 1));
    }

    internal static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum) throw new ArgumentException("Minimum exceeds maximum.");
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    internal static long GetBitLength(BigInteger value)
    {
#if NET8_0_OR_GREATER
        return value.GetBitLength();
#else
        return GetBitLengthPortable(value);
#endif
    }

    internal static long GetBitLengthPortable(BigInteger value)
    {
        // BigInteger.GetBitLength excludes the sign bit, including for negative values.
        if (value.Sign < 0) value = ~value;
        var bytes = value.ToByteArray();
        var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var bits = (long)last * 8;
        for (var head = bytes[last]; head != 0; head >>= 1) bits++;
        return bits;
    }
}
