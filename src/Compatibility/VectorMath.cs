using System;
#if NET8_0_OR_GREATER
using System.Numerics;
#endif

namespace OoplesFinance.StockIndicators.Compatibility;

internal static class VectorMath
{
    internal static double Sum(ReadOnlySpan<double> input)
    {
#if NET8_0_OR_GREATER
        if (Vector.IsHardwareAccelerated && input.Length >= Vector<double>.Count)
        {
            var vectorCount = Vector<double>.Count;
            var sumVector = Vector<double>.Zero;
            var i = 0;

            for (; i <= input.Length - vectorCount; i += vectorCount)
            {
                var v = new Vector<double>(input.Slice(i, vectorCount));
                sumVector += v;
            }

            double sum = 0;
            for (var j = 0; j < vectorCount; j++)
            {
                sum += sumVector[j];
            }

            for (; i < input.Length; i++)
            {
                sum += input[i];
            }

            return sum;
        }
#endif

        double scalarSum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            scalarSum += input[i];
        }

        return scalarSum;
    }

    internal static void Scale(ReadOnlySpan<double> input, Span<double> output, double scale)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

#if NET8_0_OR_GREATER
        if (Vector.IsHardwareAccelerated && input.Length >= Vector<double>.Count)
        {
            var vectorCount = Vector<double>.Count;
            var scaleVector = new Vector<double>(scale);
            var i = 0;

            for (; i <= input.Length - vectorCount; i += vectorCount)
            {
                var v = new Vector<double>(input.Slice(i, vectorCount));
                (v * scaleVector).CopyTo(output.Slice(i, vectorCount));
            }

            for (; i < input.Length; i++)
            {
                output[i] = input[i] * scale;
            }

            return;
        }
#endif

        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * scale;
        }
    }

    internal static void Diff(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> output)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Input spans must have the same length.");
        }

        if (output.Length < left.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

#if NET8_0_OR_GREATER
        if (Vector.IsHardwareAccelerated && left.Length >= Vector<double>.Count)
        {
            var vectorCount = Vector<double>.Count;
            var i = 0;

            for (; i <= left.Length - vectorCount; i += vectorCount)
            {
                var vLeft = new Vector<double>(left.Slice(i, vectorCount));
                var vRight = new Vector<double>(right.Slice(i, vectorCount));
                (vLeft - vRight).CopyTo(output.Slice(i, vectorCount));
            }

            for (; i < left.Length; i++)
            {
                output[i] = left[i] - right[i];
            }

            return;
        }
#endif

        for (var i = 0; i < left.Length; i++)
        {
            output[i] = left[i] - right[i];
        }
    }

    internal static void Clamp(ReadOnlySpan<double> input, Span<double> output, double min, double max)
    {
        if (min > max)
        {
            throw new ArgumentException("Min must be less than or equal to max.");
        }

        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

#if NET8_0_OR_GREATER
        if (Vector.IsHardwareAccelerated && input.Length >= Vector<double>.Count)
        {
            var vectorCount = Vector<double>.Count;
            var minVector = new Vector<double>(min);
            var maxVector = new Vector<double>(max);
            var i = 0;

            for (; i <= input.Length - vectorCount; i += vectorCount)
            {
                var v = new Vector<double>(input.Slice(i, vectorCount));
                v = Vector.Min(Vector.Max(v, minVector), maxVector);
                v.CopyTo(output.Slice(i, vectorCount));
            }

            for (; i < input.Length; i++)
            {
                var value = input[i];
                output[i] = value < min ? min : value > max ? max : value;
            }

            return;
        }
#endif

        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            output[i] = value < min ? min : value > max ? max : value;
        }
    }
}
