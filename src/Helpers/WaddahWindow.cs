using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class WaddahWindow
{
    private readonly Ema _fast, _slow, _lagFast, _lagSlow;
    private readonly Number _sensitivity, _threshold;
    private readonly int _length;
    private readonly Queue<double> _history = new();
    private BigInteger _sum, _squares;
    private Number _p1, _p2, _p3, _previousUp, _previousWidthSquare;
    internal WaddahWindow(int fast, int slow, double sensitivity)
    {
        StreamingInputValidation.Finite(sensitivity, nameof(sensitivity));
        _sensitivity = Number.Of(sensitivity); _threshold = Number.Of(fast); _length = Math.Max(1, fast);
        _fast = new(fast); _slow = new(slow); _lagFast = new(fast); _lagSlow = new(slow);
    }
    internal (double T1, double T2, double E1, double Up, double Down, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var p = Number.Of(price); var delta = p - _p1; var lagDelta = _p2 - _p3;
        // Exact linear filters of differences equal the difference of separately lagged MACDs.
        var t1 = (_fast.Next(delta, final) - _slow.Next(delta, final)) * _sensitivity;
        var t2 = (_lagFast.Next(lagDelta, final) - _lagSlow.Next(lagDelta, final)) * _sensitivity;
        var units = ExactVarianceWindow.Units(price); var sum = _sum + units; var squares = _squares + units * units;
        if (_history.Count == _length) { var old = ExactVarianceWindow.Units(_history.Peek()); sum -= old; squares -= old * old; }
        var moment = _history.Count < _length - 1 ? BigInteger.Zero : _length * squares - sum * sum;
        var denominator = (BigInteger)_length * _length;
        var width = ExactPopulationDeviation.RootRatio(16 * moment, denominator);
        var unit = Number.Of(double.Epsilon);
        var widthSquare = (Number.Integer(16 * moment) * unit * unit).Divide(Number.Integer(denominator));
        var up = t1.Sign > 0 ? t1 : default; var down = t1.Sign < 0 ? t1.Times(-1) : default;
        var upSquare = up * up;
        var buy = (up - _previousUp).Sign > 0 && (upSquare - widthSquare).Sign > 0 &&
            (widthSquare - _previousWidthSquare).Sign > 0 && (up - _threshold).Sign > 0 &&
            (_threshold.Sign < 0 || (widthSquare - _threshold * _threshold).Sign > 0);
        var trade = buy ? Signal.Buy : (upSquare - widthSquare).Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_history.Count == _length) _history.Dequeue();
            _history.Enqueue(price); _sum = sum; _squares = squares;
            _p3 = _p2; _p2 = _p1; _p1 = p; _previousUp = up; _previousWidthSquare = widthSquare;
        }
        return (t1.Publish(), t2.Publish(), width, up.Publish(), down.Publish(), trade);
    }
    internal static (Dictionary<string, List<double>> Outputs, Signal[] Trades) Calculate(StockData data, int fast, int slow, double sensitivity)
    {
        var (prices, _, _, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { prices, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var window = new WaddahWindow(fast, slow, sensitivity);
        var outputs = new Dictionary<string, List<double>> { ["T1"] = new(), ["T2"] = new(), ["E1"] = new(), ["TrendUp"] = new(), ["TrendDn"] = new() };
        var trades = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        {
            var point = window.Next(prices[i], true);
            outputs["T1"].Add(point.T1); outputs["T2"].Add(point.T2); outputs["E1"].Add(point.E1);
            outputs["TrendUp"].Add(point.Up); outputs["TrendDn"].Add(point.Down); trades[i] = point.Trade;
        }
        return (outputs, trades);
    }
    internal void Reset()
    {
        _fast.Reset(); _slow.Reset(); _lagFast.Reset(); _lagSlow.Reset(); _history.Clear(); _sum = _squares = default;
        _p1 = _p2 = _p3 = _previousUp = _previousWidthSquare = default;
    }
    private sealed class Ema
    {
        private readonly int _length; private long _count; private Number _sum, _previous;
        internal Ema(int length) => _length = Math.Max(1, length);
        internal Number Next(Number value, bool final)
        {
            var sum = _sum; Number result;
            if (_count < _length) { sum += value; result = sum.Divide(_count + 1); }
            else result = (_previous.Times(_length - 1L) + value.Times(2)).Divide(_length + 1L);
            if (final) { _sum = sum; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _count = 0; _sum = _previous = default; }
    }
}
