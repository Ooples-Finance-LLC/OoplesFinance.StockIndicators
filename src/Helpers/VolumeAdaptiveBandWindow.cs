using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VolumeAdaptiveBandWindow : IDisposable
{
    private readonly RocBankAverage? _volume, _upper, _lower;
    private readonly IMovingAverageSmoother? _volumeFallback, _upperFallback, _lowerFallback;
    private RocBankValue _previousUp, _previousDown;
    private bool _hasPrevious;
    internal VolumeAdaptiveBandWindow(MovingAvgType kind, int length, bool external = false, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length);
        if (!external) { if (StrengthWindow.Supports(kind)) { _volume = new(kind, length, capacityHint); _upper = new(kind, length, capacityHint); _lower = new(kind, length, capacityHint); } else { _volumeFallback = MovingAverageSmootherFactory.Create(kind, length); _upperFallback = MovingAverageSmootherFactory.Create(kind, length); _lowerFallback = MovingAverageSmootherFactory.Create(kind, length); } }
    }
    private static RocBankValue Advance(double close, RocBankValue previous, double volume, int sign)
    {
        var sum = new ExactMeanAccumulator(); sum.AddProduct(close, volume); previous.AddTo(ref sum, sign); return RocBankValue.Round(sum, volume);
    }
    internal static (double Upper, double Middle, double Lower) Bands(RocBankValue upper, RocBankValue lower)
    { var sum = new ExactMeanAccumulator(); upper.AddTo(ref sum); lower.AddTo(ref sum); return (upper.Publish(), sum.Mean(2), lower.Publish()); }
    internal (double RawUp, double RawDown, double Upper, double Middle, double Lower) Next(double close, double volume, bool commit, double? externalVolume = null)
    {
        var average = externalVolume ?? (_volume is not null ? _volume.Next(new RocBankValue(volume), commit).Publish() : _volumeFallback!.Next(volume, commit)); var divisor = Math.Max(1, average);
        var up = Advance(close, _hasPrevious ? _previousUp : new RocBankValue(close), divisor, 1);
        var down = Advance(close, _hasPrevious ? _previousDown : new RocBankValue(close), divisor, -1);
        var upper = _upper is not null ? _upper.Next(up, commit) : _upperFallback is not null ? new RocBankValue(_upperFallback.Next(up.Publish(), commit)) : default;
        var lower = _lower is not null ? _lower.Next(down, commit) : _lowerFallback is not null ? new RocBankValue(_lowerFallback.Next(down.Publish(), commit)) : default;
        if (commit) { _previousUp = up; _previousDown = down; _hasPrevious = true; }
        var bands = Bands(upper, lower); return (up.Publish(), down.Publish(), bands.Upper, bands.Middle, bands.Lower);
    }
    internal void Reset() { _volume?.Reset(); _upper?.Reset(); _lower?.Reset(); _volumeFallback?.Reset(); _upperFallback?.Reset(); _lowerFallback?.Reset(); _previousUp = _previousDown = default; _hasPrevious = false; }
    public void Dispose() { _volume?.Dispose(); _upper?.Dispose(); _lower?.Dispose(); _volumeFallback?.Dispose(); _upperFallback?.Dispose(); _lowerFallback?.Dispose(); }
}
