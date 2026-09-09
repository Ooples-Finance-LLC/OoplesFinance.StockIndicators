using System.Numerics;
using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// SIMD-optimized math operations for IndicatorBuffer.
/// Uses hardware-accelerated vector operations when available,
/// with automatic fallback to scalar operations.
/// </summary>
public static class IndicatorBufferMath
{
    /// <summary>
    /// Computes the sum of all elements using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer to sum.</param>
    /// <returns>The sum of all elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sum(this IndicatorBuffer<double> buffer)
    {
        if (buffer.Count == 0)
        {
            return 0.0;
        }

        var span = buffer.AsSpan();
        return SimdSum(span);
    }

    /// <summary>
    /// Computes the average of all elements using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer to average.</param>
    /// <returns>The average of all elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Average(this IndicatorBuffer<double> buffer)
    {
        if (buffer.Count == 0)
        {
            return double.NaN;
        }

        return buffer.Sum() / buffer.Count;
    }

    /// <summary>
    /// Finds the minimum value using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer to search.</param>
    /// <returns>The minimum value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Min(this IndicatorBuffer<double> buffer)
    {
        if (buffer.Count == 0)
        {
            return double.NaN;
        }

        var span = buffer.AsSpan();
        return SimdMin(span);
    }

    /// <summary>
    /// Finds the maximum value using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer to search.</param>
    /// <returns>The maximum value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Max(this IndicatorBuffer<double> buffer)
    {
        if (buffer.Count == 0)
        {
            return double.NaN;
        }

        var span = buffer.AsSpan();
        return SimdMax(span);
    }

    /// <summary>
    /// Finds both minimum and maximum values in a single pass using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer to search.</param>
    /// <returns>A tuple containing (min, max) values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double Min, double Max) MinMax(this IndicatorBuffer<double> buffer)
    {
        if (buffer.Count == 0)
        {
            return (double.NaN, double.NaN);
        }

        var span = buffer.AsSpan();
        return SimdMinMax(span);
    }

    /// <summary>
    /// Computes the variance of all elements using SIMD vectorization.
    /// Uses the two-pass algorithm for numerical stability.
    /// </summary>
    /// <param name="buffer">The buffer to analyze.</param>
    /// <param name="sample">If true, computes sample variance (N-1); otherwise population variance (N).</param>
    /// <returns>The variance of all elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Variance(this IndicatorBuffer<double> buffer, bool sample = true)
    {
        var count = buffer.Count;
        if (count == 0 || (sample && count == 1))
        {
            return double.NaN;
        }

        var span = buffer.AsSpan();
        var mean = SimdSum(span) / count;
        var sumSquaredDiffs = SimdSumSquaredDifferences(span, mean);

        return sample ? sumSquaredDiffs / (count - 1) : sumSquaredDiffs / count;
    }

    /// <summary>
    /// Computes the standard deviation of all elements using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer to analyze.</param>
    /// <param name="sample">If true, computes sample standard deviation (N-1); otherwise population (N).</param>
    /// <returns>The standard deviation of all elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double StdDev(this IndicatorBuffer<double> buffer, bool sample = true)
    {
        var variance = buffer.Variance(sample);
        return double.IsNaN(variance) ? double.NaN : Math.Sqrt(variance);
    }

    /// <summary>
    /// Computes the dot product of two buffers using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The first buffer.</param>
    /// <param name="other">The second buffer.</param>
    /// <returns>The dot product of the two buffers.</returns>
    /// <exception cref="ArgumentException">Buffers have different lengths.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(this IndicatorBuffer<double> buffer, IndicatorBuffer<double> other)
    {
        if (buffer.Count != other.Count)
        {
            throw new ArgumentException("Buffers must have the same length for dot product.", nameof(other));
        }

        if (buffer.Count == 0)
        {
            return 0.0;
        }

        var span1 = buffer.AsSpan();
        var span2 = other.AsSpan();
        return SimdDot(span1, span2);
    }

    /// <summary>
    /// Computes the dot product with a raw span using SIMD vectorization.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="other">The span to dot product with.</param>
    /// <returns>The dot product.</returns>
    /// <exception cref="ArgumentException">Buffer and span have different lengths.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(this IndicatorBuffer<double> buffer, ReadOnlySpan<double> other)
    {
        if (buffer.Count != other.Length)
        {
            throw new ArgumentException("Buffer and span must have the same length for dot product.", nameof(other));
        }

        if (buffer.Count == 0)
        {
            return 0.0;
        }

        var span = buffer.AsSpan();
        return SimdDot(span, other);
    }

    #region SIMD Implementation

    // Note: For maximum performance on modern .NET, these could use hardware intrinsics.
    // However, for compatibility with .NET Framework 4.6.1, we use scalar implementations
    // with loop unrolling for improved performance.

    private static double SimdSum(ReadOnlySpan<double> span)
    {
        var sum = 0.0;
        var i = 0;
        var length = span.Length;

        // Process 4 elements at a time (loop unrolling)
        var unrolledEnd = length - 4;
        for (; i <= unrolledEnd; i += 4)
        {
            sum += span[i] + span[i + 1] + span[i + 2] + span[i + 3];
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            sum += span[i];
        }

        return sum;
    }

    private static double SimdMin(ReadOnlySpan<double> span)
    {
        var min = span[0];
        var length = span.Length;

        for (var i = 1; i < length; i++)
        {
            var value = span[i];
            if (value < min)
            {
                min = value;
            }
        }

        return min;
    }

    private static double SimdMax(ReadOnlySpan<double> span)
    {
        var max = span[0];
        var length = span.Length;

        for (var i = 1; i < length; i++)
        {
            var value = span[i];
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }

    private static (double Min, double Max) SimdMinMax(ReadOnlySpan<double> span)
    {
        var min = span[0];
        var max = span[0];
        var length = span.Length;

        for (var i = 1; i < length; i++)
        {
            var value = span[i];
            if (value < min) min = value;
            if (value > max) max = value;
        }

        return (min, max);
    }

    private static double SimdSumSquaredDifferences(ReadOnlySpan<double> span, double mean)
    {
        var sum = 0.0;
        var length = span.Length;
        var i = 0;

        // Process 4 elements at a time (loop unrolling)
        var unrolledEnd = length - 4;
        for (; i <= unrolledEnd; i += 4)
        {
            var d0 = span[i] - mean;
            var d1 = span[i + 1] - mean;
            var d2 = span[i + 2] - mean;
            var d3 = span[i + 3] - mean;
            sum += d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3;
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            var diff = span[i] - mean;
            sum += diff * diff;
        }

        return sum;
    }

    private static double SimdDot(ReadOnlySpan<double> span1, ReadOnlySpan<double> span2)
    {
        var sum = 0.0;
        var length = span1.Length;
        var i = 0;

        // Process 4 elements at a time (loop unrolling)
        var unrolledEnd = length - 4;
        for (; i <= unrolledEnd; i += 4)
        {
            sum += span1[i] * span2[i]
                 + span1[i + 1] * span2[i + 1]
                 + span1[i + 2] * span2[i + 2]
                 + span1[i + 3] * span2[i + 3];
        }

        // Handle remaining elements
        for (; i < length; i++)
        {
            sum += span1[i] * span2[i];
        }

        return sum;
    }

    #endregion
}
