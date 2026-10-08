using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// LazyBear calc_zvwap keeps the historical residuals, each against its own trailing mean.
internal sealed class ZDistanceWindow : IDisposable
{
    private readonly int _length;
    private readonly Average? _mean;
    private readonly Queue<(Number Product, Number Volume)> _flow = new();
    private readonly Queue<Number> _squares = new();
    private Number _product, _volume, _squareSum;
    private ZDistanceRoot _previous, _older;
    internal Signal LastSignal { get; private set; }
    internal ZDistanceWindow(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length);
        if (kind != MovingAvgType.VolumeWeightedAveragePrice) _mean = new(kind, _length);
    }
    internal double Next(double price, double volume, bool final, Number? meanOverride = null)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var current = Number.Of(price); var weight = Number.Of(volume); var product = current * weight;
        var products = _product + product; var volumes = _volume + weight;
        if (_flow.Count == _length) { products -= _flow.Peek().Product; volumes -= _flow.Peek().Volume; }
        var mean = meanOverride ?? (_mean is null ? volumes.Sign == 0 ? default : products.Divide(volumes) : _mean.Next(current, final));
        var residual = current - mean; var square = residual * residual; var squares = _squareSum + square;
        if (_squares.Count == _length) squares -= _squares.Peek();
        var ratio = _squares.Count + 1 < _length || squares.Sign == 0 ? default : square.Times(_length).Divide(squares);
        var value = residual.Sign * ExactPopulationDeviation.RootRatio(ratio.Numerator << 2148, ratio.Denominator);
        var root = new ZDistanceRoot(ratio, residual.Sign);
        LastSignal = ZDistanceRoot.Vote(root, _previous, _older);
        if (final)
        {
            if (_flow.Count == _length) _flow.Dequeue();
            if (_squares.Count == _length) _squares.Dequeue();
            _flow.Enqueue((product, weight)); _squares.Enqueue(square);
            _product = products; _volume = volumes; _squareSum = squares;
            _older = _previous; _previous = root;
        }
        return value;
    }
    internal static double[] Calculate(StockData data, MovingAvgType kind, int length, ICollection<Signal>? signals = null)
    {
        var (input, _, _, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        using var state = new ZDistanceWindow(kind, length);
        var fallback = kind != MovingAvgType.VolumeWeightedAveragePrice && !StrengthWindow.Supports(kind)
            ? CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, length), input) : null;
        return input.Select((value, i) => { var result = state.Next(value, volumes[i], true, fallback is null ? null : Number.Of(fallback[i])); signals?.Add(state.LastSignal); return result; }).ToArray();
    }
    internal void Reset() { _flow.Clear(); _squares.Clear(); _product = _volume = _squareSum = default; _mean?.Reset(); _previous = _older = default; LastSignal = Signal.None; }
    public void Dispose() => _mean?.Dispose();
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
