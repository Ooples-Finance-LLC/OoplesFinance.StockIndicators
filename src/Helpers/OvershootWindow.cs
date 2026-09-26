using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class OvershootWindow : IDisposable
{
    private readonly int _length;
    private readonly ExactLinearFitWindow _fit;
    private readonly PooledRingBuffer<BigInteger> _errors, _means;
    private readonly SortedSet<BigInteger> _sorted = new();
    private readonly Dictionary<BigInteger, int> _frequencies = new();
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private BigInteger _sum, _previous, _price;
    private long _index;
    internal OvershootWindow(MovingAvgType kind, int length, bool initializeFallback = true)
    {
        _length = Math.Max(1, length); _fit = new(_length);
        _errors = new((int)((_length + 1L) / 2)); _means = new(_length);
        if (StrengthWindow.Supports(kind)) _averages = new[] { new RocBankAverage(kind, _length, int.MaxValue), new RocBankAverage(kind, _length, int.MaxValue) };
        else if (initializeFallback) _fallbacks = new[] { MovingAverageSmootherFactory.Create(kind, _length), MovingAverageSmootherFactory.Create(kind, _length) };
    }
    private double Average(double value, int stage, bool commit) => _averages is null
        ? _fallbacks![stage].Next(value, commit) : _averages[stage].Next(new RocBankValue(value), commit).Publish();
    internal double Next(double price, bool commit, double? priceMean = null, double? indexMean = null)
    {
        var units = ExactVarianceWindow.Units(price);
        var fit = _fit.Next(price, commit);
        var meanPrice = priceMean ?? Average(price, 0, commit); var meanIndex = indexMean ?? Average(_index, 1, commit);
        var previous = _previous.IsZero ? _price : _previous;
        var error = RocBankValue.RoundUnits(BigInteger.Abs(previous - units), BigInteger.One);
        var sum = _sum + error; var count = Math.Min(_errors.Capacity, _errors.Count + 1);
        if (_errors.Count == _errors.Capacity) sum -= _errors[0];
        var mean = RocBankValue.RoundUnits(sum, count);
        var highest = _sorted.Count == 0 ? BigInteger.Zero : _sorted.Max;
        if (_means.Count == _means.Capacity && _means[0] == highest && _frequencies[highest] == 1)
            highest = _sorted.Count == 1 ? BigInteger.Zero : _sorted.GetViewBetween(BigInteger.Zero, highest - 1).Max;
        highest = BigInteger.Max(highest, mean);
        var gain = highest.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(mean << 1074, highest);
        var result = fit.Count < _length ? ExactVarianceWindow.Units(meanPrice) : fit.CenteredUnits(meanPrice, meanIndex, gain);
        if (commit)
        {
            if (_means.Count == _means.Capacity)
            {
                var expired = _means[0];
                if (--_frequencies[expired] == 0) { _frequencies.Remove(expired); _sorted.Remove(expired); }
            }
            _frequencies.TryGetValue(mean, out var frequency); _frequencies[mean] = frequency + 1; _sorted.Add(mean);
            _errors.TryAdd(error, out _); _means.TryAdd(mean, out _); _sum = sum; _previous = result; _price = units; _index++;
        }
        return ExactMeanAccumulator.UnitRatio(result, BigInteger.One);
    }
    internal void Reset()
    {
        _fit.Reset(); _errors.Clear(); _means.Clear(); _sorted.Clear(); _frequencies.Clear(); _sum = _previous = _price = default; _index = 0;
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        _fit.Dispose(); _errors.Dispose(); _means.Dispose();
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
