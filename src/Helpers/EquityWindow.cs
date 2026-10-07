using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EquityWindow : IDisposable
{
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly PooledRingBuffer<RocBankValue> _changes;
    private ExactMeanAccumulator _requested, _cumulative;
    private double _price, _equity;
    private int _side;
    private bool _started;
    internal EquityWindow(MovingAvgType kind, int length, bool initializeFallback = true)
    {
        length = Math.Max(1, length); _changes = new(length);
        if (StrengthWindow.Supports(kind)) _average = new(kind, length, int.MaxValue);
        else if (initializeFallback) _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal double Next(double price, bool commit) => NextWithAverage(price,
        _average is null ? _fallback!.Next(price, commit) : _average.Next(new RocBankValue(price), commit).Publish(), commit);
    internal double NextWithAverage(double price, double average, bool commit)
    {
        var side = price.CompareTo(average);
        var difference = new ExactMeanAccumulator();
        if (_started) { difference.Add(price); difference.Add(_price, -1); }
        var change = RocBankValue.Round(difference);
        var requestedChange = change.Multiply(_side);
        var requested = _requested; var cumulative = _cumulative;
        if (_changes.Count == _changes.Capacity) _changes[0].AddTo(ref requested, -1);
        requestedChange.AddTo(ref requested); change.AddTo(ref cumulative, side);
        var gain = cumulative.IsExactlyZero ? .99 : Math.Max(.01, Math.Min(.99, requested.Ratio(cumulative)));
        var previous = _started ? _equity : price;
        var sum = new ExactMeanAccumulator(); sum.Add(previous); sum.AddProduct(price, gain); sum.AddProduct(previous, -gain);
        var result = sum.Mean(1);
        if (commit)
        {
            _changes.TryAdd(requestedChange, out _); _requested = requested; _cumulative = cumulative;
            _price = price; _equity = result; _side = side; _started = true;
        }
        return result;
    }
    internal void Reset() { _average?.Reset(); _fallback?.Reset(); _changes.Clear(); _requested = _cumulative = default; _price = _equity = 0; _side = 0; _started = false; }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); _changes.Dispose(); }
}
