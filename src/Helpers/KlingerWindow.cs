using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

// Price-sum comparisons and trend-segment ranges are exact. Unpublished force,
// moving averages and their differences must not overflow or lose cancellation.
internal sealed class KlingerWindow : IDisposable
{
    private F _sum, _range, _cumulative, _histogram;
    private int _trend;
    private bool _started;
    private readonly Average _fast, _slow, _signal;
    internal KlingerWindow(MovingAvgType kind, int fast, int slow, int signal)
    { _fast = new(kind, fast); _slow = new(kind, slow); _signal = new(kind, signal); }

    internal F Force(double high, double low, double close, double volume, bool final)
    {
        var h = F.Of(high); var l = F.Of(low); var c = F.Of(close); var v = F.Of(volume);
        var sum = h + l + c; var direction = _started ? sum.CompareTo(_sum) : 0;
        var trend = direction == 0 ? _trend : direction;
        var range = h - l;
        var cumulative = trend == _trend ? _cumulative + range : _range + range;
        var force = cumulative.Sign == 0 ? (F)0 : v * (2 * range / cumulative - 1).Abs() * trend * 100;
        if (final) { _sum = sum; _range = range; _cumulative = cumulative; _trend = trend; _started = true; }
        return force;
    }

    internal (double Line, double SignalLine, double Histogram, Signal Trade) Finish(F line, F signal, bool final)
    {
        var histogram = line - signal; var change = histogram.CompareTo(_histogram);
        var trade = histogram.Sign > 0 && change > 0 ? Signal.StrongBuy
            : histogram.Sign < 0 && change < 0 ? Signal.StrongSell
            : histogram.Sign > 0 ? Signal.Buy : histogram.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _histogram = histogram;
        return (line.Publish(), signal.Publish(), histogram.Publish(), trade);
    }

    internal (double Line, double SignalLine, double Histogram, Signal Trade) Next(double high, double low, double close, double volume, bool final)
    {
        var force = Force(high, low, close, volume, final);
        var line = _fast.Next(force, final) - _slow.Next(force, final);
        return Finish(line, _signal.Next(line, final), final);
    }

    internal void Reset()
    { _sum = _range = _cumulative = _histogram = default; _trend = 0; _started = false; _fast.Reset(); _slow.Reset(); _signal.Reset(); }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); _signal.Dispose(); }

    internal sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<F> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private F _sum, _weighted, _previous;
        private long _count;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal F Next(F value, bool final)
        {
            if (_fallback is not null) return F.Of(_fallback.Next(value.Publish(), final));
            var sum = _sum; var weighted = _weighted; F result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted = weighted - sum + value * _length;
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted / ((long)_length * (_length + 1L) / 2)
                    : _count < _length - 1 ? 0 : sum / _length;
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum / (_count + 1); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous * (_length - 1L) + value * (ema ? 2 : 1)) / (ema ? _length + 1L : _length);
            }
            if (final)
            {
                if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }

    internal static (double[] Line, double[] SignalLine, double[] Histogram, Signal[] Trades) Calculate(StockData data,
        MovingAvgType kind, int fast, int slow, int signal, bool callbacks)
    {
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var values in new[] { prices, data.HighPrices, data.LowPrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var caller = data.CaptureInputSeries();
        F[] Mean(F[] values, int length, bool allowCallback)
        {
            length = Math.Max(1, length);
            var published = allowCallback && callbacks || !StrengthWindow.Supports(kind) ? values.Select(v => v.Publish()).ToArray() : null;
            var custom = allowCallback && callbacks ? ComponentAverage.Take(published!, length) : null;
            if (custom is not null) return Enumerable.Range(0, values.Length).Select(i => F.Of(i < custom.Count ? custom[i] : 0)).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, length, published!.ToList()).Select(F.Of).ToArray();
            using var average = new Average(kind, length); return values.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            using var window = new KlingerWindow(kind, fast, slow, signal);
            var force = Enumerable.Range(0, prices.Count).Select(i => window.Force(data.HighPrices[i], data.LowPrices[i], prices[i], data.Volumes[i], true)).ToArray();
            // The established EMA route has a coupled difference with no component callbacks for its legs.
            var legsHaveCallbacks = kind != MovingAvgType.ExponentialMovingAverage;
            var first = Mean(force, fast, legsHaveCallbacks); var second = Mean(force, slow, legsHaveCallbacks);
            var line = first.Zip(second, (f, s) => f - s).ToArray(); var smooth = Mean(line, signal, true);
            var points = line.Select((v, i) => window.Finish(v, smooth[i], true)).ToArray();
            return (points.Select(v => v.Line).ToArray(), points.Select(v => v.SignalLine).ToArray(), points.Select(v => v.Histogram).ToArray(), points.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }

    internal static void Core(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        ReadOnlySpan<double> volume, Span<double> output, int fast, int slow, int signal, bool signalOutput)
    {
        if (high.Length != close.Length || low.Length != close.Length || volume.Length != close.Length || output.Length < close.Length)
            throw new ArgumentException("Input lengths must agree and output must fit every input.");
        using var window = new KlingerWindow(MovingAvgType.ExponentialMovingAverage, fast, slow, signal);
        var values = new double[close.Length];
        for (var i = 0; i < values.Length; i++)
        { var point = window.Next(high[i], low[i], close[i], volume[i], true); values[i] = signalOutput ? point.SignalLine : point.Line; }
        values.AsSpan().CopyTo(output);
    }
}
