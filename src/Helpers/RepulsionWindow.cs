using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RepulsionWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    internal static int Period(int length, int multiple) => (int)Math.Min(int.MaxValue, Math.Max(1, (long)length) * multiple);
    internal RepulsionWindow(MovingAvgType kind, int length)
    {
        var periods = new[] { Period(length, 1), Period(length, 2), Period(length, 3) };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(p => new RocBankAverage(kind, p, int.MaxValue)).ToArray();
        else _fallbacks = periods.Select(p => MovingAverageSmootherFactory.Create(kind, p)).ToArray();
    }
    private RocBankValue Smooth(RocBankValue value, int stage, bool commit) => _averages is null
        ? new RocBankValue(_fallbacks![stage].Next(value.Publish(), commit)) : _averages[stage].Next(value, commit);
    internal double Next(double price, bool commit)
    {
        var input = new RocBankValue(price); var first = Smooth(input, 0, commit); var second = Smooth(input, 1, commit); var third = Smooth(input, 2, commit);
        var total = new ExactMeanAccumulator(); first.AddTo(ref total, -1); second.AddTo(ref total, 1); third.AddTo(ref total, 1);
        return total.Mean(1);
    }
    internal static double Combine(double first, double second, double third)
    {
        var total = new ExactMeanAccumulator(); total.Add(first, -1); total.Add(second, 1); total.Add(third, 1); return total.Mean(1);
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
