using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ResidualVolatilityWindow : IDisposable
{
    private readonly Average _mean, _variance, _signal;
    private Number _previousResidual;
    internal ResidualVolatilityWindow(MovingAvgType kind, int length)
    { _mean = new(kind, length); _variance = new(kind, length); _signal = new(kind, length); }
    // Keep the binary64 root component, extending only its upper exponent. The
    // 106-bit estimate locates the neighbors; exact squared midpoints decide rounding.
    internal static Number Root(Number square)
    {
        if (square.Sign <= 0) return default;
        var estimate = square.OverRoot(square).Publish();
        if (double.IsInfinity(estimate)) return Root(square.Divide(4)).Times(2);
        var bits = BitConverter.DoubleToInt64Bits(estimate);
        while (true)
        {
            var value = Number.Of(BitConverter.Int64BitsToDouble(bits));
            if (bits > 0)
            {
                var lower = (value + Number.Of(BitConverter.Int64BitsToDouble(bits - 1))).Divide(2);
                var side = (square - lower * lower).Sign;
                if (side < 0 || side == 0 && (bits & 1) != 0) { bits--; continue; }
            }
            var next = bits == 0x7fefffffffffffff ? Number.Integer(BigInteger.One << 1024) : Number.Of(BitConverter.Int64BitsToDouble(bits + 1));
            var upper = (value + next).Divide(2); var above = (square - upper * upper).Sign;
            if (above > 0 || above == 0 && (bits & 1) != 0)
            { if (bits == 0x7fefffffffffffff) return next; bits++; continue; }
            return value;
        }
    }
    private (double Deviation, double Variance, double SignalLine, Signal Trade) Finish(Number residual, Number variance, Number deviation, Number signal, bool final)
    {
        var change = residual - _previousResidual;
        var trade = (deviation - signal).Sign < 0 ? Signal.None
            : residual.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : residual.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousResidual = residual;
        return (deviation.Publish(), variance.Publish(), signal.Publish(), trade);
    }
    internal (double Deviation, double Variance, double SignalLine, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price);
        var residual = value - _mean.Next(value, final); var variance = _variance.Next(residual * residual, final);
        var deviation = Root(variance); return Finish(residual, variance, deviation, _signal.Next(deviation, final), final);
    }
    internal static (double[] Deviation, double[] Variance, double[] SignalLine, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks, bool includeSignal = true)
    {
        length = Math.Max(1, length); var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var source = prices.Select(Number.Of).ToArray(); var caller = data.CaptureInputSeries();
        Number[] Mean(Number[] values)
        {
            if (callbacks && ComponentAverage.HasOverrides)
            {
                var published = values.Select(v => v.Publish()).ToArray(); foreach (var value in published) StreamingInputValidation.Finite(value, nameof(values));
                var custom = ComponentAverage.Take(published, length);
                if (custom is not null)
                { var result = custom.ToArray(); foreach (var value in result) StreamingInputValidation.Finite(value, nameof(custom)); return result.Select(Number.Of).ToArray(); }
            }
            using var average = new Average(kind, length); return values.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            var mean = Mean(source); var residual = source.Select((v, i) => v - mean[i]).ToArray();
            var variance = Mean(residual.Select(v => v * v).ToArray()); var deviation = variance.Select(Root).ToArray();
            var signal = includeSignal ? Mean(deviation) : new Number[source.Length]; var trades = new Signal[source.Length];
            using var window = new ResidualVolatilityWindow(kind, length);
            for (var i = 0; i < trades.Length; i++) trades[i] = window.Finish(residual[i], variance[i], deviation[i], signal[i], true).Trade;
            return (deviation.Select(v => v.Publish()).ToArray(), variance.Select(v => v.Publish()).ToArray(), signal.Select(v => v.Publish()).ToArray(), trades);
        }
        finally { data.RestoreInputSeries(caller); }
    }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous; private long _count;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Number result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? _length + 1L : _length); }
            if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    internal void Reset() { _mean.Reset(); _variance.Reset(); _signal.Reset(); _previousResidual = default; }
    public void Dispose() { Reset(); _mean.Dispose(); _variance.Dispose(); _signal.Dispose(); }
}
