using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ReversalPointsWindow : IDisposable
{
    private readonly StrengthAverage? _first, _second;
    private readonly IMovingAverageSmoother? _fallbackFirst, _fallbackSecond;
    private readonly PooledRingBuffer<double> _ratios;
    private readonly RollingWindowSum? _fallbackSum;
    private ExactMeanAccumulator _sum;
    private double _previous;

    internal ReversalPointsWindow(MovingAvgType kind, int length)
    {
        var smooth = MathHelper.MinOrMax((int)Math.Ceiling(Math.Max(1, length) / 2d));
        _ratios = new PooledRingBuffer<double>(Math.Max(1, length));
        if (StrengthWindow.Supports(kind))
        { _first = new StrengthAverage(kind, smooth); _second = new StrengthAverage(kind, smooth); }
        else
        { _fallbackFirst = MovingAverageSmootherFactory.Create(kind, smooth); _fallbackSecond = MovingAverageSmootherFactory.Create(kind, smooth); _fallbackSum = new RollingWindowSum(Math.Max(1, length)); }
    }

    internal double Next(double price, bool commit)
    {
        double ratio;
        if (_first is not null)
        {
            var change = new ExactMeanAccumulator(); change.Add(price); change.Add(_previous, -1);
            var first = _first.Next(StrengthValue.Round(change, 1).Absolute, commit);
            var second = _second!.Next(first, commit);
            var numerator = new ExactMeanAccumulator(); first.AddTo(ref numerator);
            var denominator = new ExactMeanAccumulator(); second.AddTo(ref denominator);
            ratio = numerator.Ratio(denominator);
        }
        else
        {
            var first = _fallbackFirst!.Next(Math.Abs(price - _previous), commit);
            var second = _fallbackSecond!.Next(first, commit);
            ratio = second == 0 ? 0 : first / second;
        }
        var value = _fallbackSum is null ? NextRatio(ratio, commit) : commit ? _fallbackSum.Add(ratio, out _) : _fallbackSum.Preview(ratio, out _);
        if (commit) _previous = price;
        return value;
    }

    internal double NextRatio(double ratio, bool commit)
    {
        var sum = _sum;
        if (_ratios.Count == _ratios.Capacity) sum.Add(_ratios[0], -1);
        sum.Add(ratio);
        if (commit) { _sum = sum; _ratios.TryAdd(ratio, out _); }
        return sum.Mean(1);
    }
    internal void Reset()
    { _previous = 0; _sum = default; _ratios.Clear(); _first?.Reset(); _second?.Reset(); _fallbackFirst?.Reset(); _fallbackSecond?.Reset(); _fallbackSum?.Reset(); }
    public void Dispose()
    { _ratios.Dispose(); _first?.Dispose(); _second?.Dispose(); _fallbackFirst?.Dispose(); _fallbackSecond?.Dispose(); _fallbackSum?.Dispose(); }
}
