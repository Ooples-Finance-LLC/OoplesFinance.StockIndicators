using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HirashimaWindow
{
    private readonly Average _ema, _width;
    private readonly Regression _first, _second;
    private BigInteger _previousSecond, _previousMargin;
    internal HirashimaWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        _ema = new(MovingAvgType.ExponentialMovingAverage, length); _width = new(kind, length);
        _first = new(length); _second = new(length);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger value) => RocBankValue.RoundUnits(value, BigInteger.One);
    private static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    internal (double[] Bands, Signal Trade) Next(double price, bool final)
    {
        var ema = _ema.Next(U(price), final); var residual = Round(U(price) - ema);
        var width = _width.Next(BigInteger.Abs(residual), final);
        return Finish(price, ema, residual, width, final);
    }
    private (double[] Bands, Signal Trade) Finish(double price, BigInteger ema, BigInteger residual, BigInteger width, bool final)
    {
        var first = _first.Next(residual, final);
        var remaining = Round(U(price) - ema - first);
        var second = _second.Next(remaining, final);
        var basis = Round(ema + first + second - _previousSecond);
        var margin = U(price) - basis;
        var trade = margin.Sign > 0 && margin > _previousMargin ? Signal.StrongBuy : margin.Sign < 0 && margin < _previousMargin ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousSecond = second; _previousMargin = margin; }
        // Publish each complete band independently; inner-band overflow must
        // not prevent the opposite band or center from remaining finite.
        return (new[] { Publish(basis + width), Publish(basis + 2 * width), Publish(basis), Publish(basis - width), Publish(basis - 2 * width) }, trade);
    }
    internal static (double[][] Bands, Signal[] Trades) Calculate(IReadOnlyList<double> prices, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var window = new HirashimaWindow(kind, length);
        var externalEma = callbacks ? ComponentAverage.Take(prices.ToArray(), length) : null;
        var emas = new BigInteger[prices.Count]; var residuals = new BigInteger[prices.Count];
        for (var i = 0; i < prices.Count; i++) { emas[i] = externalEma is null ? window._ema.Next(U(prices[i]), true) : U(externalEma[i]); residuals[i] = Round(U(prices[i]) - emas[i]); }
        var externalWidth = callbacks ? ComponentAverage.Take(residuals.Select(v => Publish(BigInteger.Abs(v))).ToArray(), length) : null;
        var bands = Enumerable.Range(0, 5).Select(_ => new double[prices.Count]).ToArray(); var trades = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        {
            var width = externalWidth is null ? window._width.Next(BigInteger.Abs(residuals[i]), true) : U(externalWidth[i]);
            var point = window.Finish(prices[i], emas[i], residuals[i], width, true);
            for (var k = 0; k < 5; k++) bands[k][i] = point.Bands[k]; trades[i] = point.Trade;
        }
        return (bands, trades);
    }
    internal static double Band(double basis, double width, int offset)
    {
        if (double.IsNaN(basis) || double.IsInfinity(basis) || double.IsNaN(width) || double.IsInfinity(width)) return basis + offset * width;
        return Publish(U(basis) + offset * U(width));
    }
    internal void Reset() { _ema.Reset(); _width.Reset(); _first.Reset(); _second.Reset(); _previousSecond = _previousMargin = default; }
    private sealed class Regression
    {
        private readonly int _length; private readonly Queue<BigInteger> _history = new(); private BigInteger _sum, _weighted;
        internal Regression(int length) => _length = length;
        internal BigInteger Next(BigInteger value, bool final)
        {
            var full = _history.Count == _length; var count = full ? _length : _history.Count + 1;
            var expired = full ? _history.Peek() : BigInteger.Zero;
            var sum = _sum + value - expired;
            var weighted = full ? _weighted - _sum + expired + (count - 1) * value : _weighted + (count - 1) * value;
            var n = new BigInteger(count); var spread = n * n - 1; var covariance = 2 * weighted - (n - 1) * sum;
            var result = count == 1 ? value : RocBankValue.RoundUnits(sum * spread + 3 * covariance * (n - 1), n * spread);
            if (final) { if (full) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _weighted = weighted; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; }
    }
    private sealed class Average
    {
        private readonly MovingAvgType _kind; private readonly int _length; private readonly Queue<BigInteger> _history = new();
        private BigInteger _sum, _weighted, _previous; private int _count;
        internal Average(MovingAvgType kind, int length) { _kind = kind; _length = length; }
        internal BigInteger Next(BigInteger value, bool final)
        {
            var sum = _sum; var weighted = _weighted; BigInteger result;
            var rolling = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (rolling)
            {
                weighted = weighted - sum + _length * value; if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.RoundUnits(weighted, new BigInteger((long)_length * (_length + 1L) / 2))
                    : _history.Count < _length - 1 ? BigInteger.Zero : RocBankValue.RoundUnits(sum, new BigInteger(_length));
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = RocBankValue.RoundUnits(sum, new BigInteger(_count + 1)); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = RocBankValue.RoundUnits((_length - 1L) * _previous + (ema ? 2 : 1) * value, new BigInteger(ema ? _length + 1L : _length)); }
            if (final) { _sum = sum; _weighted = weighted; _previous = result; if (_count < _length) _count++; if (rolling) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; }
    }
}
