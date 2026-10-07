using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VortexBandWindow : IDisposable
{
    private readonly Mean? _basis, _width;
    private readonly IMovingAverageSmoother? _basisFallback, _widthFallback;
    private static bool Supports(MovingAvgType kind) => StrengthWindow.Supports(kind) || kind == MovingAvgType.McNichollMovingAverage;
    internal VortexBandWindow(MovingAvgType kind, int length)
    {
        if (Supports(kind)) { _basis = new(kind, length); _width = new(kind, length); }
        else { _basisFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _widthFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    private static (double Upper, double Middle, double Lower, Signal Trade) Finish(Number basis, Number width)
    {
        var halfWidth = width.Sign > 0 ? width.Times(2) : default;
        return ((basis + halfWidth).Publish(), basis.Publish(), (basis - halfWidth).Publish(), width.Sign > 0 ? Signal.Buy : Signal.None);
    }
    private static (double Upper, double Middle, double Lower, Signal Trade) Fallback(double basis, double width)
    {
        var halfWidth = 2 * Math.Max(0, width); var upper = basis + halfWidth; var lower = basis - halfWidth;
        return (upper, basis, lower, SignalHelper.GetConditionSignal(upper > lower && upper > basis, lower > upper && lower > basis));
    }
    internal (double Upper, double Middle, double Lower, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        if (_basisFallback is not null)
        {
            var basis = _basisFallback.Next(price, final);
            return Fallback(basis, _widthFallback!.Next(Math.Abs(price - basis), final));
        }
        var value = Number.Of(price); var mean = _basis!.Next(value, final); var residual = value - mean;
        if (residual.Sign < 0) residual = residual.Times(-1);
        return Finish(mean, _width!.Next(residual, final));
    }
    internal static (double[] Upper, double[] Middle, double[] Lower, Signal[] Trades) Calculate(StockData data,
        MovingAvgType kind, int length, bool fast = false)
    {
        var (prices, _, _, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var upper = new double[prices.Count]; var middle = new double[prices.Count]; var lower = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!Supports(kind))
        {
            double[] Smooth(double[] values)
            {
                if (!fast) return CalculationsHelper.GetMovingAverageList(data, kind, length, values.ToList()).ToArray();
                var output = new double[values.Length]; MovingAverageRegistry.GetRequired(kind).Compute(values, output, length); return output;
            }
            var basis = Smooth(prices.ToArray()); var widths = Smooth(prices.Select((p, i) => Math.Abs(p - basis[i])).ToArray());
            for (var i = 0; i < prices.Count; i++) { var point = Fallback(basis[i], widths[i]); upper[i] = point.Upper; middle[i] = point.Middle; lower[i] = point.Lower; trades[i] = point.Trade; }
        }
        else
        {
            using var window = new VortexBandWindow(kind, length);
            for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], true); upper[i] = point.Upper; middle[i] = point.Middle; lower[i] = point.Lower; trades[i] = point.Trade; }
        }
        return (upper, middle, lower, trades);
    }
    internal void Reset() { _basis?.Reset(); _width?.Reset(); _basisFallback?.Reset(); _widthFallback?.Reset(); }
    public void Dispose() { _basis?.Dispose(); _width?.Dispose(); _basisFallback?.Dispose(); _widthFallback?.Dispose(); }

    private sealed class Mean : IDisposable
    {
        private readonly Average _first;
        private readonly Average? _second;
        private readonly int _length;
        internal Mean(MovingAvgType kind, int length)
        {
            var mcNicholl = kind == MovingAvgType.McNichollMovingAverage;
            _length = Math.Max(mcNicholl ? 2 : 1, length);
            _first = new(mcNicholl ? MovingAvgType.ExponentialMovingAverage : kind, _length);
            if (mcNicholl) _second = new(MovingAvgType.ExponentialMovingAverage, _length);
        }
        internal Number Next(Number value, bool final)
        {
            var first = _first.Next(value, final);
            if (_second is null) return first;
            var second = _second.Next(first, final);
            return (first.Times(2L * _length) - second.Times(_length + 1L)).Divide(_length - 1L);
        }
        internal void Reset() { _first.Reset(); _second?.Reset(); }
        public void Dispose() { _first.Dispose(); _second?.Dispose(); }
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
