using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class WaveTrendWindow : IDisposable
{
    private readonly Average _basis, _deviation, _line, _signal;
    private Number _previousLine, _previousMargin;
    internal WaveTrendWindow(MovingAvgType kind, int channel, int average, int signal)
    { _basis = new(kind, channel); _deviation = new(kind, channel); _line = new(kind, average); _signal = new(kind, signal); }
    internal static Number Price(double open, double high, double low, double close)
        => (Number.Of(open) + Number.Of(high) + Number.Of(low) + Number.Of(close)).Divide(4);
    internal static Number Price(double high, double low, double close)
        => (Number.Of(high) + Number.Of(low) + Number.Of(close)).Divide(3);
    private static Number Channel(Number residual, Number deviation)
        => deviation.Sign == 0 ? default : residual.Divide(Number.Of(.015) * deviation);
    private static Signal Trade(Number line, Number signal, Number previousLine, Number previousMargin)
    {
        var margin = line - signal; var change = margin - previousMargin;
        if (margin.Sign > 0 && change.Sign > 0) return Signal.StrongBuy;
        if (margin.Sign < 0 && change.Sign < 0) return Signal.StrongSell;
        if (margin.Sign > 0 || (previousLine - Number.Of(-53)).Sign < 0 && (line - Number.Of(-53)).Sign > 0) return Signal.Buy;
        if (margin.Sign < 0 || (previousLine - Number.Of(53)).Sign > 0 && (line - Number.Of(53)).Sign < 0) return Signal.Sell;
        return Signal.None;
    }
    internal (double Line, double SignalLine, Signal Trade) Next(Number price, bool final)
    {
        var residual = price - _basis.Next(price, final); var absolute = residual.Sign < 0 ? residual.Times(-1) : residual;
        var ci = Channel(residual, _deviation.Next(absolute, final));
        var line = _line.Next(ci, final); var signal = _signal.Next(line, final);
        var trade = Trade(line, signal, _previousLine, _previousMargin);
        if (final) { _previousLine = line; _previousMargin = line - signal; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, MovingAvgType kind,
        int channel, int average, int signal, bool fast = false, bool includeSignal = true)
    {
        channel = Math.Max(1, channel); average = Math.Max(1, average); signal = Math.Max(1, signal);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var selected = data.ChainedValues.Count > 0;
        var prices = Enumerable.Range(0, data.Count).Select(i => selected ? Number.Of(data.ChainedValues[i])
            : Price(data.OpenPrices[i], data.HighPrices[i], data.LowPrices[i], data.ClosePrices[i])).ToArray();
        Number[] ExactMean(Number[] values, int period)
        { using var mean = new Average(kind, period); return values.Select(v => mean.Next(v, true)).ToArray(); }
        Number[] Mean(Number[] values, int period)
        {
            var replacement = fast ? ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), period) : null;
            if (replacement is not null) return Enumerable.Range(0, prices.Length).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            if (StrengthWindow.Supports(kind)) return ExactMean(values, period);
            var input = values.Select(v => v.Publish()).ToArray();
            if (!fast) return CalculationsHelper.GetMovingAverageList(data, kind, period, input.ToList()).Select(Number.Of).ToArray();
            var output = new double[input.Length]; MovingAverageRegistry.GetRequired(kind).Compute(input, output, period); return output.Select(Number.Of).ToArray();
        }
        var basis = Mean(prices, channel);
        // The original EMA residual recurrence bypassed the first override, while still consuming its slot.
        if (fast && kind == MovingAvgType.ExponentialMovingAverage) basis = ExactMean(prices, channel);
        var residuals = prices.Select((v, i) => v - basis[i]).ToArray();
        var deviation = Mean(residuals.Select(v => v.Sign < 0 ? v.Times(-1) : v).ToArray(), channel);
        var ci = residuals.Select((v, i) => Channel(v, deviation[i])).ToArray();
        var line = Mean(ci, average); var smoothed = includeSignal ? Mean(line, signal) : new Number[prices.Length];
        var trades = new Signal[prices.Length];
        if (includeSignal) for (var i = 0; i < trades.Length; i++)
            trades[i] = Trade(line[i], smoothed[i], i == 0 ? default : line[i - 1], i == 0 ? default : line[i - 1] - smoothed[i - 1]);
        return (line.Select(v => v.Publish()).ToArray(), smoothed.Select(v => v.Publish()).ToArray(), trades);
    }
    internal void Reset() { _basis.Reset(); _deviation.Reset(); _line.Reset(); _signal.Reset(); _previousLine = _previousMargin = default; }
    public void Dispose() { _basis.Dispose(); _deviation.Dispose(); _line.Dispose(); _signal.Dispose(); }
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
