using System;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The volume-weighted mean of the trailing window, partial at the start: sum(volume * value) / sum(volume).
/// </summary>
/// <remarks>The streaming twin of the batch <c>GetRollingVolumeWeightedMeanList</c>, summed in the same order.</remarks>
internal sealed class RollingVolumeWeightedMean : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _volumePrices;
    private readonly PooledRingBuffer<double> _volumes;

    public RollingVolumeWeightedMean(int length)
    {
        _length = Math.Max(1, length);
        _volumePrices = new PooledRingBuffer<double>(_length);
        _volumes = new PooledRingBuffer<double>(_length);
    }

    public double Next(double value, double volume, bool isFinal)
    {
        var start = _volumes.Count >= _length ? 1 : 0;
        double volumePriceSum = 0, volumeSum = 0;
        for (var i = start; i < _volumes.Count; i++)
        {
            volumePriceSum += _volumePrices[i];
            volumeSum += _volumes[i];
        }

        var volumePrice = volume * value;
        volumePriceSum += volumePrice;
        volumeSum += volume;
        if (isFinal)
        {
            _volumePrices.TryAdd(volumePrice, out _);
            _volumes.TryAdd(volume, out _);
        }

        return volumeSum != 0 ? volumePriceSum / volumeSum : 0;
    }

    public void Reset()
    {
        _volumePrices.Clear();
        _volumes.Clear();
    }

    public void Dispose()
    {
        _volumePrices.Dispose();
        _volumes.Dispose();
    }
}

/// <summary>
/// A value's distance from its mean in units of sqrt(sma((value - mean)^2, length)): LazyBear's calc_zvwap.
/// </summary>
/// <remarks>
/// The streaming twin of the batch <c>GetZScoreList</c>. The squared distances are averaged afresh over the
/// window, oldest first, so the two engines agree to the bit and a flat window is exactly 0.
/// </remarks>
internal sealed class RollingZScore : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _devSquares;

    public RollingZScore(int length)
    {
        _length = Math.Max(1, length);
        _devSquares = new PooledRingBuffer<double>(_length);
    }

    public double Next(double value, double mean, bool isFinal)
    {
        var deviation = value - mean;
        var devSquared = deviation * deviation;
        var kept = Math.Min(_devSquares.Count, _length - 1);
        double variance = 0;
        if (kept + 1 >= _length)
        {
            double sum = 0;
            for (var i = _devSquares.Count - kept; i < _devSquares.Count; i++)
            {
                sum += _devSquares[i];
            }

            variance = (sum + devSquared) / _length;
        }

        if (isFinal)
        {
            _devSquares.TryAdd(devSquared, out _);
        }

        var deviationSd = Math.Sqrt(variance);
        return deviationSd != 0 ? (value - mean) / deviationSd : 0;
    }

    public void Reset() => _devSquares.Clear();

    public void Dispose() => _devSquares.Dispose();
}
