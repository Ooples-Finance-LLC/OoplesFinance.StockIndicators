using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ElasticVolumeAverageWindow : IDisposable
{
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly BigInteger _multiplier;
    private RocBankValue _previous;
    private bool _started;
    internal ElasticVolumeAverageWindow(MovingAvgType kind, int length, double multiplier, bool initializeFallback = true)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        _multiplier = ExactVarianceWindow.Units(multiplier);
        if (StrengthWindow.Supports(kind)) _average = new(kind, length, int.MaxValue);
        else if (initializeFallback) _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal double Next(double price, double volume, bool commit) => NextWithAverage(price, volume,
        _average is null ? new RocBankValue(_fallback!.Next(volume, commit)) : _average.Next(new RocBankValue(volume), commit), commit);
    internal double NextWithAverage(double price, double volume, RocBankValue average, bool commit)
    {
        var denominator = (ExactVarianceWindow.Units(average.Mantissa) << average.UpperShift) * _multiplier;
        var current = ExactVarianceWindow.Units(volume) << 1074;
        var previous = _started ? _previous : new RocBankValue(price); var value = previous;
        if (denominator.Sign > 0)
        {
            var top = new ExactMeanAccumulator();
            top.Add(previous.Mantissa, (denominator - current) << previous.UpperShift); top.Add(price, current);
            for (var shift = 0; ; shift += 1024)
            {
                var bottom = new ExactMeanAccumulator(); bottom.Add(1, denominator << shift);
                var rounded = top.Ratio(bottom);
                if (!double.IsInfinity(rounded)) { value = new RocBankValue(rounded, shift); break; }
            }
        }
        if (commit) { _previous = value; _started = true; }
        return value.Publish();
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _previous = default; _started = false; }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
