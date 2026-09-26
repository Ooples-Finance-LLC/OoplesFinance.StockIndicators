using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TrixWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private RocBankValue _previous;
    internal TrixWindow(MovingAvgType kind, int length)
    {
        if (StrengthWindow.Supports(kind)) _averages = Enumerable.Range(0, 3).Select(_ => new RocBankAverage(kind, length, int.MaxValue)).ToArray();
        else _fallbacks = Enumerable.Range(0, 3).Select(_ => MovingAverageSmootherFactory.Create(kind, Math.Max(1, length))).ToArray();
    }
    internal RocBankValue Next(double price, bool commit, double? customerFirst = null, double? customerSecond = null, double? customerThird = null)
    {
        RocBankValue Smooth(RocBankValue value, int stage, double? customer) => customer.HasValue ? new RocBankValue(customer.Value)
            : _averages is null ? new RocBankValue(_fallbacks![stage].Next(value.Publish(), commit)) : _averages[stage].Next(value, commit);
        var first = Smooth(new RocBankValue(price), 0, customerFirst);
        var second = Smooth(first, 1, customerSecond); var third = Smooth(second, 2, customerThird);
        var numerator = new ExactMeanAccumulator(); third.AddTo(ref numerator, 100); _previous.AddTo(ref numerator, -100);
        var result = _previous.Mantissa == 0 ? default : RocBankValue.Round(numerator, Math.Abs(_previous.Publish()));
        if (commit) _previous = third;
        return result;
    }
    internal void Reset()
    {
        _previous = default;
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
