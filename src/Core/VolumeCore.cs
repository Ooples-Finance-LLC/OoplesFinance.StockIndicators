using System;
using System.Buffers;

namespace OoplesFinance.StockIndicators.Core;

/// <summary>
/// Core span-based implementations for volume indicators.
/// Zero-allocation computation directly into output spans.
/// </summary>
internal static class VolumeCore
{
    /// <summary>
    /// Computes On Balance Volume (OBV).
    /// </summary>
    internal static void OnBalanceVolume(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        output[0] = volume[0];
        for (var i = 1; i < close.Length; i++)
        {
            if (close[i] > close[i - 1])
            {
                output[i] = output[i - 1] + volume[i];
            }
            else if (close[i] < close[i - 1])
            {
                output[i] = output[i - 1] - volume[i];
            }
            else
            {
                output[i] = output[i - 1];
            }
        }
    }

    /// <summary>
    /// Computes Accumulation/Distribution Line.
    /// </summary>
    internal static void AccumulationDistributionLine(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double adl = 0;
        for (var i = 0; i < close.Length; i++)
        {
            var range = high[i] - low[i];
            var mfm = range != 0 ? ((close[i] - low[i]) - (high[i] - close[i])) / range : 0;
            var mfv = mfm * volume[i];
            adl += mfv;
            output[i] = adl;
        }
    }

