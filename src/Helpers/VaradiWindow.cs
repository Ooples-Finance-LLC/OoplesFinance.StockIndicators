using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VaradiWindow : IDisposable
{
    private readonly int _length;
    private readonly ExactAverage _average;
    private readonly VaradiOrderStatistic _order;
    private long _previousCount, _previousSlope;

    internal VaradiWindow(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length);
        _average = new(kind, _length);
        _order = new(_length);
        _order.Add(default);
    }

    private static Number Ratio(double price, double high, double low)
    {
        var sum = Number.Of(high) + Number.Of(low);
        return sum.Sign == 0 ? default : Number.Of(price).Times(2).Divide(sum);
    }

    private (double Value, Signal Trade) Rank(Number average, bool final)
    {
        var count = _order.CountLessThanOrEqual(average);
        var slope = count - _previousCount;
        var acceleration = slope - _previousSlope;
        var trade = slope > 0 && acceleration > 0 ? Signal.StrongBuy
            : slope < 0 && acceleration < 0 ? Signal.StrongSell
            : slope > 0 ? Signal.Buy : slope < 0 ? Signal.Sell : Signal.None;
        var value = Number.Integer(count).Times(100).Divide(_length).Publish();
        if (final)
        {
            _order.Add(average);
            _previousCount = count;
            _previousSlope = slope;
        }
        return (value, trade);
    }

    internal (double Value, Signal Trade) Next(double price, double high, double low, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        StreamingInputValidation.Finite(high, nameof(high));
        StreamingInputValidation.Finite(low, nameof(low));
        return Rank(_average.Next(Ratio(price, high, low), final), final);
    }

    internal static (double[] Values, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool fast)
    {
        length = Math.Max(1, length);
        var (prices, highs, lows, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var ratios = Enumerable.Range(0, prices.Count).Select(i => Ratio(prices[i], highs[i], lows[i])).ToArray();
        IReadOnlyList<double>? replacement = null;
        var standard = StrengthWindow.Supports(kind);
        // Batch historically bypasses callbacks for its four standard means;
        // the explicit fast method requests one component average for every kind.
        if (!standard)
            replacement = CalculationsHelper.GetMovingAverageList(data, kind, length,
                ratios.Select(value => value.Publish()).ToList());
        else if (fast && ComponentAverage.HasOverrides)
            replacement = ComponentAverage.Take(ratios.Select(value => value.Publish()).ToArray(), length);
        using var window = new VaradiWindow(kind, length);
        var values = new double[prices.Count];
        var trades = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        {
            var mean = replacement is null ? window._average.Next(ratios[i], true)
                : i < replacement.Count ? Number.Of(replacement[i]) : default;
            var point = window.Rank(mean, true);
            values[i] = point.Value;
            trades[i] = point.Trade;
        }
        return (values, trades);
    }

    internal void Reset()
    {
        _average.Reset();
        _order.Dispose();
        _order.Add(default);
        _previousCount = _previousSlope = 0;
    }
    public void Dispose() { _average.Dispose(); _order.Dispose(); }

    // Ranking is discontinuous: rounding a mean can turn distinct observations
    // into equal keys. Retain exact fractions through all four standard means.
    private sealed class ExactAverage : IDisposable
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous;
        private long _count;
        internal ExactAverage(MovingAvgType kind, int length)
        {
            _kind = kind; _length = length;
            if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length);
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
