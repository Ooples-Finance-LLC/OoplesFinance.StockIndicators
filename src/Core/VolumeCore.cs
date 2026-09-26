using System;
using System.Buffers;

namespace OoplesFinance.StockIndicators.Core;

/// <summary>
/// Core span-based implementations for volume indicators.
/// Computation directly into output spans; exceptional-range exact arithmetic may allocate.
/// </summary>
internal static class VolumeCore
{
    /// <summary>
    /// Computes On Balance Volume (OBV).
    /// </summary>
    internal static void OnBalanceVolume(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var sum = new ExactMeanAccumulator();
        for (var i = 0; i < close.Length; i++)
        {
            var previous = i == 0 ? 0 : close[i - 1];
            if (close[i] > previous) sum.Add(volume[i]);
            else if (close[i] < previous) sum.Add(volume[i], -1);
            output[i] = sum.Mean(1);
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

            // CalculateChaikinMoneyFlow divides one rolling sum by another over however many bars have
            // arrived, so the flow has a reading from the first bar rather than none.
            output[i] = volSum != 0 ? mfvSum / volSum : 0;
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
                output[i] = RoundedPercentageChange.Of(volume[i], prevVol);
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

        var mean = new ExactVolumeMean();

        for (var i = 0; i < close.Length; i++)
        {
            mean.AddTypical(high[i], low[i], close[i], volume[i]);
            output[i] = mean.Value();
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

        try
        {
            var vf = vfArray.AsSpan(0, close.Length);

            double previousSum = 0, previousRange = 0, cumulativeRange = 0;
            var trend = 0;
            for (var i = 0; i < close.Length; i++)
            {
                var sum = high[i] + low[i] + close[i];
                var range = high[i] - low[i];
                var nextTrend = i == 0 ? 0 : sum > previousSum ? 1 : sum < previousSum ? -1 : trend;
                cumulativeRange = nextTrend == trend ? cumulativeRange + range : previousRange + range;
                vf[i] = cumulativeRange == 0 ? 0 : volume[i] * Math.Abs(2 * range / cumulativeRange - 1) * nextTrend * 100;
                trend = nextTrend;
                previousSum = sum;
                previousRange = range;
            }

            var difference = new OoplesFinance.StockIndicators.Streaming.KlingerEmaDifference(fastLength, slowLength);
            for (var i = 0; i < close.Length; i++) output[i] = difference.Next(vf[i], true);
        }
        finally
        {
            pool.Return(vfArray);
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
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var flow = new OoplesFinance.StockIndicators.Streaming.RollingMoneyFlowIndex(Math.Min(Math.Max(1, length), Math.Max(1, close.Length)));
        for (var i = 0; i < close.Length; i++)
            output[i] = flow.Next(OoplesFinance.StockIndicators.Streaming.RollingMoneyFlowIndex.TypicalPrice(high[i], low[i], close[i]), volume[i], true);
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

            BollingerArithmetic.Mean(volume, fastSma, fastLength);
            BollingerArithmetic.Mean(volume, slowSma, slowLength);

            for (var i = 0; i < volume.Length; i++)
            {
                output[i] = RoundedPercentageChange.Of(fastSma[i], slowSma[i]);
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

        if (close.Length == 0) return;

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
                output[i] = RoundedMomentumRatio.Of(vp[i], tv[i]);
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

    /// <summary>
    /// Computes Williams Accumulation/Distribution.
    /// </summary>
    internal static void WilliamsAD(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var window = new WilliamsAccumulationWindow();
        for (var i = 0; i < close.Length; i++) output[i] = window.Next(high[i], low[i], close[i], true).Publish();
    }

    /// <summary>
    /// Computes Net Volume.
    /// </summary>
    internal static void NetVolume(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
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
            if (close[i] > close[i - 1])
            {
                output[i] = volume[i];
            }
            else if (close[i] < close[i - 1])
            {
                output[i] = -volume[i];
            }
            else
            {
                output[i] = 0;
            }
        }
    }

    /// <summary>
    /// Computes Cumulative Volume Index.
    /// </summary>
    internal static void CumulativeVolumeIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        var cvi = new ExactMeanAccumulator();
        output[0] = 0;

        for (var i = 1; i < close.Length; i++)
        {
            if (close[i] > close[i - 1])
            {
                cvi.Add(volume[i]);
            }
            else if (close[i] < close[i - 1])
            {
                cvi.Add(volume[i], -1);
            }

            output[i] = cvi.Mean(1);
        }
    }

    /// <summary>
    /// Computes Volume Momentum.
    /// </summary>
    internal static void VolumeMomentum(ReadOnlySpan<double> volume, Span<double> output, int length = 10)
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
                output[i] = volume[i] - volume[i - length];
            }
        }
    }

    /// <summary>
    /// Computes Volume Price Trend.
    /// </summary>
    internal static void VolumePriceTrend(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        double vpt = 0;
        output[0] = 0;

        for (var i = 1; i < close.Length; i++)
        {
            var pctChange = close[i - 1] != 0 ? (close[i] - close[i - 1]) / close[i - 1] : 0;
            vpt += volume[i] * pctChange;
            output[i] = vpt;
        }
    }

    /// <summary>
    /// Computes Elder Ray Bull Power.
    /// </summary>
    internal static void ElderRayBullPower(ReadOnlySpan<double> high, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = high[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Elder Ray Bear Power.
    /// </summary>
    internal static void ElderRayBearPower(ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = low[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Normalized Volume.
    /// </summary>
    internal static void NormalizedVolume(ReadOnlySpan<double> volume, Span<double> output, int length = 20)
    {
        if (output.Length < volume.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var sum = new ExactMeanAccumulator();
        for (var i = 0; i < volume.Length; i++)
        {
            sum.Add(volume[i]);
            if (i >= length) sum.Add(volume[i - length], -1);
            var numerator = new ExactMeanAccumulator();
            numerator.Add(volume[i], length);
            output[i] = i + 1 < length ? 0 : numerator.Ratio(sum);
        }
    }

    /// <summary>
    /// Computes Volume Weighted RSI.
    /// </summary>
    internal static void VolumeWeightedRsi(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0)
        {
            return;
        }

        double avgGain = 0;
        double avgLoss = 0;
        output[0] = 50;

        for (var i = 1; i < close.Length; i++)
        {
            var change = close[i] - close[i - 1];
            var weightedChange = change * volume[i];

            var gain = weightedChange > 0 ? weightedChange : 0;
            var loss = weightedChange < 0 ? -weightedChange : 0;

            if (i <= length)
            {
                avgGain += gain;
                avgLoss += loss;

                if (i == length)
                {
                    avgGain /= length;
                    avgLoss /= length;
                }

                output[i] = 50;
            }
            else
            {
                var k = 1.0 / length;
                avgGain = (gain * k) + (avgGain * (1 - k));
                avgLoss = (loss * k) + (avgLoss * (1 - k));

                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
                output[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + rs));
            }
        }
    }
}
