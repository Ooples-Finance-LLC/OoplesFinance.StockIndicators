using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VolumePositiveNegativeWindow : IDisposable
{
    private readonly int _length;
    private readonly Average _volume, _range, _signal;
    private readonly Queue<Number> _votes = new();
    private Number _sum, _previousPrice, _previousClose;
    private bool _started;
    internal VolumePositiveNegativeWindow(MovingAvgType kind, int length, int smooth)
    { _length = Math.Max(1, length); _volume = new(kind, _length); _range = new(kind, _length); _signal = new(kind, smooth); }
    private static Number Abs(Number value) => value.Sign < 0 ? value.Times(-1) : value;
    private static Number Range(Number high, Number low, Number previous)
    {
        var range = high - low;
        foreach (var gap in new[] { Abs(high - previous), Abs(low - previous) }) if ((gap - range).Sign > 0) range = gap;
        return range;
    }
    private static Number Vote(Number movement, Number atr, Number volume)
    {
        // Compare against one tenth of ATR without rounding either side first.
        var scaled = movement.Times(10);
        var positive = (scaled - atr).Sign > 0 ? volume : default;
        var negative = (scaled + atr).Sign < 0 ? volume : default;
        return positive - negative;
    }
    internal (double Line, double Signal) Next(double high, double low, double close, double volume, bool final, bool selected = false)
    {
        var top = Number.Of(high); var bottom = Number.Of(low); var price = Number.Of(close); var size = Number.Of(volume);
        var typical = selected ? price : (top + bottom + price).Divide(3);
        var atr = _range.Next(Range(top, bottom, _started ? _previousClose : price), final);
        var average = _volume.Next(size, final); if (average.Sign <= 0) average = Number.Integer(1);
        var vote = Vote(typical - _previousPrice, atr, size); var full = _votes.Count == _length;
        var sum = _sum + vote - (full ? _votes.Peek() : default);
        var line = sum.Times(100).Divide(average).Divide(_length); var signal = _signal.Next(line, final);
        if (final)
        {
            if (full) _votes.Dequeue(); _votes.Enqueue(vote); _sum = sum; _previousPrice = typical; _previousClose = price; _started = true;
        }
        return (line.Publish(), signal.Publish());
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int length, int smooth)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var selected = data.ChainedValues.Count > 0;
        var (close, high, low, _, _) = CalculationsHelper.GetInputValuesList(data);
        var prices = Enumerable.Range(0, data.Count).Select(i => selected ? Number.Of(close[i])
            : (Number.Of(data.HighPrices[i]) + Number.Of(data.LowPrices[i]) + Number.Of(data.ClosePrices[i])).Divide(3)).ToArray();
        var ranges = Enumerable.Range(0, data.Count).Select(i => Range(Number.Of(high[i]), Number.Of(low[i]), Number.Of(close[i == 0 ? 0 : i - 1]))).ToArray();
        Number[] Mean(Number[] values, int period, bool callback)
        {
            var supplied = callback ? ComponentAverage.Take(ComponentAverage.HasOverrides ? values.Select(v => v.Publish()).ToArray() : Array.Empty<double>(), period) : null;
            if (supplied is not null) return Enumerable.Range(0, values.Length).Select(i => i < supplied.Count ? Number.Of(supplied[i]) : default).ToArray();
            using var mean = new Average(kind, period); return values.Select(v => mean.Next(v, true)).ToArray();
        }
        var volumes = data.Volumes.Select(Number.Of).ToArray(); var volumeMean = Mean(volumes, length, true);
        var atr = Mean(ranges, length, ComponentAverage.HasOverrides); var line = new Number[prices.Length]; var votes = new Queue<Number>(); Number sum = default;
        for (var i = 0; i < prices.Length; i++)
        {
            var vote = Vote(prices[i] - (i == 0 ? default : prices[i - 1]), atr[i], volumes[i]);
            if (votes.Count == length) sum -= votes.Dequeue(); votes.Enqueue(vote); sum += vote;
            var divisor = volumeMean[i].Sign > 0 ? volumeMean[i] : Number.Integer(1); line[i] = sum.Times(100).Divide(divisor).Divide(length);
        }
        var signal = Mean(line, smooth, true); var trades = new Signal[line.Length]; Number previous = default;
        for (var i = 0; i < signal.Length; i++)
        {
            var value = signal[i]; trades[i] = value.Sign > 0 && (value - previous).Sign > 0 ? Signal.StrongBuy
                : value.Sign < 0 && (value - previous).Sign < 0 ? Signal.StrongSell : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None; previous = value;
        }
        return (new() { ["Vpni"] = line.Select(v => v.Publish()).ToArray(), ["Signal"] = signal.Select(v => v.Publish()).ToArray() }, trades);
    }
    internal void Reset() { _volume.Reset(); _range.Reset(); _signal.Reset(); _votes.Clear(); _sum = _previousPrice = _previousClose = default; _started = false; }
    public void Dispose() { _volume.Dispose(); _range.Dispose(); _signal.Dispose(); }
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
