using System;

namespace OoplesFinance.StockIndicators.Core;

/// <summary>
/// Core span-based implementations for oscillator indicators.
/// Zero-allocation computation directly into output spans.
/// </summary>
internal static class OscillatorCore
{
    /// <summary>
    /// Computes Relative Strength Index using Wilder's smoothing.
    /// </summary>
    /// <param name="input">Price series (typically close prices).</param>
    /// <param name="output">Output span for RSI values (0-100 scale).</param>
    /// <param name="length">RSI period (default 14).</param>
    internal static void RelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0)
        {
            return;
        }

        var k = 1.0 / length;
        double avgGain = 0;
        double avgLoss = 0;
        double prevValue = input[0];
        output[0] = 0;

        for (var i = 1; i < input.Length; i++)
        {
            var currentValue = input[i];
            var change = currentValue - prevValue;
            prevValue = currentValue;

            var gain = change > 0 ? change : 0;
            var loss = change < 0 ? -change : 0;

            if (i <= length)
            {
                // Initial period: simple average
                avgGain += gain;
                avgLoss += loss;

                if (i == length)
                {
                    avgGain /= length;
                    avgLoss /= length;
                }

                output[i] = 0;
            }
            else
            {
                // After initial period: Wilder's smoothing
                avgGain = (gain * k) + (avgGain * (1 - k));
                avgLoss = (loss * k) + (avgLoss * (1 - k));

                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
                output[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + rs));
            }
        }
    }

    /// <summary>
    /// Computes Rate of Change (percentage change over period).
    /// </summary>
    /// <param name="input">Price series.</param>
    /// <param name="output">Output span for ROC values.</param>
    /// <param name="length">ROC period (default 12).</param>
    internal static void RateOfChange(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
            }
            else
            {
                var prevValue = input[i - length];
                output[i] = prevValue != 0 ? ((input[i] - prevValue) / prevValue) * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Momentum (simple difference over period).
    /// </summary>
    /// <param name="input">Price series.</param>
    /// <param name="output">Output span for Momentum values.</param>
    /// <param name="length">Momentum period (default 10).</param>
    internal static void Momentum(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            output[i] = i >= length ? input[i] - input[i - length] : 0;
        }
    }

    /// <summary>
    /// Computes Williams %R.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for Williams %R values (-100 to 0).</param>
    /// <param name="length">Period (default 14).</param>
    internal static void WilliamsR(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Find highest high and lowest low in the period
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var range = highestHigh - lowestLow;
            output[i] = range != 0 ? -100 * (highestHigh - close[i]) / range : 0;
        }
    }

    /// <summary>
    /// Computes Commodity Channel Index.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for CCI values.</param>
    /// <param name="length">Period (default 20).</param>
    internal static void CommodityChannelIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        const double constant = 0.015;

        for (var i = 0; i < close.Length; i++)
        {
            // Typical Price
            var tp = (high[i] + low[i] + close[i]) / 3;

            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Calculate SMA of TP
            double tpSum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                tpSum += (high[j] + low[j] + close[j]) / 3;
            }
            var smaTP = tpSum / length;

            // Calculate Mean Deviation
            double meanDev = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                meanDev += Math.Abs((high[j] + low[j] + close[j]) / 3 - smaTP);
            }
            meanDev /= length;

            output[i] = meanDev != 0 ? (tp - smaTP) / (constant * meanDev) : 0;
        }
    }
}
