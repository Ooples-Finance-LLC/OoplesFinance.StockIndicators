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
}
