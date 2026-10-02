using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VpciWindow : IDisposable
{
    private readonly VolumeMean _weightedFast, _weightedSlow;
    private readonly Average _priceFast, _priceSlow, _volumeFast, _volumeSlow, _signal;
    private Number _previousMargin;
    internal VpciWindow(MovingAvgType kind, int fast, int slow, int signal)
    {
        _weightedFast = new(fast); _weightedSlow = new(slow);
        _priceFast = new(kind, fast); _priceSlow = new(kind, slow);
        _volumeFast = new(kind, fast); _volumeSlow = new(kind, slow); _signal = new(kind, signal);
    }
    private static Number Combine(Number weightedFast, Number weightedSlow, Number priceFast, Number priceSlow,
        Number volumeFast, Number volumeSlow)
        => priceFast.Sign == 0 || volumeSlow.Sign == 0 ? default
            : (weightedSlow - priceSlow) * weightedFast.Divide(priceFast) * volumeFast.Divide(volumeSlow);
    private (double Line, double SignalLine, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var margin = line - signal; var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousMargin = margin;
        return (line.Publish(), signal.Publish(), trade);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var p = Number.Of(price); var v = Number.Of(volume);
        var line = Combine(_weightedFast.Next(p, v, final), _weightedSlow.Next(p, v, final),
            _priceFast.Next(p, final), _priceSlow.Next(p, final), _volumeFast.Next(v, final), _volumeSlow.Next(v, final));
        return Finish(line, _signal.Next(line, final), final);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, MovingAvgType kind,
        int fast, int slow, int signal, bool fastRoute, bool includeSignal = true)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); signal = Math.Max(1, signal);
        var (prices, _, _, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var p = prices.Select(Number.Of).ToArray(); var v = volumes.Select(Number.Of).ToArray();
        Number[] Mean(Number[] values, int period, bool weighted = false)
        {
            var replacement = ComponentAverage.Take(values.Select(value => value.Publish()).ToArray(), period);
            if (replacement is not null)
                return Enumerable.Range(0, p.Length).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            if (weighted)
            {
                var mean = new VolumeMean(period);
                return Enumerable.Range(0, p.Length).Select(i => mean.Next(p[i], v[i], true)).ToArray();
            }
            if (!StrengthWindow.Supports(kind))
                return CalculationsHelper.GetMovingAverageList(data, kind, period, values.Select(value => value.Publish()).ToList()).Select(Number.Of).ToArray();
            using var average = new Average(kind, period);
            return values.Select(value => average.Next(value, true)).ToArray();
        }
        var wf = Mean(p, fast, true); var ws = Mean(p, slow, true);
        Number[] pf, ps, vf, vs;
        // The explicit fast path historically requests price means before volume
        // means; batch requests volume first. Preserve the callback slot contracts.
        if (fastRoute) { pf = Mean(p, fast); ps = Mean(p, slow); vf = Mean(v, fast); vs = Mean(v, slow); }
        else { vf = Mean(v, fast); vs = Mean(v, slow); pf = Mean(p, fast); ps = Mean(p, slow); }
        var raw = Enumerable.Range(0, p.Length).Select(i => Combine(wf[i], ws[i], pf[i], ps[i], vf[i], vs[i])).ToArray();
        var smoothed = includeSignal ? Mean(raw, signal) : new Number[p.Length];
        var lines = new double[p.Length]; var signals = new double[p.Length]; var trades = new Signal[p.Length];
        using var window = new VpciWindow(kind, fast, slow, signal);
        for (var i = 0; i < p.Length; i++)
        {
            var point = window.Finish(raw[i], smoothed[i], true);
            lines[i] = point.Line; signals[i] = point.SignalLine; trades[i] = point.Trade;
        }
        return (lines, signals, trades);
    }
    internal void Reset()
    {
        _weightedFast.Reset(); _weightedSlow.Reset(); _priceFast.Reset(); _priceSlow.Reset();
        _volumeFast.Reset(); _volumeSlow.Reset(); _signal.Reset(); _previousMargin = default;
    }
    public void Dispose() { _priceFast.Dispose(); _priceSlow.Dispose(); _volumeFast.Dispose(); _volumeSlow.Dispose(); _signal.Dispose(); }

    private sealed class VolumeMean
    {
        private readonly int _length;
        private readonly Queue<(Number Product, Number Volume)> _history = new();
        private Number _products, _volumes;
        internal VolumeMean(int length) => _length = Math.Max(1, length);
        internal Number Next(Number price, Number volume, bool final)
        {
            var products = _products; var volumes = _volumes;
            if (_history.Count == _length) { var old = _history.Peek(); products -= old.Product; volumes -= old.Volume; }
            var product = price * volume; products += product; volumes += volume;
            var result = _history.Count < _length - 1 || volumes.Sign == 0 ? default : products.Divide(volumes);
            if (final)
            {
                if (_history.Count == _length) _history.Dequeue();
                _history.Enqueue((product, volume)); _products = products; _volumes = volumes;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _products = _volumes = default; }
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
