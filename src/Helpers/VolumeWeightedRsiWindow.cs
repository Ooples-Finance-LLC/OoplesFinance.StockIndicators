using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VolumeWeightedRsiWindow : IDisposable
{
    private readonly Average _up, _down, _smooth;
    private double _previousPrice;
    private bool _hasPrevious;
    internal VolumeWeightedRsiWindow(MovingAvgType kind, int length, int smoothLength)
    { _up = new(kind, length); _down = new(kind, length); _smooth = new(kind, smoothLength); }

    private static Number Center(Number up, Number down)
        => down.Sign == 0 ? Number.Integer(100) : up.Sign == 0 ? Number.Integer(-100)
            : (up - down).Times(100).Divide(up + down);

    internal double Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        StreamingInputValidation.Finite(volume, nameof(volume));
        // Classify after multiplying: finite negative volume reverses the flow.
        var flow = _hasPrevious ? (Number.Of(price) - Number.Of(_previousPrice)) * Number.Of(volume) : default;
        var up = _up.Next(flow.Sign > 0 ? flow : default, final);
        var down = _down.Next(flow.Sign < 0 ? flow.Times(-1) : default, final);
        var result = _smooth.Next(Center(up, down), final).Publish();
        if (final) { _previousPrice = price; _hasPrevious = true; }
        return result;
    }

    internal static double[] Calculate(StockData data, MovingAvgType kind, int length, int smoothLength, bool fast = false)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        var (prices, _, _, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var gains = new Number[prices.Count]; var losses = new Number[prices.Count];
        for (var i = 1; i < prices.Count; i++)
        {
            var flow = (Number.Of(prices[i]) - Number.Of(prices[i - 1])) * Number.Of(volumes[i]);
            if (flow.Sign > 0) gains[i] = flow; else losses[i] = flow.Times(-1);
        }
        Number[] Mean(Number[] values, int period)
        {
            var replacement = fast ? ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), period) : null;
            if (replacement is not null)
                return Enumerable.Range(0, prices.Count).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            if (!StrengthWindow.Supports(kind))
                return CalculationsHelper.GetMovingAverageList(data, kind, period, values.Select(v => v.Publish()).ToList()).Select(Number.Of).ToArray();
            using var average = new Average(kind, period);
            return values.Select(v => average.Next(v, true)).ToArray();
        }
        var up = Mean(gains, length); var down = Mean(losses, length);
        var centered = Enumerable.Range(0, prices.Count).Select(i => Center(up[i], down[i])).ToArray();
        return Mean(centered, smoothLength).Select(v => v.Publish()).ToArray();
    }
    internal void Reset() { _up.Reset(); _down.Reset(); _smooth.Reset(); _previousPrice = 0; _hasPrevious = false; }
    public void Dispose() { _up.Dispose(); _down.Dispose(); _smooth.Dispose(); }
    // Keep recursive gains/losses exact too: premature rounding can erase
    // a small imbalance between otherwise equal large flows.
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous;
        private long _count;
        internal Average(MovingAvgType kind, int length)
        {
            _kind = kind; _length = Math.Max(1, length);
            if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
        }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Number result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek();
                sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage
                    ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1))
                    .Divide(ema ? _length + 1L : _length);
            }
            if (final)
            {
                if (finite)
                {
                    if (_history.Count == _length) _history.Dequeue();
                    _history.Enqueue(value);
                }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
}
