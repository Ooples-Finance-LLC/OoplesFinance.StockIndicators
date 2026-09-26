using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class LightLeastSquaresWindow : IDisposable
{
    private readonly int _length;
    private readonly double _indexDeviation;
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private long _index;
    private int _constantRun;
    private double _lastPrice;
    internal LightLeastSquaresWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue, bool initializeFallback = true)
    {
        _length = Math.Max(1, length);
        var half = Math.Max(2, Math.Min(530, (int)((_length + 1L) / 2)));
        var periods = new[] { _length, half, _length };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(p => new RocBankAverage(kind, p, capacityHint)).ToArray();
        else if (initializeFallback) _fallbacks = periods.Select(p => MovingAverageSmootherFactory.Create(kind, p)).ToArray();
        _indexDeviation = ExactPopulationDeviation.RootRatio((((BigInteger)_length * _length) - 1) << 2148, 12);
    }
    private RocBankValue Smooth(double value, int stage, bool commit) => _averages is null
        ? new RocBankValue(_fallbacks![stage].Next(value, commit)) : _averages[stage].Next(new RocBankValue(value), commit);
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue left, RocBankValue right, int weight = 1)
    {
        var term = new ExactMeanAccumulator(); term.AddProduct(left.Mantissa, right.Mantissa, weight); term.ScaleByPowerOfTwo(left.UpperShift + right.UpperShift);
        var negative = new ExactMeanAccumulator(); negative.Subtract(term); sum.Subtract(negative);
    }
    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var first = Smooth(price, 0, commit); var second = Smooth(price, 1, commit); var indexMean = Smooth(_index, 2, commit);
        return Finish(price, first, second, indexMean, commit);
    }
    internal double NextWithAverages(double price, double first, double second, double indexMean, bool commit) =>
        Finish(price, new RocBankValue(first), new RocBankValue(second), new RocBankValue(indexMean), commit);
    private double Finish(double price, RocBankValue first, RocBankValue second, RocBankValue indexMean, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var run = _constantRun > 0 && price == _lastPrice ? (int)Math.Min(_length, _constantRun + 1L) : 1;
        var result = first.Publish();
        // Population deviations use full windows. Cancel the nonzero price
        // deviation algebraically, avoiding its overflow and subnormal underflow.
        if (_index + 1 >= _length && _indexDeviation != 0 && run < _length)
        {
            var index = new RocBankValue(_index); var numerator = new ExactMeanAccumulator();
            Product(ref numerator, first, new RocBankValue(_indexDeviation));
            Product(ref numerator, index, second); Product(ref numerator, index, first, -1);
            Product(ref numerator, indexMean, second, -1); Product(ref numerator, indexMean, first);
            result = RocBankValue.Round(numerator, _indexDeviation).Publish();
        }
        if (commit) { _index++; _constantRun = run; _lastPrice = price; }
        return result;
    }
    internal void Reset()
    {
        _index = 0; _constantRun = 0; _lastPrice = 0;
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
