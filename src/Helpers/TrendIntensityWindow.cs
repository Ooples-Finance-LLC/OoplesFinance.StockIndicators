using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TrendIntensityWindow : IDisposable
{
    private readonly StrengthAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly PooledRingBuffer<(double Price, double Mean)> _window;
    private ExactMeanAccumulator _up, _total;
    internal TrendIntensityWindow(MovingAvgType kind, int fastLength, int slowLength)
    {
        slowLength = Math.Max(1, slowLength); _window = new(Math.Max(1, fastLength));
        if (StrengthWindow.Supports(kind)) _average = new StrengthAverage(kind, slowLength);
        else _fallback = MovingAverageSmootherFactory.Create(kind, slowLength);
    }
    private static void Add(ref ExactMeanAccumulator up, ref ExactMeanAccumulator total, double price, double mean, int weight)
    {
        if (price > mean) { up.Add(price, 100 * weight); up.Add(mean, -100 * weight); }
        var direction = price >= mean ? weight : -weight;
        total.Add(price, direction); total.Add(mean, -direction);
    }
    internal double Next(double price, bool commit, double? customerMean = null)
    {
        var mean = customerMean ?? (_average is null ? _fallback!.Next(price, commit) : _average.Next(new StrengthValue(price), commit).Mantissa);
        var up = _up; var total = _total;
        if (_window.Count == _window.Capacity)
        { var expired = _window[0]; Add(ref up, ref total, expired.Price, expired.Mean, -1); }
        Add(ref up, ref total, price, mean, 1);
        var value = up.Ratio(total);
        if (commit) { _window.TryAdd((price, mean), out _); _up = up; _total = total; }
        return value;
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _window.Clear(); _up = _total = default; }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); _window.Dispose(); }
}
