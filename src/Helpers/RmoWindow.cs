using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RmoWindow : IDisposable
{
    private readonly int _range;
    private readonly Mean[] _cascade;
    private readonly Mean _second, _third, _rmo;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _count;
    private Number _previous;
    internal RmoWindow(int first, int range, int swing, int output)
    {
        _range = Math.Max(2, range); _cascade = Enumerable.Range(0, 10).Select(_ => new Mean(first, false)).ToArray();
        _second = new(swing, true); _third = new(swing, true); _rmo = new(output, true);
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double price, bool high, bool final)
    {
        var expiry = _count - _range + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var value = first is null ? price : high ? Math.Max(price, first.Value.Value) : Math.Min(price, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (high ? last.Value.Value <= price : last.Value.Value >= price)) deque.RemoveLast();
            deque.AddLast((_count, price));
        }
        return value;
    }
    private Number Raw(double price, Number sum, bool final)
    {
        var high = Extreme(_highs, price, true, final); var low = Extreme(_lows, price, false, final);
        var width = Number.Of(high) - Number.Of(low);
        var raw = width.Sign == 0 ? default : (Number.Of(price).Times(10) - sum).Times(10).Divide(width);
        if (final) _count++;
        return raw;
    }
    internal (double Rmo, double Swing1, double Swing2, double Swing3, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price); var sum = default(Number);
        foreach (var stage in _cascade) { value = stage.Next(value, final); sum += value; }
        var first = Raw(price, sum, final); var second = _second.Next(first, final); var third = _third.Next(second, final); var rmo = _rmo.Next(first, final);
        var change = rmo - _previous;
        var trade = rmo.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : rmo.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : rmo.Sign > 0 ? Signal.Buy : rmo.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previous = rmo;
        return (rmo.Publish(), first.Publish(), second.Publish(), third.Publish(), trade);
    }
    internal static (double[] Rmo, double[] Swing1, double[] Swing2, double[] Swing3, Signal[] Trades) Calculate(
        StockData data, int first, int range, int swing, int output, bool callbacks,
        IndicatorCompute.RahulMohindarSeries series = IndicatorCompute.RahulMohindarSeries.Rmo)
    {
        first = Math.Max(1, first); swing = Math.Max(1, swing); output = Math.Max(1, output);
        var (prices, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        using var window = new RmoWindow(first, range, swing, output);
        var rmo = new double[prices.Count]; var one = new double[prices.Count]; var two = new double[prices.Count]; var three = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        {
            for (var i = 0; i < prices.Count; i++) (rmo[i], one[i], two[i], three[i], trades[i]) = window.Next(prices[i], true);
        }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                double[] Average(double[] values, int period, MovingAvgType kind)
                {
                    var result = ComponentAverage.Take(values, period)?.ToArray()
                        ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller); return result;
                }
                var values = prices.ToArray(); var sums = new Number[prices.Count];
                for (var stage = 0; stage < 10; stage++)
                { values = Average(values, first, MovingAvgType.SimpleMovingAverage); for (var i = 0; i < values.Length; i++) sums[i] += Number.Of(values[i]); }
                for (var i = 0; i < prices.Count; i++) one[i] = window.Raw(prices[i], sums[i], true).Publish();
                if (series == IndicatorCompute.RahulMohindarSeries.Rmo) rmo = Average(one, output, MovingAvgType.ExponentialMovingAverage);
                else if (series != IndicatorCompute.RahulMohindarSeries.SwingTrade1)
                {
                    two = Average(one, swing, MovingAvgType.ExponentialMovingAverage);
                    if (series == IndicatorCompute.RahulMohindarSeries.SwingTrade3) three = Average(two, swing, MovingAvgType.ExponentialMovingAverage);
                }
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (rmo, one, two, three, trades);
    }
    private sealed class Mean
    {
        private readonly int _period; private readonly bool _ema;
        private readonly Queue<Number> _history = new();
        private Number _sum, _previous; private long _count;
        internal Mean(int period, bool ema) { _period = Math.Max(1, period); _ema = ema; }
        internal Number Next(Number value, bool final)
        {
            var sum = _sum; Number result;
            if (!_ema)
            {
                if (_history.Count == _period) sum -= _history.Peek(); sum += value;
                result = _count + 1 < _period ? default : sum.Divide(_period);
            }
            else if (_count < _period) { sum += value; result = sum.Divide(_count + 1); }
            else result = (_previous.Times(_period - 1L) + value.Times(2)).Divide(_period + 1L);
            if (final)
            { if (!_ema) { if (_history.Count == _period) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _previous = default; _count = 0; }
    }
    internal void Reset() { foreach (var stage in _cascade) stage.Reset(); _second.Reset(); _third.Reset(); _rmo.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _previous = default; }
    public void Dispose() => Reset();
}
