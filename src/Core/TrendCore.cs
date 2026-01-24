using System;
using System.Buffers;

namespace OoplesFinance.StockIndicators.Core;

/// <summary>
/// Core span-based implementations for trend indicators.
/// Zero-allocation computation directly into output spans.
/// </summary>
internal static class TrendCore
{
    /// <summary>
    /// Computes Parabolic SAR.
    /// </summary>
    internal static void ParabolicSar(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, double afStart = 0.02, double afStep = 0.02, double afMax = 0.2)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (high.Length == 0)
        {
            return;
        }

        var isLong = true;
        var af = afStart;
        var ep = high[0];
        var sar = low[0];

        output[0] = sar;

        for (var i = 1; i < high.Length; i++)
        {
            sar = sar + af * (ep - sar);

            if (isLong)
            {
                if (high[i] > ep)
                {
                    ep = high[i];
                    af = Math.Min(af + afStep, afMax);
                }

                if (low[i] < sar)
                {
                    isLong = false;
                    sar = ep;
                    ep = low[i];
                    af = afStart;
                }
                else
                {
                    sar = Math.Min(sar, Math.Min(low[i], i > 0 ? low[i - 1] : low[i]));
                }
            }
            else
            {
                if (low[i] < ep)
                {
                    ep = low[i];
                    af = Math.Min(af + afStep, afMax);
                }

                if (high[i] > sar)
                {
                    isLong = true;
                    sar = ep;
                    ep = high[i];
                    af = afStart;
                }
                else
                {
                    sar = Math.Max(sar, Math.Max(high[i], i > 0 ? high[i - 1] : high[i]));
                }
            }

            output[i] = sar;
        }
    }

    /// <summary>
    /// Computes SuperTrend indicator.
    /// </summary>
    internal static void SuperTrend(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 10, double multiplier = 3.0)
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
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            double upperBand = 0;
            double lowerBand = 0;
            double prevSuperTrend = 0;

            for (var i = 0; i < close.Length; i++)
            {
                var hl2 = (high[i] + low[i]) / 2;
                var currentUpper = hl2 + (multiplier * atr[i]);
                var currentLower = hl2 - (multiplier * atr[i]);

                if (i == 0)
                {
                    upperBand = currentUpper;
                    lowerBand = currentLower;
                    prevSuperTrend = upperBand;
                    output[i] = prevSuperTrend;
                    continue;
                }

                // Update bands
                lowerBand = currentLower > lowerBand || close[i - 1] < lowerBand ? currentLower : lowerBand;
                upperBand = currentUpper < upperBand || close[i - 1] > upperBand ? currentUpper : upperBand;

                // Determine trend
                if (prevSuperTrend == upperBand)
                {
                    if (close[i] > upperBand)
                    {
                        prevSuperTrend = lowerBand;
                    }
                    else
                    {
                        prevSuperTrend = upperBand;
                    }
                }
                else
                {
                    if (close[i] < lowerBand)
                    {
                        prevSuperTrend = upperBand;
                    }
                    else
                    {
                        prevSuperTrend = lowerBand;
                    }
                }

                output[i] = prevSuperTrend;
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Donchian Channel Middle.
    /// </summary>
    internal static void DonchianChannelMiddle(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 20)
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

            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;

            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            output[i] = (highestHigh + lowestLow) / 2;
        }
    }

    /// <summary>
    /// Computes Keltner Channel Middle (EMA).
    /// </summary>
    internal static void KeltnerChannelMiddle(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        MovingAverageCore.ExponentialMovingAverage(close, output, length);
    }

    /// <summary>
    /// Computes Highest High over period.
    /// </summary>
    internal static void HighestHigh(ReadOnlySpan<double> high, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            var highest = double.MinValue;
            var start = Math.Max(0, i - length + 1);
            for (var j = start; j <= i; j++)
            {
                if (high[j] > highest) highest = high[j];
            }
            output[i] = highest;
        }
    }

    /// <summary>
    /// Computes Lowest Low over period.
    /// </summary>
    internal static void LowestLow(ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < low.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < low.Length; i++)
        {
            var lowest = double.MaxValue;
            var start = Math.Max(0, i - length + 1);
            for (var j = start; j <= i; j++)
            {
                if (low[j] < lowest) lowest = low[j];
            }
            output[i] = lowest;
        }
    }

    /// <summary>
    /// Computes Average Day Range.
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
    /// Computes Typical Price.
    /// </summary>
    internal static void TypicalPrice(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (high[i] + low[i] + close[i]) / 3;
        }
    }

    /// <summary>
    /// Computes Median Price.
    /// </summary>
    internal static void MedianPrice(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            output[i] = (high[i] + low[i]) / 2;
        }
    }

    /// <summary>
    /// Computes Weighted Close Price.
    /// </summary>
    internal static void WeightedClose(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (high[i] + low[i] + (2 * close[i])) / 4;
        }
    }

    /// <summary>
    /// Computes Percentage Change.
    /// </summary>
    internal static void PercentageChange(ReadOnlySpan<double> input, Span<double> output, int length = 1)
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
                var prev = input[i - length];
                output[i] = prev != 0 ? ((input[i] - prev) / prev) * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Linear Regression Slope.
    /// </summary>
    internal static void LinearRegressionSlope(ReadOnlySpan<double> input, Span<double> output, int length = 14)
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
            output[i] = denominator != 0 ? ((length * sumXY) - (sumX * sumY)) / denominator : 0;
        }
    }

    /// <summary>
    /// Computes R-Squared (Coefficient of Determination).
    /// </summary>
    internal static void RSquared(ReadOnlySpan<double> input, Span<double> output, int length = 14)
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

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (var j = 0; j < length; j++)
            {
                var x = j;
                var y = input[i - length + 1 + j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                sumY2 += y * y;
            }

            var numerator = (length * sumXY) - (sumX * sumY);
            var denominator = Math.Sqrt(((length * sumX2) - (sumX * sumX)) * ((length * sumY2) - (sumY * sumY)));
            var r = denominator != 0 ? numerator / denominator : 0;
            output[i] = r * r;
        }
    }

    /// <summary>
    /// Computes Standard Error.
    /// </summary>
    internal static void StandardError(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var linRegArray = pool.Rent(input.Length);

        try
        {
            var linReg = linRegArray.AsSpan(0, input.Length);
            MovingAverageCore.LinearRegression(input, linReg, length);

            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double sumSqDiff = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    var diff = input[j] - linReg[i];
                    sumSqDiff += diff * diff;
                }

                output[i] = Math.Sqrt(sumSqDiff / length);
            }
        }
        finally
        {
            pool.Return(linRegArray);
        }
    }

    /// <summary>
    /// Computes Trend Detection using linear regression.
    /// Returns 1 for uptrend, -1 for downtrend, 0 for no trend.
    /// </summary>
    internal static void TrendDetection(ReadOnlySpan<double> input, Span<double> output, int length = 14, double threshold = 0.0)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var slopeArray = pool.Rent(input.Length);

        try
        {
            var slope = slopeArray.AsSpan(0, input.Length);
            LinearRegressionSlope(input, slope, length);

            for (var i = 0; i < input.Length; i++)
            {
                if (slope[i] > threshold)
                {
                    output[i] = 1;
                }
                else if (slope[i] < -threshold)
                {
                    output[i] = -1;
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(slopeArray);
        }
    }

    /// <summary>
    /// Computes Price Channel Middle.
    /// </summary>
    internal static void PriceChannelMiddle(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 20)
    {
        DonchianChannelMiddle(high, low, output, length);
    }

    /// <summary>
    /// Computes Vertical Horizontal Filter (VHF).
    /// </summary>
    internal static void VerticalHorizontalFilter(ReadOnlySpan<double> close, Span<double> output, int length = 28)
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

            var highest = double.MinValue;
            var lowest = double.MaxValue;
            double sumAbsChange = 0;

            for (var j = i - length + 1; j <= i; j++)
            {
                if (close[j] > highest) highest = close[j];
                if (close[j] < lowest) lowest = close[j];

                if (j > i - length + 1)
                {
                    sumAbsChange += Math.Abs(close[j] - close[j - 1]);
                }
            }

            var numerator = highest - lowest;
            output[i] = sumAbsChange != 0 ? numerator / sumAbsChange : 0;
        }
    }

    /// <summary>
    /// Computes ZigZag indicator.
    /// </summary>
    internal static void ZigZag(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, double deviation = 5)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (high.Length == 0) return;

        var lastPivotIdx = 0;
        var lastPivotVal = (high[0] + low[0]) / 2;
        var lastPivotIsHigh = true;
        var deviationPct = deviation / 100;

        for (var i = 0; i < high.Length; i++)
        {
            output[i] = lastPivotVal;
        }

        for (var i = 1; i < high.Length; i++)
        {
            if (lastPivotIsHigh)
            {
                // Looking for lower low
                if (low[i] < lastPivotVal * (1 - deviationPct))
                {
                    // New pivot found
                    for (var j = lastPivotIdx; j <= i; j++)
                    {
                        output[j] = lastPivotVal + (low[i] - lastPivotVal) * (j - lastPivotIdx) / (i - lastPivotIdx);
                    }
                    lastPivotIdx = i;
                    lastPivotVal = low[i];
                    lastPivotIsHigh = false;
                }
                else if (high[i] > lastPivotVal)
                {
                    // Extend the current pivot
                    lastPivotVal = high[i];
                    lastPivotIdx = i;
                }
            }
            else
            {
                // Looking for higher high
                if (high[i] > lastPivotVal * (1 + deviationPct))
                {
                    // New pivot found
                    for (var j = lastPivotIdx; j <= i; j++)
                    {
                        output[j] = lastPivotVal + (high[i] - lastPivotVal) * (j - lastPivotIdx) / (i - lastPivotIdx);
                    }
                    lastPivotIdx = i;
                    lastPivotVal = high[i];
                    lastPivotIsHigh = true;
                }
                else if (low[i] < lastPivotVal)
                {
                    // Extend the current pivot
                    lastPivotVal = low[i];
                    lastPivotIdx = i;
                }
            }
        }
    }

    /// <summary>
    /// Computes Chandelier Exit Long.
    /// </summary>
    internal static void ChandelierExitLong(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 22, double multiplier = 3)
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
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                var highestHigh = double.MinValue;
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (high[j] > highestHigh) highestHigh = high[j];
                }

                output[i] = highestHigh - multiplier * atr[i];
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Chandelier Exit Short.
    /// </summary>
    internal static void ChandelierExitShort(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 22, double multiplier = 3)
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
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                var lowestLow = double.MaxValue;
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (low[j] < lowestLow) lowestLow = low[j];
                }

                output[i] = lowestLow + multiplier * atr[i];
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Trend Intensity Index.
    /// </summary>
    internal static void TrendIntensityIndex(ReadOnlySpan<double> close, Span<double> output, int length = 30)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(close.Length);

        try
        {
            var sma = smaArray.AsSpan(0, close.Length);
            MovingAverageCore.SimpleMovingAverage(close, sma, length);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 50;
                    continue;
                }

                var upCount = 0;
                var downCount = 0;

                for (var j = i - length + 1; j <= i; j++)
                {
                    if (close[j] > sma[j])
                    {
                        upCount++;
                    }
                    else if (close[j] < sma[j])
                    {
                        downCount++;
                    }
                }

                var total = upCount + downCount;
                output[i] = total != 0 ? (double)upCount / total * 100 : 50;
            }
        }
        finally
        {
            pool.Return(smaArray);
        }
    }

    /// <summary>
    /// Computes Average Price.
    /// </summary>
    internal static void AveragePrice(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (open[i] + high[i] + low[i] + close[i]) / 4;
        }
    }

    /// <summary>
    /// Computes Pivot Points (Standard).
    /// </summary>
    internal static void PivotPoint(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (high[i] + low[i] + close[i]) / 3;
        }
    }

    /// <summary>
    /// Computes Range (High - Low).
    /// </summary>
    internal static void Range(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            output[i] = high[i] - low[i];
        }
    }

    /// <summary>
    /// Computes Price Momentum.
    /// </summary>
    internal static void PriceMomentum(ReadOnlySpan<double> close, Span<double> output, int length = 10)
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
            }
            else
            {
                output[i] = close[i] - close[i - length];
            }
        }
    }

    /// <summary>
    /// Computes Midpoint.
    /// </summary>
    internal static void Midpoint(ReadOnlySpan<double> close, Span<double> output, int length = 14)
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

            var highest = double.MinValue;
            var lowest = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (close[j] > highest) highest = close[j];
                if (close[j] < lowest) lowest = close[j];
            }

            output[i] = (highest + lowest) / 2;
        }
    }

    /// <summary>
    /// Computes Midprice (HL Average).
    /// </summary>
    internal static void Midprice(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
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

            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            output[i] = (highestHigh + lowestLow) / 2;
        }
    }

    /// <summary>
    /// Computes Gann HiLo Activator.
    /// </summary>
    internal static void GannHiLoActivator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaHighArray = pool.Rent(close.Length);
        var smaLowArray = pool.Rent(close.Length);

        try
        {
            var smaHigh = smaHighArray.AsSpan(0, close.Length);
            var smaLow = smaLowArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(high, smaHigh, length);
            MovingAverageCore.SimpleMovingAverage(low, smaLow, length);

            bool isUptrend = true;

            for (var i = 0; i < close.Length; i++)
            {
                if (i == 0)
                {
                    output[i] = close[i] > smaHigh[i] ? smaLow[i] : smaHigh[i];
                    isUptrend = close[i] > smaHigh[i];
                }
                else
                {
                    if (close[i] > smaHigh[i])
                    {
                        isUptrend = true;
                    }
                    else if (close[i] < smaLow[i])
                    {
                        isUptrend = false;
                    }

                    output[i] = isUptrend ? smaLow[i] : smaHigh[i];
                }
            }
        }
        finally
        {
            pool.Return(smaHighArray);
            pool.Return(smaLowArray);
        }
    }

    /// <summary>
    /// Computes HalfTrend indicator.
    /// </summary>
    internal static void HalfTrend(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int amplitude = 2, int channelDeviation = 2)
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
            VolatilityCore.AverageTrueRange(high, low, close, atr, 100);

            bool trend = false;
            double maxLowPrice = low[0];
            double minHighPrice = high[0];
            double halfTrend = 0;

            for (var i = 0; i < close.Length; i++)
            {
                double highPrice = high[i];
                double lowPrice = low[i];
                double closePrice = close[i];

                // Calculate highest high and lowest low in amplitude period
                double highestHigh = highPrice;
                double lowestLow = lowPrice;

                int startIdx = Math.Max(0, i - amplitude);
                for (var j = startIdx; j <= i; j++)
                {
                    if (high[j] > highestHigh) highestHigh = high[j];
                    if (low[j] < lowestLow) lowestLow = low[j];
                }

                double highMa = (highestHigh + lowestLow) / 2;
                double lowMa = highMa;

                double dev = channelDeviation * atr[i];

                if (i > 0)
                {
                    if (!trend && closePrice > maxLowPrice + dev)
                    {
                        trend = true;
                        halfTrend = maxLowPrice;
                        minHighPrice = highPrice;
                    }
                    else if (trend && closePrice < minHighPrice - dev)
                    {
                        trend = false;
                        halfTrend = minHighPrice;
                        maxLowPrice = lowPrice;
                    }

                    if (trend)
                    {
                        if (lowPrice > maxLowPrice) maxLowPrice = lowPrice;
                        halfTrend = maxLowPrice;
                    }
                    else
                    {
                        if (highPrice < minHighPrice) minHighPrice = highPrice;
                        halfTrend = minHighPrice;
                    }
                }
                else
                {
                    halfTrend = (highestHigh + lowestLow) / 2;
                }

                output[i] = halfTrend;
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Vortex Indicator Positive (VI+).
    /// </summary>
    internal static void VortexPositive(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var trArray = pool.Rent(close.Length);

        try
        {
            var tr = trArray.AsSpan(0, close.Length);
            VolatilityCore.TrueRange(high, low, close, tr);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                    continue;
                }

                double vmPlus = 0;
                double sumTr = 0;

                for (var j = i - length + 1; j <= i; j++)
                {
                    vmPlus += Math.Abs(high[j] - low[j - 1]);
                    sumTr += tr[j];
                }

                output[i] = sumTr != 0 ? vmPlus / sumTr : 0;
            }
        }
        finally
        {
            pool.Return(trArray);
        }
    }

    /// <summary>
    /// Computes Vortex Indicator Negative (VI-).
    /// </summary>
    internal static void VortexNegative(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var trArray = pool.Rent(close.Length);

        try
        {
            var tr = trArray.AsSpan(0, close.Length);
            VolatilityCore.TrueRange(high, low, close, tr);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                    continue;
                }

                double vmMinus = 0;
                double sumTr = 0;

                for (var j = i - length + 1; j <= i; j++)
                {
                    vmMinus += Math.Abs(low[j] - high[j - 1]);
                    sumTr += tr[j];
                }

                output[i] = sumTr != 0 ? vmMinus / sumTr : 0;
            }
        }
        finally
        {
            pool.Return(trArray);
        }
    }

    /// <summary>
    /// Computes Linear Regression Intercept.
    /// </summary>
    internal static void LinearRegressionIntercept(ReadOnlySpan<double> input, Span<double> output, int length = 14)
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

            // Calculate linear regression
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            int startIdx = i - length + 1;

            for (var j = 0; j < length; j++)
            {
                double x = j;
                double y = input[startIdx + j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumXX += x * x;
            }

            double n = length;
            double denominator = n * sumXX - sumX * sumX;
            double slope = denominator != 0 ? (n * sumXY - sumX * sumY) / denominator : 0;
            double intercept = (sumY - slope * sumX) / n;

            output[i] = intercept;
        }
    }

    /// <summary>
    /// Computes Elder Impulse System.
    /// Returns 1 for bullish, -1 for bearish, 0 for neutral.
    /// </summary>
    internal static void ElderImpulseSystem(ReadOnlySpan<double> close, Span<double> output, int emaLength = 13, int macdFastLength = 12, int macdSlowLength = 26, int macdSignalLength = 9)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);
        var macdHistArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            var macdHist = macdHistArray.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, ema, emaLength);
            OscillatorCore.MacdHistogram(close, macdHist, macdFastLength, macdSlowLength, macdSignalLength);

            for (var i = 0; i < close.Length; i++)
            {
                if (i == 0)
                {
                    output[i] = 0;
                    continue;
                }

                bool emaRising = ema[i] > ema[i - 1];
                bool histRising = macdHist[i] > macdHist[i - 1];

                if (emaRising && histRising)
                {
                    output[i] = 1; // Bullish
                }
                else if (!emaRising && !histRising)
                {
                    output[i] = -1; // Bearish
                }
                else
                {
                    output[i] = 0; // Neutral
                }
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(macdHistArray);
        }
    }

    /// <summary>
    /// Computes Ichimoku Tenkan-sen (Conversion Line).
    /// </summary>
    internal static void IchimokuTenkanSen(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 9)
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

            double highestHigh = double.MinValue;
            double lowestLow = double.MaxValue;

            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            output[i] = (highestHigh + lowestLow) / 2;
        }
    }

    /// <summary>
    /// Computes Ichimoku Kijun-sen (Base Line).
    /// </summary>
    internal static void IchimokuKijunSen(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 26)
    {
        IchimokuTenkanSen(high, low, output, length);
    }

    /// <summary>
    /// Computes Mass Thrust Indicator.
    /// </summary>
    internal static void MassThrust(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var thrustArray = pool.Rent(close.Length);

        try
        {
            var thrust = thrustArray.AsSpan(0, close.Length);

            thrust[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                if (close[i] > close[i - 1])
                {
                    thrust[i] = volume[i];
                }
                else if (close[i] < close[i - 1])
                {
                    thrust[i] = -volume[i];
                }
                else
                {
                    thrust[i] = 0;
                }
            }

            MovingAverageCore.SimpleMovingAverage(thrust, output, length);
        }
        finally
        {
            pool.Return(thrustArray);
        }
    }

    /// <summary>
    /// Computes Chande Trend Score.
    /// Measures trend strength over multiple timeframes.
    /// </summary>
    internal static void ChandeTrendScore(ReadOnlySpan<double> close, Span<double> output, int length = 20)
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

            var score = 0;
            for (var j = 1; j <= length; j++)
            {
                if (close[i] > close[i - j])
                {
                    score++;
                }
                else if (close[i] < close[i - j])
                {
                    score--;
                }
            }

            output[i] = score;
        }
    }

    /// <summary>
    /// Computes Chop Zone indicator.
    /// Measures whether market is choppy or trending.
    /// </summary>
    internal static void ChopZone(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(close.Length);
        var emaArray = pool.Rent(close.Length);

        try
        {
            var atr = atrArray.AsSpan(0, close.Length);
            var ema = emaArray.AsSpan(0, close.Length);

            VolatilityCore.AverageTrueRange(high, low, close, atr, length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, 34);

            for (var i = 0; i < close.Length; i++)
            {
                if (atr[i] > 0)
                {
                    var distance = (close[i] - ema[i]) / atr[i];
                    output[i] = distance;
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(atrArray);
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Auto Line indicator.
    /// Adaptive trend line calculation.
    /// </summary>
    internal static void AutoLine(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = close[0];
        for (var i = 1; i < close.Length; i++)
        {
            var change = Math.Abs(close[i] - close[i - 1]);
            var alpha = change > 0 ? Math.Min(1.0, change / close[i - 1]) : 0.1;
            alpha = Math.Max(2.0 / (length + 1), alpha);
            output[i] = output[i - 1] + alpha * (close[i] - output[i - 1]);
        }
    }

    /// <summary>
    /// Computes Auto Line with Drift indicator.
    /// Adaptive trend line with drift adjustment.
    /// </summary>
    internal static void AutoLineWithDrift(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = close[0];
        var drift = 0.0;
        for (var i = 1; i < close.Length; i++)
        {
            var change = Math.Abs(close[i] - close[i - 1]);
            var alpha = change > 0 ? Math.Min(1.0, change / close[i - 1]) : 0.1;
            alpha = Math.Max(2.0 / (length + 1), alpha);

            drift = alpha * (close[i] - close[i - 1]) + (1 - alpha) * drift;
            output[i] = output[i - 1] + alpha * (close[i] - output[i - 1]) + drift;
        }
    }

    /// <summary>
    /// Computes Auto Filter indicator.
    /// Adaptive filter for trend detection.
    /// </summary>
    internal static void AutoFilter(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(close.Length);

        try
        {
            var sma = smaArray.AsSpan(0, close.Length);
            MovingAverageCore.SimpleMovingAverage(close, sma, length);

            for (var i = 0; i < close.Length; i++)
            {
                // Auto filter adjusts based on price position relative to SMA
                output[i] = close[i] > sma[i] ? sma[i] + Math.Abs(close[i] - sma[i]) * 0.5 :
                           close[i] < sma[i] ? sma[i] - Math.Abs(close[i] - sma[i]) * 0.5 : sma[i];
            }
        }
        finally
        {
            pool.Return(smaArray);
        }
    }

    /// <summary>
    /// Computes Buff Average indicator.
    /// Smoothed average for trend following.
    /// </summary>
    internal static void BuffAverage(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(close.Length);
        var ema2Array = pool.Rent(close.Length);

        try
        {
            var ema1 = ema1Array.AsSpan(0, close.Length);
            var ema2 = ema2Array.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, ema1, length);
            MovingAverageCore.ExponentialMovingAverage(ema1, ema2, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = 2 * ema1[i] - ema2[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
        }
    }

    /// <summary>
    /// Computes Bryant Adaptive Moving Average.
    /// Adaptive MA based on market conditions.
    /// </summary>
    internal static void BryantAdaptiveMovingAverage(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = close[0];
        for (var i = 1; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = close[i];
                continue;
            }

            // Calculate volatility factor
            var sumChange = 0.0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sumChange += Math.Abs(close[j] - close[j - 1]);
            }
            var avgChange = sumChange / length;

            // Adapt smoothing factor based on volatility
            var k = avgChange > 0 ? Math.Min(1.0, Math.Abs(close[i] - close[i - 1]) / avgChange) : 0.5;
            var alpha = (2.0 / (length + 1)) + k * (1 - 2.0 / (length + 1));

            output[i] = output[i - 1] + alpha * (close[i] - output[i - 1]);
        }
    }

    /// <summary>
    /// Computes ATR Trailing Stops.
    /// Stop loss levels based on ATR.
    /// </summary>
    internal static void AtrTrailingStops(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14, double multiplier = 3)
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
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            output[0] = close[0];
            var trend = 1; // 1 = uptrend, -1 = downtrend

            for (var i = 1; i < close.Length; i++)
            {
                var atrValue = atr[i] * multiplier;
                var longStop = close[i] - atrValue;
                var shortStop = close[i] + atrValue;

                if (trend == 1)
                {
                    output[i] = Math.Max(output[i - 1], longStop);
                    if (close[i] < output[i])
                    {
                        trend = -1;
                        output[i] = shortStop;
                    }
                }
                else
                {
                    output[i] = Math.Min(output[i - 1], shortStop);
                    if (close[i] > output[i])
                    {
                        trend = 1;
                        output[i] = longStop;
                    }
                }
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Compound Ratio Moving Average.
    /// </summary>
    internal static void CompoundRatioMovingAverage(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = close[i];
                continue;
            }

            var sum = 0.0;
            var weightSum = 0.0;
            for (var j = 0; j < length; j++)
            {
                var weight = Math.Pow(1.0 + (double)j / length, j);
                sum += close[i - j] * weight;
                weightSum += weight;
            }

            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Conditional Accumulator.
    /// Accumulates values based on conditions.
    /// </summary>
    internal static void ConditionalAccumulator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = close[0];
        for (var i = 1; i < close.Length; i++)
        {
            if (close[i] > close[i - 1])
            {
                output[i] = output[i - 1] + (close[i] - close[i - 1]);
            }
            else if (close[i] < close[i - 1])
            {
                output[i] = output[i - 1] - (close[i - 1] - close[i]);
            }
            else
            {
                output[i] = output[i - 1];
            }
        }
    }

    /// <summary>
    /// Computes Ahrens Moving Average.
    /// </summary>
    internal static void AhrensMovingAverage(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = close[0];
        var k = 2.0 / (length + 1);

        for (var i = 1; i < close.Length; i++)
        {
            var diff = close[i] - output[i - 1];
            output[i] = output[i - 1] + k * diff * (1 + Math.Abs(diff) / (Math.Abs(close[i]) + 1e-10));
        }
    }

    #region Batch 16 - Additional Trend Indicators

    /// <summary>
    /// Computes Ichimoku Senkou Span A (Leading Span A).
    /// </summary>
    internal static void IchimokuSenkouSpanA(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int tenkanLength = 9, int kijunLength = 26)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var tenkanArray = pool.Rent(high.Length);
        var kijunArray = pool.Rent(high.Length);

        try
        {
            var tenkan = tenkanArray.AsSpan(0, high.Length);
            var kijun = kijunArray.AsSpan(0, high.Length);

            IchimokuTenkanSen(high, low, tenkan, tenkanLength);
            IchimokuKijunSen(high, low, kijun, kijunLength);

            // Senkou Span A = (Tenkan + Kijun) / 2
            for (var i = 0; i < high.Length; i++)
            {
                output[i] = (tenkan[i] + kijun[i]) / 2;
            }
        }
        finally
        {
            pool.Return(tenkanArray);
            pool.Return(kijunArray);
        }
    }

    /// <summary>
    /// Computes Ichimoku Senkou Span B (Leading Span B).
    /// </summary>
    internal static void IchimokuSenkouSpanB(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 52)
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

            var highest = double.MinValue;
            var lowest = double.MaxValue;

            for (var j = 0; j < length; j++)
            {
                var idx = i - j;
                if (high[idx] > highest) highest = high[idx];
                if (low[idx] < lowest) lowest = low[idx];
            }

            output[i] = (highest + lowest) / 2;
        }
    }

    /// <summary>
    /// Computes Ichimoku Chikou Span (Lagging Span).
    /// Simply the close price (actual lagging is done via displacement).
    /// </summary>
    internal static void IchimokuChikouSpan(ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        close.CopyTo(output);
    }

    /// <summary>
    /// Computes Aroon Up indicator.
    /// </summary>
    internal static void AroonUp(ReadOnlySpan<double> high, Span<double> output, int length = 25)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var highestIdx = 0;
            var highest = double.MinValue;
            for (var j = 0; j <= length; j++)
            {
                var idx = i - j;
                if (high[idx] > highest)
                {
                    highest = high[idx];
                    highestIdx = j;
                }
            }

            output[i] = ((length - highestIdx) / (double)length) * 100;
        }
    }

    /// <summary>
    /// Computes Aroon Down indicator.
    /// </summary>
    internal static void AroonDown(ReadOnlySpan<double> low, Span<double> output, int length = 25)
    {
        if (output.Length < low.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < low.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var lowestIdx = 0;
            var lowest = double.MaxValue;
            for (var j = 0; j <= length; j++)
            {
                var idx = i - j;
                if (low[idx] < lowest)
                {
                    lowest = low[idx];
                    lowestIdx = j;
                }
            }

            output[i] = ((length - lowestIdx) / (double)length) * 100;
        }
    }

    /// <summary>
    /// Computes Williams Fractal Up.
    /// </summary>
    internal static void WilliamsFractalUp(ReadOnlySpan<double> high, Span<double> output, int length = 2)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var lookback = length * 2 + 1;

        for (var i = 0; i < high.Length; i++)
        {
            if (i < lookback - 1)
            {
                output[i] = 0;
                continue;
            }

            var midIdx = i - length;
            var isFractal = true;

            for (var j = 1; j <= length; j++)
            {
                if (high[midIdx - j] >= high[midIdx] || high[midIdx + j] >= high[midIdx])
                {
                    isFractal = false;
                    break;
                }
            }

            output[i] = isFractal ? high[midIdx] : 0;
        }
    }

    /// <summary>
    /// Computes Williams Fractal Down.
    /// </summary>
    internal static void WilliamsFractalDown(ReadOnlySpan<double> low, Span<double> output, int length = 2)
    {
        if (output.Length < low.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var lookback = length * 2 + 1;

        for (var i = 0; i < low.Length; i++)
        {
            if (i < lookback - 1)
            {
                output[i] = 0;
                continue;
            }

            var midIdx = i - length;
            var isFractal = true;

            for (var j = 1; j <= length; j++)
            {
                if (low[midIdx - j] <= low[midIdx] || low[midIdx + j] <= low[midIdx])
                {
                    isFractal = false;
                    break;
                }
            }

            output[i] = isFractal ? low[midIdx] : 0;
        }
    }

    /// <summary>
    /// Computes Alligator Jaw (Blue line - 13-period SMMA displaced by 8 bars).
    /// </summary>
    internal static void AlligatorJaw(ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        MovingAverageCore.SmoothedMovingAverage(close, output, length);
    }

    /// <summary>
    /// Computes Alligator Teeth (Red line - 8-period SMMA displaced by 5 bars).
    /// </summary>
    internal static void AlligatorTeeth(ReadOnlySpan<double> close, Span<double> output, int length = 8)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        MovingAverageCore.SmoothedMovingAverage(close, output, length);
    }

    /// <summary>
    /// Computes Alligator Lips (Green line - 5-period SMMA displaced by 3 bars).
    /// </summary>
    internal static void AlligatorLips(ReadOnlySpan<double> close, Span<double> output, int length = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        MovingAverageCore.SmoothedMovingAverage(close, output, length);
    }

    #endregion

    #region Batch 29 - Additional Missing Indicators

    /// <summary>
    /// Computes 3HMA (Triple Hull Moving Average).
    /// </summary>
    internal static void TripleHullMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var p = Math.Max(1, (int)Math.Ceiling((double)length / 2));
        var p1 = Math.Max(1, (int)Math.Ceiling((double)p / 3));
        var p2 = Math.Max(1, (int)Math.Ceiling((double)p / 2));

        var wma1Array = pool.Rent(input.Length);
        var wma2Array = pool.Rent(input.Length);
        var wma3Array = pool.Rent(input.Length);
        var midArray = pool.Rent(input.Length);

        try
        {
            var wma1 = wma1Array.AsSpan(0, input.Length);
            var wma2 = wma2Array.AsSpan(0, input.Length);
            var wma3 = wma3Array.AsSpan(0, input.Length);
            var mid = midArray.AsSpan(0, input.Length);

            MovingAverageCore.WeightedMovingAverage(input, wma1, p1);
            MovingAverageCore.WeightedMovingAverage(input, wma2, p2);
            MovingAverageCore.WeightedMovingAverage(input, wma3, p);

            // mid = wma1 * 3 - wma2 - wma3
            for (var i = 0; i < input.Length; i++)
            {
                mid[i] = (wma1[i] * 3) - wma2[i] - wma3[i];
            }

            // Final WMA of mid
            MovingAverageCore.WeightedMovingAverage(mid, output, p);
        }
        finally
        {
            pool.Return(wma1Array);
            pool.Return(wma2Array);
            pool.Return(wma3Array);
            pool.Return(midArray);
        }
    }

    /// <summary>
    /// Computes Adaptive Autonomous Recursive Moving Average.
    /// </summary>
    internal static void AdaptiveAutonomousRecursiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double lambda = 1)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sum = 0;
        double sumSq = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            sum += currentValue;
            sumSq += currentValue * currentValue;

            if (i == 0)
            {
                output[i] = currentValue;
                continue;
            }

            var n = i + 1;
            var mean = sum / n;
            var variance = (sumSq / n) - (mean * mean);
            var stdDev = Math.Sqrt(Math.Max(0, variance));

            var prevArma = output[i - 1];
            var diff = Math.Abs(currentValue - prevArma);
            var adaptiveK = diff / (diff + (lambda * stdDev) + 1e-10);

            output[i] = prevArma + adaptiveK * (currentValue - prevArma);
        }
    }

    #endregion
}
