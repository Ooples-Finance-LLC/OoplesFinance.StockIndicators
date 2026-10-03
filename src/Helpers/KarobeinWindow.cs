using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class KarobeinWindow : IDisposable
{
    private readonly Average _price, _fall, _rise;
    private Number _previous, _line, _slope;
    internal KarobeinWindow(MovingAvgType kind, int length)
    { _price = new(kind, length); _fall = new(kind, length); _rise = new(kind, length); }
    private static Number Clamp(Number value) => value.Sign < 0 ? default : (value - Number.Integer(1)).Sign > 0 ? Number.Integer(1) : value;
    private static Number Fold(Number ratio, Number fall, Number rise)
    {
        if (ratio.Sign == 0) return default;
        var first = ratio + rise;
        if (first.Sign == 0) throw new ArgumentException("Karobein has an exact pole in its rising-ratio denominator.");
        var c = Clamp(ratio.Divide(first));
        var second = ratio + c * fall;
        if (second.Sign == 0) throw new ArgumentException("Karobein has an exact pole in its falling-ratio denominator.");
        return Clamp(ratio.Times(2).Divide(second) - Number.Integer(1));
    }
    private static Signal Trade(Number line, Number previous, Number oldSlope)
    {
        var slope = line - previous; var acceleration = slope - oldSlope;
        return slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
    }
    internal (double Line, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var input = Number.Of(price);
        // Preview every stage first: a pole rejection cannot commit only part of a bar.
        var mean = _price.Next(input, false); var delta = mean - _previous;
        var ratio = _previous.Sign == 0 ? default : mean.Divide(_previous);
        var falling = delta.Sign < 0 ? ratio : default; var rising = delta.Sign > 0 ? ratio : default;
        var fall = _fall.Next(falling, false); var rise = _rise.Next(rising, false);
        var line = Fold(ratio, fall, rise); var trade = Trade(line, _line, _slope); var published = line.Publish();
        if (final)
        {
            _price.Next(input, true); _fall.Next(falling, true); _rise.Next(rising, true);
            _previous = mean; _slope = line - _line; _line = line;
        }
        return (published, trade);
    }
    internal void Reset() { _price.Reset(); _fall.Reset(); _rise.Reset(); _previous = _line = _slope = default; }
    public void Dispose() { _price.Dispose(); _fall.Dispose(); _rise.Dispose(); }
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues, data.InputValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = input.Count; var caller = data.CaptureInputSeries();
        Number[] Mean(Number[] values)
        {
            var replacement = callbacks ? ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), length) : null;
            if (replacement is not null) return Enumerable.Range(0, count).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, length, values.Select(v => v.Publish()).ToList()).Select(Number.Of).ToArray();
            using var average = new Average(kind, length); return values.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            var mean = Mean(input.Select(Number.Of).ToArray()); var ratios = new Number[count]; var falls = new Number[count]; var rises = new Number[count];
            for (var i = 1; i < count; i++)
            {
                ratios[i] = mean[i - 1].Sign == 0 ? default : mean[i].Divide(mean[i - 1]);
                var delta = mean[i] - mean[i - 1];
                if (delta.Sign < 0) falls[i] = ratios[i]; else if (delta.Sign > 0) rises[i] = ratios[i];
            }
            var fall = Mean(falls); var rise = Mean(rises); var values = new double[count]; var signals = new Signal[count];
            Number previous = default, slope = default;
            for (var i = 0; i < count; i++)
            {
                var line = Fold(ratios[i], fall[i], rise[i]); values[i] = line.Publish(); signals[i] = Trade(line, previous, slope);
                slope = line - previous; previous = line;
            }
            return (values, signals);
        }
        finally { data.RestoreInputSeries(caller); }
    }
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
