using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VolumeIndexTotal
{
    private readonly bool _positive;
    private readonly int _initial;
    private RocBankValue _index;
    private double _previousPrice, _previousVolume;
    internal VolumeIndexTotal(bool positive = false, int initialValue = 1000)
    {
        _positive = positive; _initial = initialValue; _index = new(initialValue);
    }
    internal RocBankValue Next(double price, double volume, bool commit)
    {
        var index = _index;
        if (_previousPrice != 0 && (_positive ? volume > _previousVolume : volume < _previousVolume))
        {
            var prior = ExactVarianceWindow.Units(_previousPrice);
            var numerator = (ExactVarianceWindow.Units(index.Mantissa) * (BigInteger.Abs(prior) + ExactVarianceWindow.Units(price) - prior)) << index.UpperShift;
            for (var shift = 0; ; shift += 1024)
            {
                var value = ExactMeanAccumulator.UnitRatio(numerator, BigInteger.Abs(prior) << shift);
                if (!double.IsInfinity(value)) { index = new RocBankValue(value, shift); break; }
            }
        }
        if (commit) { _index = index; _previousPrice = price; _previousVolume = volume; }
        return index;
    }
    internal void Reset() { _index = new(_initial); _previousPrice = 0; _previousVolume = 0; }
}

internal sealed class VolumeIndexWindow : IDisposable
{
    private readonly VolumeIndexTotal _total;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal VolumeIndexWindow(MovingAvgType kind, int length, bool positive = false, int initialValue = 1000)
    {
        _total = new(positive, initialValue);
        if (StrengthWindow.Supports(kind)) _average = new RocBankAverage(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal (double Line, double Signal) Next(double price, double volume, bool commit, double? customerSignal = null)
    {
        var line = _total.Next(price, volume, commit);
        var signal = customerSignal.HasValue ? new RocBankValue(customerSignal.Value) : _average is null
            ? new RocBankValue(_fallback!.Next(line.Publish(), commit)) : _average.Next(line, commit);
        return (line.Publish(), signal.Publish());
    }
    internal void Reset() { _total.Reset(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
