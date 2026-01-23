using System;
using System.Buffers;

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

    /// <summary>
    /// Computes Historical Volatility (annualized standard deviation of log returns).
    /// </summary>
    internal static void HistoricalVolatility(ReadOnlySpan<double> close, Span<double> output, int length = 20, int annualizationFactor = 252)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var logReturnsArray = pool.Rent(close.Length);

        try
        {
            var logReturns = logReturnsArray.AsSpan(0, close.Length);

            // Calculate log returns
            logReturns[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                logReturns[i] = close[i - 1] != 0 ? Math.Log(close[i] / close[i - 1]) : 0;
            }

            // Calculate rolling standard deviation of log returns and annualize
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += logReturns[j];
                }
                var mean = sum / length;

                double variance = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    var diff = logReturns[j] - mean;
                    variance += diff * diff;
                }
                variance /= length;

                output[i] = Math.Sqrt(variance) * Math.Sqrt(annualizationFactor) * 100;
            }
        }
        finally
        {
            pool.Return(logReturnsArray);
        }
    }

    /// <summary>
    /// Computes Chaikin Volatility.
    /// </summary>
    internal static void ChaikinVolatility(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 10, int rocLength = 10)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rangeArray = pool.Rent(high.Length);
        var emaRangeArray = pool.Rent(high.Length);

        try
        {
            var range = rangeArray.AsSpan(0, high.Length);
            var emaRange = emaRangeArray.AsSpan(0, high.Length);

            // Calculate high-low range
            for (var i = 0; i < high.Length; i++)
            {
                range[i] = high[i] - low[i];
            }

            // EMA of range
            MovingAverageCore.ExponentialMovingAverage(range, emaRange, length);

            // Rate of change of EMA
            for (var i = 0; i < high.Length; i++)
            {
                if (i < rocLength)
                {
                    output[i] = 0;
                }
                else
                {
                    var prevEma = emaRange[i - rocLength];
                    output[i] = prevEma != 0 ? ((emaRange[i] - prevEma) / prevEma) * 100 : 0;
                }
            }
        }
        finally
        {
            pool.Return(rangeArray);
            pool.Return(emaRangeArray);
        }
    }

    /// <summary>
    /// Computes Ulcer Index.
    /// </summary>
    internal static void UlcerIndex(ReadOnlySpan<double> close, Span<double> output, int length = 14)
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

            // Find highest close in period
            var highest = double.MinValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (close[j] > highest) highest = close[j];
            }

            // Calculate sum of squared percentage drawdowns
            double sumSqDd = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var pctDrawdown = highest != 0 ? 100 * (close[j] - highest) / highest : 0;
                sumSqDd += pctDrawdown * pctDrawdown;
            }

            output[i] = Math.Sqrt(sumSqDd / length);
        }
    }

    /// <summary>
    /// Computes Normalized ATR (ATR as percentage of close).
    /// </summary>
    internal static void NormalizedAtr(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(close.Length);

        try
        {
            var atr = atrArray.AsSpan(0, close.Length);
            AverageTrueRange(high, low, close, atr, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] != 0 ? (atr[i] / close[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Variance.
    /// </summary>
    internal static void Variance(ReadOnlySpan<double> input, Span<double> output, int length = 20)
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

            output[i] = variance / length;
        }
    }

    /// <summary>
    /// Computes Coefficient of Variation.
    /// </summary>
    internal static void CoefficientOfVariation(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(input.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, input.Length);
            StandardDeviation(input, stdDev, length);

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

                output[i] = mean != 0 ? (stdDev[i] / mean) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Standard Error.
    /// </summary>
    internal static void StandardError(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(input.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, input.Length);
            StandardDeviation(input, stdDev, length);

            var sqrtLength = Math.Sqrt(length);
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = stdDev[i] / sqrtLength;
            }
        }
        finally
        {
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Keltner Channel Width.
    /// </summary>
    internal static void KeltnerChannelWidth(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 20, double multiplier = 2)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);
        var atrArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            var atr = atrArray.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, ema, length);
            AverageTrueRange(high, low, close, atr, length);

            for (var i = 0; i < close.Length; i++)
            {
                var upper = ema[i] + (multiplier * atr[i]);
                var lower = ema[i] - (multiplier * atr[i]);
                output[i] = ema[i] != 0 ? ((upper - lower) / ema[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Bollinger Bands Width.
    /// </summary>
    internal static void BollingerBandsWidth(ReadOnlySpan<double> close, Span<double> output, int length = 20, double multiplier = 2)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(close.Length);
        var stdDevArray = pool.Rent(close.Length);

        try
        {
            var sma = smaArray.AsSpan(0, close.Length);
            var stdDev = stdDevArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(close, sma, length);
            StandardDeviation(close, stdDev, length);

            for (var i = 0; i < close.Length; i++)
            {
                var upper = sma[i] + (multiplier * stdDev[i]);
                var lower = sma[i] - (multiplier * stdDev[i]);
                output[i] = sma[i] != 0 ? ((upper - lower) / sma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(smaArray);
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Relative Volatility Index.
    /// </summary>
    internal static void RelativeVolatilityIndex(ReadOnlySpan<double> close, Span<double> output, int length = 14, int stdDevLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(close.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, close.Length);
            StandardDeviation(close, stdDev, stdDevLength);

            double upSum = 0;
            double downSum = 0;

            for (var i = 1; i < close.Length; i++)
            {
                var change = close[i] - close[i - 1];
                var upStdDev = change > 0 ? stdDev[i] : 0;
                var downStdDev = change < 0 ? stdDev[i] : 0;

                if (i <= length)
                {
                    upSum += upStdDev;
                    downSum += downStdDev;
                }
                else
                {
                    upSum = upSum - (upSum / length) + upStdDev;
                    downSum = downSum - (downSum / length) + downStdDev;
                }

                var total = upSum + downSum;
                output[i] = total != 0 ? (upSum / total) * 100 : 50;
            }
            output[0] = 50;
        }
        finally
        {
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Donchian Channel Width.
    /// </summary>
    internal static void DonchianChannelWidth(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 20)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            var hh = high[i - length + 1];
            var ll = low[i - length + 1];
            for (var j = i - length + 2; j <= i; j++)
            {
                if (high[j] > hh) hh = high[j];
                if (low[j] < ll) ll = low[j];
            }

            output[i] = hh - ll;
        }
    }

    /// <summary>
    /// Computes Mass Index.
    /// </summary>
    internal static void MassIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 25, int emaLength = 9)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rangeArray = pool.Rent(high.Length);
        var ema1Array = pool.Rent(high.Length);
        var ema2Array = pool.Rent(high.Length);
        var ratioArray = pool.Rent(high.Length);

        try
        {
            var range = rangeArray.AsSpan(0, high.Length);
            var ema1 = ema1Array.AsSpan(0, high.Length);
            var ema2 = ema2Array.AsSpan(0, high.Length);
            var ratio = ratioArray.AsSpan(0, high.Length);

            for (var i = 0; i < high.Length; i++)
            {
                range[i] = high[i] - low[i];
            }

            MovingAverageCore.ExponentialMovingAverage(range, ema1, emaLength);
            MovingAverageCore.ExponentialMovingAverage(ema1, ema2, emaLength);

            for (var i = 0; i < high.Length; i++)
            {
                ratio[i] = ema2[i] != 0 ? ema1[i] / ema2[i] : 1;
            }

            for (var i = 0; i < high.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += ratio[j];
                }
                output[i] = sum;
            }
        }
        finally
        {
            pool.Return(rangeArray);
            pool.Return(ema1Array);
            pool.Return(ema2Array);
            pool.Return(ratioArray);
        }
    }

    /// <summary>
    /// Computes Close-to-Close Volatility.
    /// </summary>
    internal static void CloseToCloseVolatility(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var returnsArray = pool.Rent(close.Length);

        try
        {
            var returns = returnsArray.AsSpan(0, close.Length);

            returns[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                returns[i] = close[i - 1] != 0 ? Math.Log(close[i] / close[i - 1]) : 0;
            }

            StandardDeviation(returns, output, length);

            // Annualize (assuming 252 trading days)
            var annFactor = Math.Sqrt(252);
            for (var i = 0; i < close.Length; i++)
            {
                output[i] *= annFactor;
            }
        }
        finally
        {
            pool.Return(returnsArray);
        }
    }

    /// <summary>
    /// Computes Parkinson Volatility.
    /// </summary>
    internal static void ParkinsonVolatility(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 20)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var factor = 1 / (4 * length * Math.Log(2));
        var sqrtFactor = Math.Sqrt(252);

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var logRatio = low[j] != 0 ? Math.Log(high[j] / low[j]) : 0;
                sum += logRatio * logRatio;
            }

            output[i] = Math.Sqrt(factor * sum) * sqrtFactor;
        }
    }

    /// <summary>
    /// Computes Garman-Klass Volatility.
    /// </summary>
    internal static void GarmanKlassVolatility(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrtFactor = Math.Sqrt(252);

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var logHL = low[j] != 0 ? Math.Log(high[j] / low[j]) : 0;
                var logCO = open[j] != 0 ? Math.Log(close[j] / open[j]) : 0;

                var term = 0.5 * logHL * logHL - (2 * Math.Log(2) - 1) * logCO * logCO;
                sum += term;
            }

            output[i] = Math.Sqrt(sum / length) * sqrtFactor;
        }
    }

    /// <summary>
    /// Computes Rogers-Satchell Volatility.
    /// More accurate for trending markets than close-to-close volatility.
    /// </summary>
    internal static void RogersSatchellVolatility(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrtFactor = Math.Sqrt(252);

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var logHC = close[j] != 0 ? Math.Log(high[j] / close[j]) : 0;
                var logHO = open[j] != 0 ? Math.Log(high[j] / open[j]) : 0;
                var logLC = close[j] != 0 ? Math.Log(low[j] / close[j]) : 0;
                var logLO = open[j] != 0 ? Math.Log(low[j] / open[j]) : 0;

                sum += logHC * logHO + logLC * logLO;
            }

            output[i] = Math.Sqrt(sum / length) * sqrtFactor;
        }
    }

    /// <summary>
    /// Computes Yang-Zhang Volatility.
    /// Combines open, high, low, close for robust volatility estimate.
    /// </summary>
    internal static void YangZhangVolatility(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrtFactor = Math.Sqrt(252);
        var k = 0.34 / (1 + (double)(length + 1) / (length - 1));

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            // Overnight volatility
            double overnightSum = 0;
            double overnightMean = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (j > 0 && close[j - 1] != 0)
                {
                    var logOC = Math.Log(open[j] / close[j - 1]);
                    overnightMean += logOC;
                }
            }
            overnightMean /= length;

            for (var j = i - length + 1; j <= i; j++)
            {
                if (j > 0 && close[j - 1] != 0)
                {
                    var logOC = Math.Log(open[j] / close[j - 1]);
                    overnightSum += (logOC - overnightMean) * (logOC - overnightMean);
                }
            }
            var overnightVar = overnightSum / (length - 1);

            // Open-to-close volatility
            double ocSum = 0;
            double ocMean = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (open[j] != 0)
                {
                    var logCO = Math.Log(close[j] / open[j]);
                    ocMean += logCO;
                }
            }
            ocMean /= length;

            for (var j = i - length + 1; j <= i; j++)
            {
                if (open[j] != 0)
                {
                    var logCO = Math.Log(close[j] / open[j]);
                    ocSum += (logCO - ocMean) * (logCO - ocMean);
                }
            }
            var openToCloseVar = ocSum / (length - 1);

            // Rogers-Satchell volatility
            double rsSum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var logHC = close[j] != 0 ? Math.Log(high[j] / close[j]) : 0;
                var logHO = open[j] != 0 ? Math.Log(high[j] / open[j]) : 0;
                var logLC = close[j] != 0 ? Math.Log(low[j] / close[j]) : 0;
                var logLO = open[j] != 0 ? Math.Log(low[j] / open[j]) : 0;
                rsSum += logHC * logHO + logLC * logLO;
            }
            var rsVar = rsSum / length;

            // Yang-Zhang formula
            var yzVar = overnightVar + k * openToCloseVar + (1 - k) * rsVar;
            output[i] = Math.Sqrt(yzVar) * sqrtFactor;
        }
    }

    /// <summary>
    /// Computes Calmar Ratio.
    /// Risk-adjusted return measure.
    /// </summary>
    internal static void CalmarRatio(ReadOnlySpan<double> close, Span<double> output, int length = 252)
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

            // Calculate annualized return
            var startPrice = close[i - length + 1];
            var totalReturn = startPrice > 0 ? (close[i] - startPrice) / startPrice : 0;
            var annualizedReturn = totalReturn; // Assume length is already 252 trading days

            // Calculate maximum drawdown
            double maxDrawdown = 0;
            double peak = close[i - length + 1];
            for (var j = i - length + 1; j <= i; j++)
            {
                peak = Math.Max(peak, close[j]);
                var drawdown = peak > 0 ? (peak - close[j]) / peak : 0;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }

            output[i] = maxDrawdown > 0 ? annualizedReturn / maxDrawdown : 0;
        }
    }

    /// <summary>
    /// Computes Sortino Ratio component - Downside Deviation.
    /// </summary>
    internal static void DownsideDeviation(ReadOnlySpan<double> close, Span<double> output, int length = 20, double targetReturn = 0)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            double sumSquaredDownside = 0;
            var count = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var ret = close[j - 1] > 0 ? (close[j] - close[j - 1]) / close[j - 1] : 0;
                if (ret < targetReturn)
                {
                    sumSquaredDownside += (ret - targetReturn) * (ret - targetReturn);
                    count++;
                }
            }

            output[i] = count > 0 ? Math.Sqrt(sumSquaredDownside / count) : 0;
        }
    }

    /// <summary>
    /// Computes Average Day Range.
    /// Average of high-low range.
    /// </summary>
    internal static void AverageDayRange(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rangeArray = pool.Rent(high.Length);

        try
        {
            var range = rangeArray.AsSpan(0, high.Length);

            for (var i = 0; i < high.Length; i++)
            {
                range[i] = high[i] - low[i];
            }

            MovingAverageCore.SimpleMovingAverage(range, output, length);
        }
        finally
        {
            pool.Return(rangeArray);
        }
    }

    /// <summary>
    /// Computes ATR Channel Width.
    /// Distance between upper and lower ATR bands.
    /// </summary>
    internal static void AtrChannelWidth(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14, double multiplier = 2)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(close.Length);

        try
        {
            var atr = atrArray.AsSpan(0, close.Length);
            AverageTrueRange(high, low, close, atr, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = 2 * multiplier * atr[i];
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Commodity Selection Index.
    /// Measures trending potential of a commodity.
    /// </summary>
    internal static void CommoditySelectionIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14, double pointValue = 1, double margin = 1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(close.Length);
        var adxArray = pool.Rent(close.Length);

        try
        {
            var atr = atrArray.AsSpan(0, close.Length);
            var adx = adxArray.AsSpan(0, close.Length);

            AverageTrueRange(high, low, close, atr, length);
            OscillatorCore.AverageDirectionalIndex(high, low, close, adx, length);

            for (var i = 0; i < close.Length; i++)
            {
                var k = margin > 0 ? 100 * pointValue / Math.Sqrt(margin) : 0;
                output[i] = k * atr[i] * adx[i] / 100;
            }
        }
        finally
        {
            pool.Return(atrArray);
            pool.Return(adxArray);
        }
    }
}
