namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>Allocation and vector primitives shared by the fast kernels.</summary>
/// <remarks>
/// On modern targets the element-wise operations are AiDotNet.Tensors SIMD kernels; the .NET Framework
/// target, which does not reference that package, uses the scalar loops.
/// </remarks>
internal static class FastMath
{
    /// <summary>An output column. Kernels write every element, so it need not be cleared first.</summary>
    internal static double[] Allocate(int count) =>
#if NETFRAMEWORK
        new double[count];
#else
        GC.AllocateUninitializedArray<double>(count);
#endif

    /// <summary>One exponential step: weight * previous + scaled, where scaled is alpha * input.</summary>
    /// <remarks>
    /// Written so the only operation on the loop-carried value is a single fused multiply-add. The usual
    /// previous + alpha * (input - previous) form chains a subtract, a multiply and an add through the
    /// recurrence, which is three times the latency per bar.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static double Blend(double weight, double previous, double scaled)
    {
#if !NETFRAMEWORK
        if (System.Runtime.Intrinsics.X86.Fma.IsSupported || System.Runtime.Intrinsics.Arm.AdvSimd.Arm64.IsSupported)
            return Math.FusedMultiplyAdd(weight, previous, scaled);
#endif
        return weight * previous + scaled;
    }

    /// <summary>Whether every element is finite, checked a vector at a time.</summary>
    internal static bool AllFinite(ReadOnlySpan<double> values)
    {
        // value - value is zero for a finite value and NaN for an infinity or a NaN.
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated && values.Length >= System.Numerics.Vector<double>.Count)
        {
            var width = System.Numerics.Vector<double>.Count;
            var check = System.Numerics.Vector<double>.Zero;
            for (; i <= values.Length - width; i += width)
            {
                var v = new System.Numerics.Vector<double>(values.Slice(i, width));
                check += v - v;
            }
            if (!System.Numerics.Vector.EqualsAll(check, System.Numerics.Vector<double>.Zero)) return false;
        }
#endif
        double tail = 0;
        for (; i < values.Length; i++) tail += values[i] - values[i];
        return tail == 0;
    }

    internal static void Add(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
#if !NETFRAMEWORK
        AiDotNet.Tensors.Engines.Simd.SimdKernels.VectorAdd(a, b, result);
#else
        for (var i = 0; i < result.Length; i++) result[i] = a[i] + b[i];
#endif
    }

    internal static void Subtract(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
#if !NETFRAMEWORK
        AiDotNet.Tensors.Engines.Simd.SimdKernels.VectorSubtract(a, b, result);
#else
        for (var i = 0; i < result.Length; i++) result[i] = a[i] - b[i];
#endif
    }

    internal static void Scale(ReadOnlySpan<double> a, double scalar, Span<double> result)
    {
#if !NETFRAMEWORK
        AiDotNet.Tensors.Engines.Simd.SimdKernels.MultiplyScalar(a, scalar, result);
#else
        for (var i = 0; i < result.Length; i++) result[i] = a[i] * scalar;
#endif
    }

    internal static void Floor(ReadOnlySpan<double> input, Span<double> result)
    {
#if !NETFRAMEWORK
        AiDotNet.Tensors.Engines.Simd.SimdKernels.Floor(input, result);
#else
        for (var i = 0; i < result.Length; i++) result[i] = Math.Floor(input[i]);
#endif
    }

    internal static void Ceiling(ReadOnlySpan<double> input, Span<double> result)
    {
#if !NETFRAMEWORK
        AiDotNet.Tensors.Engines.Simd.SimdKernels.Ceiling(input, result);
#else
        for (var i = 0; i < result.Length; i++) result[i] = Math.Ceiling(input[i]);
#endif
    }

    internal static void Sqrt(ReadOnlySpan<double> input, Span<double> result)
    {
#if !NETFRAMEWORK
        AiDotNet.Tensors.Engines.Simd.SimdKernels.Sqrt(input, result);
#else
        for (var i = 0; i < result.Length; i++) result[i] = Math.Sqrt(input[i]);
#endif
    }

    internal static double Min(ReadOnlySpan<double> input)
    {
#if !NETFRAMEWORK
        return AiDotNet.Tensors.Engines.Simd.SimdKernels.Min(input);
#else
        var minimum = input[0];
        for (var i = 1; i < input.Length; i++) minimum = Math.Min(minimum, input[i]);
        return minimum;
#endif
    }

    /// <summary>
    /// The larger of two finite values as one instruction. Math.Max also orders NaN and signed zero, which
    /// costs several more and, on a value carried from bar to bar, sets the pace of the whole loop.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static double Larger(double a, double b)
    {
#if !NETFRAMEWORK
        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
            return System.Runtime.Intrinsics.Vector128.ToScalar(System.Runtime.Intrinsics.X86.Sse2.MaxScalar(
                System.Runtime.Intrinsics.Vector128.CreateScalarUnsafe(a),
                System.Runtime.Intrinsics.Vector128.CreateScalarUnsafe(b)));
#endif
        return a > b ? a : b;
    }

    /// <summary>
    /// The correctly rounded mean of the high, low and close: the value the exact route publishes, without
    /// its arbitrary-precision sum.
    /// </summary>
    /// <remarks>
    /// Indicators that compare typical prices need them rounded exactly, because two bars with equal sums
    /// must compare equal. The three-way sum is kept as a rounded value and its exact residual, the quotient
    /// is corrected by the exact remainder, and the result is accepted only when what is left is provably
    /// inside the half-unit that makes it the nearest double. Anything closer to a tie than that, and any
    /// residual that is not exact, takes the exact route's own calculation.
    /// </remarks>
    internal static double TypicalPrice(double high, double low, double close)
    {
#if !NETFRAMEWORK
        var pair = high + low;
        var virtualLow = pair - high;
        var pairResidual = high - (pair - virtualLow) + (low - virtualLow);
        var sum = pair + close;
        var virtualClose = sum - pair;
        var sumResidual = pair - (sum - virtualClose) + (close - virtualClose);
        if (pairResidual == 0 && sumResidual == 0) return sum == 0 ? 0 : sum / 3;
        var residual = pairResidual + sumResidual;
        var quotient = sum / 3;
        // sum - 3 * quotient is exact in one fused step; with the sum's residual it is what the quotient lacks.
        var remainder = Math.FusedMultiplyAdd(-3, quotient, sum);
        var lacking = remainder + residual;
        var result = quotient + lacking / 3;
        var left = quotient - result + lacking / 3;
        var magnitude = Math.Abs(result);
        var unit = Math.BitIncrement(magnitude) - magnitude;
        // Exact residual sums, a result away from a power of two and from the subnormals, and a remaining
        // distance clearly under half a unit: then no other double is nearer.
        if (residual - pairResidual == sumResidual && lacking - remainder == residual
            && magnitude > 1e-290 && magnitude < 1e290
            && (BitConverter.DoubleToInt64Bits(result) & 0xFFFFFFFFFFFFFL) != 0
            && Math.Abs(left) < 0.4999999 * unit)
            return result;
#endif
        return Helpers.ExactTypicalPrice.Calculate(high, low, close);
    }
}
