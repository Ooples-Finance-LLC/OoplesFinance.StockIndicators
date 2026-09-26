using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

// Retains the factors of each contribution so product overflow and expiry lose no information.
internal sealed class RollingMoneyFlowIndex : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<Observation> _window;
    private ExactMeanAccumulator _positiveScaled, _negative, _total;
    private double _previous;
    private bool _hasPrevious;

    private readonly struct Observation
    {
        internal Observation(double price, double volume, int direction)
        { Price = price; Volume = volume; Direction = direction; }
        internal double Price { get; }
        internal double Volume { get; }
        internal int Direction { get; }
    }

    internal RollingMoneyFlowIndex(int length)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<Observation>(_length);
    }

    internal static double TypicalPrice(double high, double low, double close)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(high); sum.Add(low); sum.Add(close);
        return sum.Mean(3);
    }

    internal double Next(double price, double volume, bool commit)
    {
        var direction = !_hasPrevious ? 0 : price > _previous ? 1 : price < _previous ? -1 : 0;
        var observation = new Observation(price, volume, direction);
        var positive = _positiveScaled;
        var negative = _negative;
        var total = _total;
        Apply(observation, 1, ref positive, ref negative, ref total);
        if (_window.Count == _length) Apply(_window[0], -1, ref positive, ref negative, ref total);
        var value = negative.IsExactlyZero ? 100 : positive.IsExactlyZero ? 0
            : Math.Max(0, Math.Min(100, positive.Ratio(total)));
        if (commit)
        {
            _positiveScaled = positive; _negative = negative; _total = total;
            _window.TryAdd(observation, out _);
            _previous = price; _hasPrevious = true;
        }
        return value;
    }

    private static void Apply(Observation observation, int weight, ref ExactMeanAccumulator positive,
        ref ExactMeanAccumulator negative, ref ExactMeanAccumulator total)
    {
        if (observation.Direction == 0) return;
        total.AddProduct(observation.Price, observation.Volume, weight);
        if (observation.Direction > 0) positive.AddProduct(observation.Price, observation.Volume, 100 * weight);
        else negative.AddProduct(observation.Price, observation.Volume, weight);
    }

    internal void Reset()
    {
        _window.Clear();
        _positiveScaled = default; _negative = default; _total = default;
        _previous = 0; _hasPrevious = false;
    }

    public void Dispose() => _window.Dispose();
}
