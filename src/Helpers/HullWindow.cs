using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HullWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    internal static int Half(int length) => Math.Max(1, Math.Min(530, (int)Math.Round(Math.Max(1, length) / 2d)));
    internal static int Root(int length) => Math.Max(1, Math.Min(530, (int)Math.Round(Math.Sqrt(Math.Max(1, length)))));
    internal HullWindow(MovingAvgType kind, int length)
    {
        var periods = new[] { Math.Max(1, length), Half(length), Root(length) };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(p => new RocBankAverage(kind, p, int.MaxValue)).ToArray();
        else _fallbacks = periods.Select(p => MovingAverageSmootherFactory.Create(kind, p)).ToArray();
    }
    private RocBankValue Smooth(RocBankValue value, int stage, bool commit) => _averages is null
        ? new RocBankValue(_fallbacks![stage].Next(value.Publish(), commit)) : _averages[stage].Next(value, commit);
    internal double Next(double price, bool commit)
    {
        var input = new RocBankValue(price); var full = Smooth(input, 0, commit); var half = Smooth(input, 1, commit);
        var total = new ExactMeanAccumulator(); half.AddTo(ref total, 2); full.AddTo(ref total, -1);
        return Smooth(RocBankValue.Round(total), 2, commit).Publish();
    }
    internal static double Combine(double full, double half)
    {
        var total = new ExactMeanAccumulator(); total.Add(half, 2); total.Add(full, -1); return total.Mean(1);
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