    /// <summary>
    /// Computes Chaikin Money Flow.
    /// </summary>
    internal static void ChaikinMoneyFlow(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double mfvSum = 0;
        double volSum = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var range = high[i] - low[i];
            var mfm = range != 0 ? ((close[i] - low[i]) - (high[i] - close[i])) / range : 0;
            var mfv = mfm * volume[i];

            mfvSum += mfv;
            volSum += volume[i];

            if (i >= length)
            {
                var oldRange = high[i - length] - low[i - length];
                var oldMfm = oldRange != 0 ? ((close[i - length] - low[i - length]) - (high[i - length] - close[i - length])) / oldRange : 0;
                mfvSum -= oldMfm * volume[i - length];
                volSum -= volume[i - length];
            }

            output[i] = i >= length - 1 && volSum != 0 ? mfvSum / volSum : 0;
        }
    }

    /// <summary>
    /// Computes Force Index.
    /// </summary>
    internal static void ForceIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rawForceArray = pool.Rent(close.Length);

        try
        {
            var rawForce = rawForceArray.AsSpan(0, close.Length);

            rawForce[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                rawForce[i] = (close[i] - close[i - 1]) * volume[i];
            }

            MovingAverageCore.ExponentialMovingAverage(rawForce, output, length);
        }
        finally
        {
            pool.Return(rawForceArray);
        }
    }

    /// <summary>
    /// Computes Ease of Movement.
    /// </summary>
    internal static void EaseOfMovement(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rawEmvArray = pool.Rent(high.Length);

        try
        {
            var rawEmv = rawEmvArray.AsSpan(0, high.Length);

            rawEmv[0] = 0;
            for (var i = 1; i < high.Length; i++)
            {
                var dm = ((high[i] + low[i]) / 2) - ((high[i - 1] + low[i - 1]) / 2);
                var br = volume[i] / 100000000 / (high[i] - low[i]);
                rawEmv[i] = br != 0 ? dm / br : 0;
            }

            MovingAverageCore.SimpleMovingAverage(rawEmv, output, length);
        }
        finally
        {
            pool.Return(rawEmvArray);
        }
    }

    /// <summary>
    /// Computes Volume Rate of Change.
    /// </summary>
    internal static void VolumeRateOfChange(ReadOnlySpan<double> volume, Span<double> output, int length = 12)
    {
        if (output.Length < volume.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < volume.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
            }
            else
            {
                var prevVol = volume[i - length];
                output[i] = prevVol != 0 ? ((volume[i] - prevVol) / prevVol) * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Negative Volume Index.
    /// </summary>
    internal static void NegativeVolumeIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        output[0] = 1000;
        for (var i = 1; i < close.Length; i++)
        {
            if (volume[i] < volume[i - 1])
            {
                var roc = (close[i] - close[i - 1]) / close[i - 1];
                output[i] = output[i - 1] + (output[i - 1] * roc);
            }
            else
            {
                output[i] = output[i - 1];
            }
        }
    }

    /// <summary>
    /// Computes Positive Volume Index.
    /// </summary>
    internal static void PositiveVolumeIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        output[0] = 1000;
        for (var i = 1; i < close.Length; i++)
        {
            if (volume[i] > volume[i - 1])
            {
                var roc = (close[i] - close[i - 1]) / close[i - 1];
                output[i] = output[i - 1] + (output[i - 1] * roc);
            }
            else
            {
                output[i] = output[i - 1];
            }
        }
    }

    /// <summary>
    /// Computes Price Volume Trend (PVT).
    /// </summary>
    internal static void PriceVolumeTrend(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        output[0] = 0;
        for (var i = 1; i < close.Length; i++)
        {
            var roc = close[i - 1] != 0 ? (close[i] - close[i - 1]) / close[i - 1] : 0;
            output[i] = output[i - 1] + (volume[i] * roc);
        }
    }

    /// <summary>
    /// Computes Volume Weighted Average Price (VWAP).
    /// </summary>
    internal static void VolumeWeightedAveragePrice(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double cumulativeTPV = 0;
        double cumulativeVol = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var typicalPrice = (high[i] + low[i] + close[i]) / 3;
            cumulativeTPV += typicalPrice * volume[i];
            cumulativeVol += volume[i];
            output[i] = cumulativeVol != 0 ? cumulativeTPV / cumulativeVol : 0;
        }
    }

    /// <summary>
    /// Computes Chaikin Oscillator.
    /// </summary>
    internal static void ChaikinOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int fastLength = 3, int slowLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var adlArray = pool.Rent(close.Length);
        var fastEmaArray = pool.Rent(close.Length);
        var slowEmaArray = pool.Rent(close.Length);

        try
        {
            var adl = adlArray.AsSpan(0, close.Length);
            var fastEma = fastEmaArray.AsSpan(0, close.Length);
            var slowEma = slowEmaArray.AsSpan(0, close.Length);

            AccumulationDistributionLine(high, low, close, volume, adl);
            MovingAverageCore.ExponentialMovingAverage(adl, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(adl, slowEma, slowLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(adlArray);
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Klinger Volume Oscillator.
    /// </summary>
    internal static void KlingerVolumeOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int fastLength = 34, int slowLength = 55)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var vfArray = pool.Rent(close.Length);
        var fastEmaArray = pool.Rent(close.Length);
        var slowEmaArray = pool.Rent(close.Length);

        try
        {
            var vf = vfArray.AsSpan(0, close.Length);
            var fastEma = fastEmaArray.AsSpan(0, close.Length);
            var slowEma = slowEmaArray.AsSpan(0, close.Length);

            double prevHlc = 0;
            int trend = 0;

            for (var i = 0; i < close.Length; i++)
            {
                var hlc = high[i] + low[i] + close[i];
                var dm = high[i] - low[i];
                var cm = i > 0 ? (hlc > prevHlc ? dm : -dm) : dm;
                trend = i > 0 && hlc > prevHlc ? 1 : -1;

                var range = high[i] - low[i];
                vf[i] = range != 0 ? volume[i] * Math.Abs(2 * (dm / range) - 1) * trend * 100 : 0;
                prevHlc = hlc;
            }

            MovingAverageCore.ExponentialMovingAverage(vf, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(vf, slowEma, slowLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(vfArray);
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Volume Price Confirmation Indicator.
    /// </summary>
    internal static void VolumePriceConfirmationIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int shortLength = 5, int longLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var vwmaShortArray = pool.Rent(close.Length);
        var vwmaLongArray = pool.Rent(close.Length);
        var smaShortArray = pool.Rent(close.Length);
        var smaLongArray = pool.Rent(close.Length);

        try
        {
            var vwmaShort = vwmaShortArray.AsSpan(0, close.Length);
            var vwmaLong = vwmaLongArray.AsSpan(0, close.Length);
            var smaShort = smaShortArray.AsSpan(0, close.Length);
            var smaLong = smaLongArray.AsSpan(0, close.Length);

            MovingAverageCore.VolumeWeightedMovingAverage(close, volume, vwmaShort, shortLength);
            MovingAverageCore.VolumeWeightedMovingAverage(close, volume, vwmaLong, longLength);
            MovingAverageCore.SimpleMovingAverage(close, smaShort, shortLength);
            MovingAverageCore.SimpleMovingAverage(close, smaLong, longLength);

            for (var i = 0; i < close.Length; i++)
            {
                var vpcShort = vwmaShort[i] - smaShort[i];
                var vpcLong = vwmaLong[i] - smaLong[i];
                output[i] = vpcShort - vpcLong;
            }
        }
        finally
        {
            pool.Return(vwmaShortArray);
            pool.Return(vwmaLongArray);
            pool.Return(smaShortArray);
            pool.Return(smaLongArray);
        }
    }

    /// <summary>
    /// Computes Money Flow Index (MFI).
    /// </summary>
    internal static void MoneyFlowIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var typicalPriceArray = pool.Rent(close.Length);
        var rawMoneyFlowArray = pool.Rent(close.Length);

        try
        {
            var typicalPrice = typicalPriceArray.AsSpan(0, close.Length);
            var rawMoneyFlow = rawMoneyFlowArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                typicalPrice[i] = (high[i] + low[i] + close[i]) / 3;
                rawMoneyFlow[i] = typicalPrice[i] * volume[i];
            }

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                    continue;
                }

                double posFlow = 0;
                double negFlow = 0;

                for (var j = i - length + 1; j <= i; j++)
                {
                    if (j > 0 && typicalPrice[j] > typicalPrice[j - 1])
                    {
                        posFlow += rawMoneyFlow[j];
                    }
                    else if (j > 0)
                    {
                        negFlow += rawMoneyFlow[j];
                    }
                }

                var mfRatio = negFlow != 0 ? posFlow / negFlow : 0;
                output[i] = 100 - (100 / (1 + mfRatio));
            }
        }
        finally
        {
            pool.Return(typicalPriceArray);
            pool.Return(rawMoneyFlowArray);
        }
    }

    /// <summary>
    /// Computes Trade Volume Index.
    /// </summary>
    internal static void TradeVolumeIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, double minTickValue = 0.5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        double tvi = 0;
        output[0] = tvi;

        for (var i = 1; i < close.Length; i++)
        {
            var change = close[i] - close[i - 1];

            if (change > minTickValue)
            {
                tvi += volume[i];
            }
            else if (change < -minTickValue)
            {
                tvi -= volume[i];
            }

            output[i] = tvi;
        }
    }

    /// <summary>
    /// Computes Volume Oscillator.
    /// </summary>
    internal static void VolumeOscillator(ReadOnlySpan<double> volume, Span<double> output, int fastLength = 5, int slowLength = 20)
    {
        if (output.Length < volume.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastSmaArray = pool.Rent(volume.Length);
        var slowSmaArray = pool.Rent(volume.Length);

        try
        {
            var fastSma = fastSmaArray.AsSpan(0, volume.Length);
            var slowSma = slowSmaArray.AsSpan(0, volume.Length);

            MovingAverageCore.SimpleMovingAverage(volume, fastSma, fastLength);
            MovingAverageCore.SimpleMovingAverage(volume, slowSma, slowLength);

            for (var i = 0; i < volume.Length; i++)
            {
                output[i] = slowSma[i] != 0 ? ((fastSma[i] - slowSma[i]) / slowSma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(fastSmaArray);
            pool.Return(slowSmaArray);
        }
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average Price (VWMA).
    /// </summary>
    internal static void VolumeWeightedMovingAveragePrice(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 20)
    {
        MovingAverageCore.VolumeWeightedMovingAverage(close, volume, output, length);
    }

    /// <summary>
    /// Computes Twiggs Money Flow.
    /// </summary>
    internal static void TwiggsMoneyFlow(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 21)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        var pool = ArrayPool<double>.Shared;
        var adArray = pool.Rent(close.Length);
        var volSumArray = pool.Rent(close.Length);

        try
        {
            var ad = adArray.AsSpan(0, close.Length);
            var volSum = volSumArray.AsSpan(0, close.Length);

            double trh = high[0];
            double trl = low[0];

            for (var i = 0; i < close.Length; i++)
            {
                if (i > 0)
                {
                    trh = Math.Max(high[i], close[i - 1]);
                    trl = Math.Min(low[i], close[i - 1]);
                }

                var range = trh - trl;
                var adValue = range != 0 ? ((close[i] - trl) - (trh - close[i])) / range * volume[i] : 0;
                ad[i] = adValue;
                volSum[i] = volume[i];
            }

            // Apply Wilder smoothing (EMA with 1/length factor)
            var k = 1.0 / length;
            double smoothedAd = ad[0];
            double smoothedVol = volSum[0];

            for (var i = 0; i < close.Length; i++)
            {
                if (i > 0)
                {
                    smoothedAd = smoothedAd + k * (ad[i] - smoothedAd);
                    smoothedVol = smoothedVol + k * (volSum[i] - smoothedVol);
                }

                output[i] = smoothedVol != 0 ? smoothedAd / smoothedVol : 0;
            }
        }
        finally
        {
            pool.Return(adArray);
            pool.Return(volSumArray);
        }
    }

    /// <summary>
    /// Computes Volume Zone Oscillator.
    /// </summary>
    internal static void VolumeZoneOscillator(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rArray = pool.Rent(close.Length);
        var vpArray = pool.Rent(close.Length);
        var tvArray = pool.Rent(close.Length);

        try
        {
            var r = rArray.AsSpan(0, close.Length);
            var vp = vpArray.AsSpan(0, close.Length);
            var tv = tvArray.AsSpan(0, close.Length);

            r[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                r[i] = close[i] > close[i - 1] ? volume[i] : -volume[i];
            }

            MovingAverageCore.ExponentialMovingAverage(r, vp, length);

            for (var i = 0; i < close.Length; i++)
            {
                tv[i] = volume[i];
            }
            MovingAverageCore.ExponentialMovingAverage(tv, tv, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = tv[i] != 0 ? (vp[i] / tv[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(rArray);
            pool.Return(vpArray);
            pool.Return(tvArray);
        }
    }

    /// <summary>
    /// Computes Demand Index.
    /// </summary>
    internal static void DemandIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        output[0] = 0;
        for (var i = 1; i < close.Length; i++)
        {
            var range = high[i] - low[i];
            var bp = close[i] - low[i];
            var sp = high[i] - close[i];

            var bpPercent = range != 0 ? bp / range : 0;
            var spPercent = range != 0 ? sp / range : 0;

            var buyVolume = volume[i] * bpPercent;
            var sellVolume = volume[i] * spPercent;

            output[i] = sellVolume != 0 ? (buyVolume / sellVolume) - 1 : 0;
        }
    }
}
