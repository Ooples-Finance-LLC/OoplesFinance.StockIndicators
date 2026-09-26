using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RelativeVolumeWindow : IDisposable
{
    private readonly StrengthAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly StandardizedScoreWindow _score;
    private readonly bool _simple;
    private bool _started;
    private double _previousPrice, _demand;
    internal RelativeVolumeWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length); _simple = kind == MovingAvgType.SimpleMovingAverage;
        if (StrengthWindow.Supports(kind)) _average = new StrengthAverage(kind, length);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
        _score = new StandardizedScoreWindow(length, false);
    }
    internal (double Score, double Demand) Next(double price, double volume, bool commit, double? customerMean = null)
    {
        var average = customerMean ?? (_average is null ? _fallback!.Next(volume, commit) : _average.Next(new StrengthValue(volume), commit).Mantissa);
        var score = _score.Next(volume, average, _simple && !customerMean.HasValue, commit);
        var demand = score >= 2 ? _previousPrice : _started ? _demand : price;
        if (commit) { _previousPrice = price; _demand = demand; _started = true; }
        return (score, demand);
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _score.Reset(); _previousPrice = _demand = 0; _started = false; }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); _score.Dispose(); }
}
