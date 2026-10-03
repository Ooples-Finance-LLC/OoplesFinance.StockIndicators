using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MarketDirectionWindow
{
    private readonly int _fast, _slow;
    private readonly long _difference;
    private readonly Window _fastSum, _slowSum;
    private BigInteger _previousPrice, _previousCrossing, _previousNumerator, _previousDenominator = BigInteger.One;
    internal MarketDirectionWindow(int fast, int slow)
    {
        _fast = Math.Max(1, fast); _slow = Math.Max(1, slow); _difference = (long)_slow - _fast;
        _fastSum = new(_fast - 1); _slowSum = new(_slow - 1);
    }
    private sealed class Window
    {
        private readonly int _length; private readonly Queue<double> _history = new(); private BigInteger _sum;
        internal Window(int length) => _length = length;
        internal BigInteger Next(double price, BigInteger units, bool final)
        {
            if (_length == 0) return BigInteger.Zero;
            var sum = _sum + units;
            if (_history.Count == _length) sum -= ExactVarianceWindow.Units(_history.Peek());
            if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(price); _sum = sum; }
            return sum;
        }
        internal void Reset() { _history.Clear(); _sum = default; }
    }
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var units = ExactVarianceWindow.Units(price);
        var fast = _fastSum.Next(price, units, final); var slow = _slowSum.Next(price, units, final);
        // Every crossing has the same divisor. Subtract its complete numerator
        // first; the common binary price grid cancels in the final quotient.
        var crossing = _fast * slow - _slow * fast;
        var numerator = 200 * (_previousCrossing - crossing); var denominator = _difference * (units + _previousPrice);
        if (denominator.IsZero) { numerator = BigInteger.Zero; denominator = BigInteger.One; }
        else if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var change = numerator * _previousDenominator - _previousNumerator * denominator;
        var trade = numerator.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : numerator.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : numerator.Sign > 0 ? Signal.Buy : numerator.Sign < 0 ? Signal.Sell : Signal.None;
        var value = ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator);
        if (final) { _previousPrice = units; _previousCrossing = crossing; _previousNumerator = numerator; _previousDenominator = denominator; }
        return (value, trade);
    }
    internal static (double[] Values, Signal[] Trades) Calculate(IReadOnlyList<double> prices, int fast, int slow)
    {
        for (var i = 0; i < prices.Count; i++) StreamingInputValidation.Finite(prices[i], nameof(prices));
        var window = new MarketDirectionWindow(fast, slow); var values = new double[prices.Count]; var trades = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], true); values[i] = point.Value; trades[i] = point.Trade; }
        return (values, trades);
    }
    internal void Reset() { _fastSum.Reset(); _slowSum.Reset(); _previousPrice = _previousCrossing = _previousNumerator = default; _previousDenominator = BigInteger.One; }
}
