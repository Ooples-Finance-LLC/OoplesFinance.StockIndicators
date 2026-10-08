using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MoneyFlowPercentWindow : IDisposable
{
    private readonly bool _twiggs;
    private readonly PooledRingBuffer<(RocBankValue Flow, double Volume)>? _window;
    private readonly RocBankAverage? _flowAverage, _volumeAverage;
    private readonly IMovingAverageSmoother? _flowFallback, _volumeFallback;
    private ExactMeanAccumulator _flow, _volume;
    private double _previous;
    internal MoneyFlowPercentWindow(int length, MovingAvgType? kind = null)
    {
        length = Math.Max(1, length); _twiggs = kind.HasValue;
        if (!kind.HasValue) _window = new(length);
        else if (StrengthWindow.Supports(kind.Value))
        { _flowAverage = new RocBankAverage(kind.Value, length, int.MaxValue); _volumeAverage = new RocBankAverage(kind.Value, length, int.MaxValue); }
        else
        { _flowFallback = MovingAverageSmootherFactory.Create(kind.Value, length); _volumeFallback = MovingAverageSmootherFactory.Create(kind.Value, length); }
    }
    internal double Next(double high, double low, double close, double volume, bool commit,
        double? customerVolume = null, double? customerFlow = null)
    {
        var value = MoneyFlowAccumulationWindow.Flow(_twiggs ? Math.Max(high, _previous) : high,
            _twiggs ? Math.Min(low, _previous) : low, close, volume);
        var numerator = _flow; var denominator = _volume;
        if (_twiggs)
        {
            var flowMean = customerFlow.HasValue ? new RocBankValue(customerFlow.Value) : _flowAverage is null
                ? new RocBankValue(_flowFallback!.Next(value.Publish(), commit)) : _flowAverage.Next(value, commit);
            var volumeMean = customerVolume.HasValue ? new RocBankValue(customerVolume.Value) : _volumeAverage is null
                ? new RocBankValue(_volumeFallback!.Next(volume, commit)) : _volumeAverage.Next(new RocBankValue(volume), commit);
            numerator = denominator = default; flowMean.AddTo(ref numerator); volumeMean.AddTo(ref denominator);
        }
        else
        {
            if (_window!.Count == _window.Capacity)
            { var expired = _window[0]; expired.Flow.AddTo(ref numerator, -100); denominator.Add(expired.Volume, -1); }
            value.AddTo(ref numerator, 100); denominator.Add(volume);
        }
        var bound = _twiggs ? 1d : 100d;
        var result = Math.Min(bound, Math.Max(-bound, numerator.Ratio(denominator)));
        if (commit) { _window?.TryAdd((value, volume), out _); _flow = numerator; _volume = denominator; _previous = close; }
        return result;
    }
    internal void Reset() { _window?.Clear(); _flowAverage?.Reset(); _volumeAverage?.Reset(); _flowFallback?.Reset(); _volumeFallback?.Reset(); _flow = _volume = default; _previous = 0; }
    public void Dispose() { _window?.Dispose(); _flowAverage?.Dispose(); _volumeAverage?.Dispose(); _flowFallback?.Dispose(); _volumeFallback?.Dispose(); }
}
