namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>Allocation and vector primitives shared by the fast kernels.</summary>
/// <remarks>
/// The element-wise operations are written here over <see cref="System.Numerics.Vector{T}"/> rather than
/// called from AiDotNet.Tensors: loading that assembly runs its GPU detection, which costs a CPU-only
/// caller about two seconds on its first build. The .NET Framework target uses the scalar loops.
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

    /// <summary>
    /// Runs the exponential recurrence value = weight * value + alpha * input over
    /// <paramref name="input"/> from <paramref name="start"/>, writing each value, and returns the last.
    /// </summary>
    /// <remarks>
    /// Four bars at a time, each written from the value four bars back and a short sum of the inputs
    /// between: the recurrence then carries one multiply-add per four bars instead of one per bar. The
    /// values agree with the bar-by-bar recurrence to rounding, not bit for bit.
    /// </remarks>
    internal static double BlendRun(double weight, double alpha, ReadOnlySpan<double> input, Span<double> output, int start,
        double value)
    {
        var i = start;
        double weight2 = weight * weight, weight3 = weight2 * weight, weight4 = weight2 * weight2;
        for (; i <= input.Length - 4; i += 4)
        {
            var source = input.Slice(i, 4);
            var target = output.Slice(i, 4);
            var s0 = alpha * source[0];
            var p1 = Blend(weight, s0, alpha * source[1]);
            var p2 = Blend(weight, p1, alpha * source[2]);
            var p3 = Blend(weight, p2, alpha * source[3]);
            target[0] = Blend(weight, value, s0);
            target[1] = Blend(weight2, value, p1);
            target[2] = Blend(weight3, value, p2);
            value = Blend(weight4, value, p3);
            target[3] = value;
        }
        for (; i < input.Length; i++)
        {
            value = Blend(weight, value, alpha * input[i]);
            output[i] = value;
        }
        return value;
    }

    /// <summary>Whether every element is finite, checked a vector at a time.</summary>
    internal static bool AllFinite(ReadOnlySpan<double> values)
    {
        // value - value is +0 for a finite value and NaN for an infinity or a NaN, so the OR of the
        // differences' bits is zero only when every value is finite. Four accumulators and a bitwise OR
        // keep the loop off the adder's latency, which a single running sum is bound by.
        var i = 0;
#if !NETFRAMEWORK
        var width = System.Numerics.Vector<double>.Count;
        if (System.Numerics.Vector.IsHardwareAccelerated && values.Length >= 4 * width)
        {
            System.Numerics.Vector<long> a = default, b = default, c = default, d = default;
            for (; i <= values.Length - 4 * width; i += 4 * width)
            {
                var block = values.Slice(i, 4 * width);
                var v0 = new System.Numerics.Vector<double>(block);
                var v1 = new System.Numerics.Vector<double>(block.Slice(width));
                var v2 = new System.Numerics.Vector<double>(block.Slice(2 * width));
                var v3 = new System.Numerics.Vector<double>(block.Slice(3 * width));
                a |= System.Numerics.Vector.AsVectorInt64(v0 - v0); // NOSONAR x - x is the finite test
                b |= System.Numerics.Vector.AsVectorInt64(v1 - v1); // NOSONAR x - x is the finite test
                c |= System.Numerics.Vector.AsVectorInt64(v2 - v2); // NOSONAR x - x is the finite test
                d |= System.Numerics.Vector.AsVectorInt64(v3 - v3); // NOSONAR x - x is the finite test
            }
            if (!System.Numerics.Vector.EqualsAll(a | b | c | d, System.Numerics.Vector<long>.Zero)) return false;
        }
#endif
        double tail = 0;
        for (; i < values.Length; i++) tail += values[i] - values[i]; // NOSONAR x - x is the finite test
        return tail == 0;
    }

    internal static void Add(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            for (; i <= result.Length - width; i += width)
                (new System.Numerics.Vector<double>(a.Slice(i, width)) + new System.Numerics.Vector<double>(b.Slice(i, width)))
                    .CopyTo(result.Slice(i, width));
        }
#endif
        for (; i < result.Length; i++) result[i] = a[i] + b[i];
    }

    internal static void Subtract(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            for (; i <= result.Length - width; i += width)
                (new System.Numerics.Vector<double>(a.Slice(i, width)) - new System.Numerics.Vector<double>(b.Slice(i, width)))
                    .CopyTo(result.Slice(i, width));
        }
#endif
        for (; i < result.Length; i++) result[i] = a[i] - b[i];
    }

    internal static void Scale(ReadOnlySpan<double> a, double scalar, Span<double> result)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            for (; i <= result.Length - width; i += width)
                (new System.Numerics.Vector<double>(a.Slice(i, width)) * scalar).CopyTo(result.Slice(i, width));
        }
#endif
        for (; i < result.Length; i++) result[i] = a[i] * scalar;
    }

    internal static void Floor(ReadOnlySpan<double> input, Span<double> result)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            for (; i <= result.Length - width; i += width)
                System.Numerics.Vector.Floor(new System.Numerics.Vector<double>(input.Slice(i, width)))
                    .CopyTo(result.Slice(i, width));
        }
#endif
        for (; i < result.Length; i++) result[i] = Math.Floor(input[i]);
    }

    internal static void Ceiling(ReadOnlySpan<double> input, Span<double> result)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            for (; i <= result.Length - width; i += width)
                System.Numerics.Vector.Ceiling(new System.Numerics.Vector<double>(input.Slice(i, width)))
                    .CopyTo(result.Slice(i, width));
        }
#endif
        for (; i < result.Length; i++) result[i] = Math.Ceiling(input[i]);
    }

    internal static void Sqrt(ReadOnlySpan<double> input, Span<double> result)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            for (; i <= result.Length - width; i += width)
                System.Numerics.Vector.SquareRoot(new System.Numerics.Vector<double>(input.Slice(i, width)))
                    .CopyTo(result.Slice(i, width));
        }
#endif
        for (; i < result.Length; i++) result[i] = Math.Sqrt(input[i]);
    }

    internal static double Min(ReadOnlySpan<double> input)
    {
        var minimum = input[0];
        var i = 1;
#if !NETFRAMEWORK
        var width = System.Numerics.Vector<double>.Count;
        if (System.Numerics.Vector.IsHardwareAccelerated && input.Length >= width)
        {
            var lowest = new System.Numerics.Vector<double>(input.Slice(0, width));
            for (i = width; i <= input.Length - width; i += width)
                lowest = System.Numerics.Vector.Min(lowest, new System.Numerics.Vector<double>(input.Slice(i, width)));
            minimum = lowest[0];
            for (var lane = 1; lane < width; lane++) minimum = Math.Min(minimum, lowest[lane]);
        }
#endif
        for (; i < input.Length; i++) minimum = Math.Min(minimum, input[i]);
        return minimum;
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
        if (residual - pairResidual == sumResidual && lacking - remainder == residual // NOSONAR an exact tie is the contract
            && magnitude > 1e-290 && magnitude < 1e290
            && (BitConverter.DoubleToInt64Bits(result) & 0xFFFFFFFFFFFFFL) != 0
            && Math.Abs(left) < 0.4999999 * unit)
            return result;
#endif
        return Helpers.ExactTypicalPrice.Calculate(high, low, close);
    }
}
