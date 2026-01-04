using System;

namespace OoplesFinance.StockIndicators.Core;

internal static class MovingAverageCore
{
    internal static void SimpleMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            sum += input[i];
            if (i >= length)
            {
                sum -= input[i - length];
            }

            output[i] = i >= length - 1 ? sum / length : 0;
        }
    }

    internal static void WeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double numerator = 0;
        double windowSum = 0;
        var weightedSumDenominator = (double)length * (length + 1) / 2;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            numerator += length * currentValue - windowSum;
            windowSum += currentValue;

            if (i >= length)
            {
                windowSum -= input[i - length];
            }

            output[i] = numerator / weightedSumDenominator;
        }
    }

    internal static void ExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = Math.Min(Math.Max((double)2 / (length + 1), 0.01), 0.99);
        double sum = 0;
        double prevEma = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            if (i < length)
            {
                sum += currentValue;
                var ema = sum / (i + 1);
                output[i] = ema;
                prevEma = ema;
            }
            else
            {
                var ema = (currentValue * k) + (prevEma * (1 - k));
                output[i] = ema;
                prevEma = ema;
            }
        }
    }

    internal static void WellesWilderMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = (double)1 / length;
        double prevWwma = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var wwma = (input[i] * k) + (prevWwma * (1 - k));
            output[i] = wwma;
            prevWwma = wwma;
        }
    }
}
