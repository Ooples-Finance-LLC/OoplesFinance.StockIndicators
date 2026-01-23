using System;

namespace OoplesFinance.StockIndicators.Core;

/// <summary>
/// Core span-based implementations for volatility indicators.
/// Zero-allocation computation directly into output spans.
/// </summary>
internal static class VolatilityCore
{
    /// <summary>
    /// Computes True Range for each bar.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for True Range values.</param>
    internal static void TrueRange(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        output[0] = high[0] - low[0];

        for (var i = 1; i < close.Length; i++)
        {
            var prevClose = close[i - 1];
            var highLow = high[i] - low[i];
            var highClose = Math.Abs(high[i] - prevClose);
            var lowClose = Math.Abs(low[i] - prevClose);

            output[i] = Math.Max(highLow, Math.Max(highClose, lowClose));
        }
    }

    /// <summary>
    /// Computes Average True Range using Wilder's smoothing.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for ATR values.</param>
    /// <param name="length">ATR period (default 14).</param>
    internal static void AverageTrueRange(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        var k = 1.0 / length;
        double prevAtr = 0;
        double trSum = 0;

        // First bar: TR = High - Low
        var tr0 = high[0] - low[0];
        trSum = tr0;
        output[0] = 0;

        for (var i = 1; i < close.Length; i++)
        {
            var prevClose = close[i - 1];
            var highLow = high[i] - low[i];
            var highClose = Math.Abs(high[i] - prevClose);
            var lowClose = Math.Abs(low[i] - prevClose);
            var tr = Math.Max(highLow, Math.Max(highClose, lowClose));

            if (i < length)
            {
                // Build up initial sum
                trSum += tr;
                output[i] = 0;
            }
            else if (i == length)
            {
                // First ATR value is simple average
                trSum += tr;
                prevAtr = trSum / (length + 1);
                output[i] = prevAtr;
            }
            else
            {
                // Wilder's smoothing
                prevAtr = (tr * k) + (prevAtr * (1 - k));
                output[i] = prevAtr;
            }
        }
    }

    /// <summary>
    /// Computes Standard Deviation of price series.
    /// </summary>
    /// <param name="input">Price series.</param>
    /// <param name="output">Output span for StdDev values.</param>
    /// <param name="length">Period (default 20).</param>
    internal static void StandardDeviation(ReadOnlySpan<double> input, Span<double> output, int length)
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

            // Calculate mean
            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }
            var mean = sum / length;

            // Calculate variance
            double variance = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var diff = input[j] - mean;
                variance += diff * diff;
            }
            variance /= length;

            output[i] = Math.Sqrt(variance);
        }
    }

    /// <summary>
    /// Computes Bollinger Bands.
    /// </summary>
    /// <param name="input">Price series.</param>
    /// <param name="upper">Output span for upper band.</param>
    /// <param name="middle">Output span for middle band (SMA).</param>
    /// <param name="lower">Output span for lower band.</param>
    /// <param name="length">SMA period (default 20).</param>
    /// <param name="multiplier">Standard deviation multiplier (default 2).</param>
    internal static void BollingerBands(ReadOnlySpan<double> input, Span<double> upper, Span<double> middle, Span<double> lower, int length, double multiplier)
    {
        if (upper.Length < input.Length || middle.Length < input.Length || lower.Length < input.Length)
        {
            throw new ArgumentException("Output spans must be at least input length.");
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                upper[i] = 0;
                middle[i] = 0;
                lower[i] = 0;
                continue;
            }

            // Calculate SMA
            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }
            var sma = sum / length;

            // Calculate Standard Deviation
            double variance = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var diff = input[j] - sma;
                variance += diff * diff;
            }
            var stdDev = Math.Sqrt(variance / length);

            middle[i] = sma;
            upper[i] = sma + (multiplier * stdDev);
            lower[i] = sma - (multiplier * stdDev);
        }
    }
}
