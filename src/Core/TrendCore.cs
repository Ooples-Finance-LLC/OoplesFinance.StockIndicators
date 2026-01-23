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
}
