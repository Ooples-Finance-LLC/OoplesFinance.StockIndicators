using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VolumeAdjustedWindow : IDisposable
{
    private readonly int _length;
    private readonly MovingAvgType _kind;
    private readonly bool _zeroFactor;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly Queue<double> _volumes = new();
    private readonly Queue<(RocBankValue Weight, ExactMeanAccumulator Product)> _terms = new();
    private ExactMeanAccumulator _volumeSum, _weightedVolume, _mass, _sum;
    private double _previousMean;
    private long _count;
    internal VolumeAdjustedWindow(MovingAvgType kind, int length, double factor = .67, bool initializeFallback = true)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _length = Math.Max(1, length); _kind = kind; _zeroFactor = factor == 0;
        if (!StrengthWindow.Supports(kind) && initializeFallback) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
    }
    private double Average(double volume, bool commit)
    {
        if (_fallback is not null) return _fallback.Next(volume, commit);
        var sum = _volumeSum; var weighted = _weightedVolume; double mean;
        if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
        {
            weighted.Subtract(sum); weighted.Add(volume, _length);
            if (_volumes.Count == _length) sum.Add(_volumes.Peek(), -1); sum.Add(volume);
            mean = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
                : _count + 1 < _length ? 0 : sum.Mean(_length);
        }
        else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
        { sum.Add(volume); mean = sum.Mean(_count + 1); }
        else
        {
            var next = new ExactMeanAccumulator(); next.Add(_previousMean, _length - 1);
            next.Add(volume, _kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1);
            mean = next.Mean(_kind == MovingAvgType.ExponentialMovingAverage ? _length + 1L : _length);
        }
        if (commit)
        {
            if (_volumes.Count == _length) _volumes.Dequeue(); _volumes.Enqueue(volume);
            _volumeSum = sum; _weightedVolume = weighted; _previousMean = mean; _count++;
        }
        return mean;
    }
    internal double NextMean(double volume, bool commit) => Average(volume, commit);
    internal double Next(double price, double volume, bool commit) => NextWithAverage(price, volume, Average(volume, commit), commit);
    internal double NextWithAverage(double price, double volume, double average, bool commit)
    {
        // A common nonzero adjustment factor cancels before quantizing relative-volume weights.
        var numerator = new ExactMeanAccumulator(); numerator.Add(volume);
        var weight = _zeroFactor || average == 0 ? default : RocBankValue.Round(numerator, average);
        var product = new ExactMeanAccumulator(); product.AddProduct(price, weight.Mantissa); product.ScaleByPowerOfTwo(weight.UpperShift);
        var mass = _mass; var sum = _sum;
        if (_terms.Count == _length) { var old = _terms.Peek(); old.Weight.AddTo(ref mass, -1); sum.Subtract(old.Product); }
        weight.AddTo(ref mass); var negative = new ExactMeanAccumulator(); negative.Subtract(product); sum.Subtract(negative);
        var result = sum.Ratio(mass);
        if (commit)
        {
            if (_terms.Count == _length) _terms.Dequeue(); _terms.Enqueue((weight, product)); _mass = mass; _sum = sum;
        }
        return result;
    }
    internal void Reset() { _fallback?.Reset(); _volumes.Clear(); _terms.Clear(); _volumeSum = _weightedVolume = _mass = _sum = default; _previousMean = 0; _count = 0; }
    public void Dispose() => _fallback?.Dispose();
}
