using System;
using System.Buffers;

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

    /// <summary>
    /// Computes Double Exponential Moving Average (DEMA = 2*EMA - EMA(EMA)).
    /// </summary>
    internal static void DoubleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);
        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);

            // First EMA
            ExponentialMovingAverage(input, ema1, length);

            // Second EMA (EMA of EMA)
            ExponentialMovingAverage(ema1, ema2, length);

            // DEMA = 2*EMA - EMA(EMA)
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (2 * ema1[i]) - ema2[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
        }
    }

    /// <summary>
    /// Computes Triple Exponential Moving Average (TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))).
    /// </summary>
    internal static void TripleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);
        var ema3Array = pool.Rent(input.Length);
        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);
            var ema3 = ema3Array.AsSpan(0, input.Length);

            // First EMA
            ExponentialMovingAverage(input, ema1, length);

            // Second EMA (EMA of EMA)
            ExponentialMovingAverage(ema1, ema2, length);

            // Third EMA (EMA of EMA of EMA)
            ExponentialMovingAverage(ema2, ema3, length);

            // TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (3 * ema1[i]) - (3 * ema2[i]) + ema3[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
            pool.Return(ema3Array);
        }
    }

    /// <summary>
    /// Computes Hull Moving Average (HMA = WMA(2*WMA(n/2) - WMA(n), sqrt(n))).
    /// </summary>
    internal static void HullMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = Math.Max(length / 2, 1);
        var sqrtLength = Math.Max((int)Math.Sqrt(length), 1);

        var pool = ArrayPool<double>.Shared;
        var wmaHalfArray = pool.Rent(input.Length);
        var wmaFullArray = pool.Rent(input.Length);
        var diffArray = pool.Rent(input.Length);
        try
        {
            var wmaHalf = wmaHalfArray.AsSpan(0, input.Length);
            var wmaFull = wmaFullArray.AsSpan(0, input.Length);
            var diff = diffArray.AsSpan(0, input.Length);

            // WMA with half period
            WeightedMovingAverage(input, wmaHalf, halfLength);

            // WMA with full period
            WeightedMovingAverage(input, wmaFull, length);

            // 2*WMA(n/2) - WMA(n)
            for (var i = 0; i < input.Length; i++)
            {
                diff[i] = (2 * wmaHalf[i]) - wmaFull[i];
            }

            // Final WMA with sqrt period
            WeightedMovingAverage(diff, output, sqrtLength);
        }
        finally
        {
            pool.Return(wmaHalfArray);
            pool.Return(wmaFullArray);
            pool.Return(diffArray);
        }
    }

    /// <summary>
    /// Computes Triangular Moving Average (TMA = SMA of SMA).
    /// </summary>
    internal static void TriangularMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = (length + 1) / 2;

        var pool = ArrayPool<double>.Shared;
        var sma1Array = pool.Rent(input.Length);
        try
        {
            var sma1 = sma1Array.AsSpan(0, input.Length);

            // First SMA
            SimpleMovingAverage(input, sma1, halfLength);

            // Second SMA (SMA of SMA)
            SimpleMovingAverage(sma1, output, halfLength);
        }
        finally
        {
            pool.Return(sma1Array);
        }
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average.
    /// </summary>
    internal static void VolumeWeightedMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length)
    {
        if (output.Length < price.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double pvSum = 0;
        double vSum = 0;

        for (var i = 0; i < price.Length; i++)
        {
            pvSum += price[i] * volume[i];
            vSum += volume[i];

            if (i >= length)
            {
                pvSum -= price[i - length] * volume[i - length];
                vSum -= volume[i - length];
            }

            output[i] = vSum != 0 ? pvSum / vSum : 0;
        }
    }

    /// <summary>
    /// Computes Linear Regression (Least Squares Moving Average).
    /// </summary>
    internal static void LinearRegression(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var j = 0; j < length; j++)
            {
                var x = j;
                var y = input[i - length + 1 + j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var denominator = (length * sumX2) - (sumX * sumX);
            if (denominator == 0)
            {
                output[i] = sumY / length;
            }
            else
            {
                var slope = ((length * sumXY) - (sumX * sumY)) / denominator;
                var intercept = (sumY - (slope * sumX)) / length;
                output[i] = intercept + (slope * (length - 1));
            }
        }
    }

    /// <summary>
    /// Computes Kaufman's Adaptive Moving Average (KAMA).
    /// </summary>
    internal static void KaufmanAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length, int fastLength = 2, int slowLength = 30)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var fastSc = 2.0 / (fastLength + 1);
        var slowSc = 2.0 / (slowLength + 1);
        double prevKama = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                prevKama = input[i];
                output[i] = prevKama;
                continue;
            }

            // Calculate efficiency ratio
            var change = Math.Abs(input[i] - input[i - length]);
            double volatility = 0;
            for (var j = 0; j < length; j++)
            {
                volatility += Math.Abs(input[i - j] - input[i - j - 1]);
            }

            var er = volatility != 0 ? change / volatility : 0;
            var sc = Math.Pow((er * (fastSc - slowSc)) + slowSc, 2);

            var kama = prevKama + (sc * (input[i] - prevKama));
            output[i] = kama;
            prevKama = kama;
        }
    }

    /// <summary>
    /// Computes Zero-Lag Exponential Moving Average.
    /// </summary>
    internal static void ZeroLagEma(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var lag = (length - 1) / 2;

        var pool = ArrayPool<double>.Shared;
        var adjustedArray = pool.Rent(input.Length);
        try
        {
            var adjusted = adjustedArray.AsSpan(0, input.Length);

            // Create lag-adjusted series
            for (var i = 0; i < input.Length; i++)
            {
                var lagIdx = Math.Max(i - lag, 0);
                adjusted[i] = (2 * input[i]) - input[lagIdx];
            }

            // Final EMA on adjusted series
            ExponentialMovingAverage(adjusted, output, length);
        }
        finally
        {
            pool.Return(adjustedArray);
        }
    }
}
