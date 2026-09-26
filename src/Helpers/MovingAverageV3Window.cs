using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MovingAverageV3Window : IDisposable
{
    private readonly RocBankAverage? _first, _second;
    private readonly IMovingAverageSmoother? _firstFallback, _secondFallback;
    private readonly int _length1, _length2;
    internal MovingAverageV3Window(MovingAvgType kind, int length1, int length2)
    {
        _length1 = Math.Max(1, length1); _length2 = Math.Max(1, length2);
        if (StrengthWindow.Supports(kind)) { _first = new(kind, _length1, int.MaxValue); _second = new(kind, _length2, int.MaxValue); }
        else { _firstFallback = MovingAverageSmootherFactory.Create(kind, _length1); _secondFallback = MovingAverageSmootherFactory.Create(kind, _length2); }
    }
    internal static double Combine(double first, double second, int length1, int length2)
    {
        var numerator = Math.Max(1, length1) - 1L; var denominator = Math.Max(1, length2) - 1L;
        if (denominator == 0) return first;
        var sum = new ExactMeanAccumulator(); new RocBankValue(first).AddTo(ref sum, numerator + denominator); new RocBankValue(second).AddTo(ref sum, -numerator);
        return sum.Mean(denominator);
    }
    internal double Next(double price, bool commit)
    {
        var first = _first is null ? _firstFallback!.Next(price, commit) : _first.Next(new RocBankValue(price), commit).Publish();
        var second = _second is null ? _secondFallback!.Next(price, commit) : _second.Next(new RocBankValue(price), commit).Publish();
        return Combine(first, second, _length1, _length2);
    }
    internal void Reset() { _first?.Reset(); _second?.Reset(); _firstFallback?.Reset(); _secondFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _firstFallback?.Dispose(); _secondFallback?.Dispose(); }
}
