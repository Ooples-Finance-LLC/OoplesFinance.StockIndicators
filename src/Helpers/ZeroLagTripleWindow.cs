using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ZeroLagTripleWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private readonly bool _triple;
    internal ZeroLagTripleWindow(MovingAvgType kind, int length)
    {
        if (kind == MovingAvgType.ZeroLagTripleExponentialMovingAverage) kind = MovingAvgType.TripleExponentialMovingAverage;
        _triple = kind == MovingAvgType.TripleExponentialMovingAverage;
        if (_triple || StrengthWindow.Supports(kind))
            _averages = Enumerable.Range(0, _triple ? 6 : 2).Select(_ => new RocBankAverage(_triple ? MovingAvgType.ExponentialMovingAverage : kind, length, int.MaxValue)).ToArray();
        else _fallbacks = Enumerable.Range(0, 2).Select(_ => MovingAverageSmootherFactory.Create(kind, Math.Max(1, length))).ToArray();
    }
    private RocBankValue Smooth(RocBankValue value, int stage, bool commit)
    {
        if (_averages is null) return new RocBankValue(_fallbacks![stage].Next(value.Publish(), commit));
        if (!_triple) return _averages[stage].Next(value, commit);
        var first = _averages[stage * 3].Next(value, commit);
        var second = _averages[stage * 3 + 1].Next(first, commit);
        var third = _averages[stage * 3 + 2].Next(second, commit);
        var total = new ExactMeanAccumulator(); first.AddTo(ref total, 3); second.AddTo(ref total, -3); third.AddTo(ref total);
        return RocBankValue.Round(total);
    }
    internal double Next(double price, bool commit, double? customerFirst = null, double? customerSecond = null)
    {
        var first = customerFirst.HasValue ? new RocBankValue(customerFirst.Value) : Smooth(new RocBankValue(price), 0, commit);
        var second = customerSecond.HasValue ? new RocBankValue(customerSecond.Value) : Smooth(first, 1, commit);
        var total = new ExactMeanAccumulator(); first.AddTo(ref total, 2); second.AddTo(ref total, -1);
        return total.Mean(1);
    }
    internal void Reset()
    {
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
