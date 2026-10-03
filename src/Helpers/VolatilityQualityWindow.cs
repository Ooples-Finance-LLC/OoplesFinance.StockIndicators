using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VolatilityQualityWindow : IDisposable
{
    private readonly Average _fast, _slow;
    private Number _quality, _sum, _previousMargin;
    private double _previousPrice;
    private bool _hasPrevious;
    internal VolatilityQualityWindow(MovingAvgType kind, int fast, int slow) { _fast = new(kind, fast); _slow = new(kind, slow); }
    private static Number Abs(Number value) => value.Sign < 0 ? value.Times(-1) : value;
    internal Number Line(double open, double high, double low, double close, bool final, bool selected = false)
    {
        foreach (var value in new[] { open, high, low, close }) StreamingInputValidation.Finite(value, nameof(close));
        var prior = _hasPrevious ? _previousPrice : close;
        if (selected && !CalculationsHelper.IsWithinBarRange(close, low, high)) { high = Math.Max(prior, close); low = Math.Min(prior, close); }
        var price = Number.Of(close); var change = price - Number.Of(prior); var body = price - Number.Of(open);
        var range = Number.Of(high) - Number.Of(low); var trueRange = range;
        foreach (var candidate in new[] { Abs(Number.Of(high) - Number.Of(prior)), Abs(Number.Of(low) - Number.Of(prior)) })
            if ((candidate - trueRange).Sign > 0) trueRange = candidate;
        var quality = trueRange.Sign != 0 && range.Sign != 0 ? (change.Divide(trueRange) + body.Divide(range)).Divide(2) : _quality;
        var sum = _sum + Abs(quality) * (change + body).Divide(2);
        if (final) { _sum = sum; _quality = quality; _previousPrice = close; _hasPrevious = true; }
        return sum;
    }
    private static Signal Trade(Number margin, Number previous)
        => margin.Sign > 0 && (margin - previous).Sign > 0 ? Signal.StrongBuy
            : margin.Sign < 0 && (margin - previous).Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
    internal (double Line, double Fast, double Slow, Signal Trade) Next(double open, double high, double low, double close, bool final, bool selected = false)
    {
        var line = Line(open, high, low, close, final, selected); var fast = _fast.Next(line, final); var slow = _slow.Next(line, final);
        var margin = line - fast; var trade = Trade(margin, _previousMargin);
        if (final) _previousMargin = margin;
        return (line.Publish(), fast.Publish(), slow.Publish(), trade);
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int fast, int slow, string? fastOutput = null)
    {
        foreach (var values in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var (prices, highs, lows, opens, _) = CalculationsHelper.GetInputValuesList(data);
        using var state = new VolatilityQualityWindow(kind, fast, slow);
        var line = prices.Select((price, i) => state.Line(opens[i], highs[i], lows[i], price, true)).ToArray();
        Number[] Mean(int length)
        {
            length = Math.Max(1, length);
            var replacement = fastOutput is null ? null : ComponentAverage.Take(line.Select(v => v.Publish()).ToArray(), length);
            if (replacement is not null) return Enumerable.Range(0, line.Length).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            if (StrengthWindow.Supports(kind)) { using var mean = new Average(kind, length); return line.Select(v => mean.Next(v, true)).ToArray(); }
            return CalculationsHelper.GetMovingAverageList(data, kind, length, line.Select(v => v.Publish()).ToList()).Select(Number.Of).ToArray();
        }
        if (fastOutput is not null)
        {
            var output = fastOutput == "FastSignal" ? Mean(fast) : fastOutput == "SlowSignal" ? Mean(slow) : line;
            return (new Dictionary<string, double[]> { [fastOutput] = output.Select(v => v.Publish()).ToArray() }, Array.Empty<Signal>());
        }
        var fastValues = Mean(fast); var slowValues = Mean(slow); var trades = new Signal[line.Length]; Number previous = default;
        for (var i = 0; i < line.Length; i++) { var margin = line[i] - fastValues[i]; trades[i] = Trade(margin, previous); previous = margin; }
        return (new Dictionary<string, double[]> { ["Vqi"] = line.Select(v => v.Publish()).ToArray(), ["FastSignal"] = fastValues.Select(v => v.Publish()).ToArray(),
            ["SlowSignal"] = slowValues.Select(v => v.Publish()).ToArray() }, trades);
    }
    internal void Reset() { _fast.Reset(); _slow.Reset(); _quality = _sum = _previousMargin = default; _previousPrice = 0; _hasPrevious = false; }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); }
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
