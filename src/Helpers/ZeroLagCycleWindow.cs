using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ZeroLagCycleWindow : IDisposable
{
    private readonly Stage[] _regressions;
    private readonly Stage _first, _second;
    private BigInteger _previous;
    internal ZeroLagCycleWindow(int length)
    {
        length = Math.Max(1, length); _regressions = Enumerable.Range(0, 6).Select(_ => new Stage(length)).ToArray();
        var smooth = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d))); _first = new(smooth); _second = new(smooth);
    }
    internal (double Line, double Filter, Signal Signal) Next(double price, bool commit)
    {
        var value = ExactVarianceWindow.Units(price);
        for (var leg = 0; leg < 6; leg++)
        {
            var fit = _regressions[leg].Next(value, commit, true);
            value = RocBankValue.RoundUnits((leg % 2 == 0 ? value : 2 * value) - fit, BigInteger.One);
        }
        var first = _first.Next(value, commit, false); var second = _second.Next(first, commit, false);
        var filter = -2 * second; var difference = value - filter;
        var signal = difference.Sign > 0 && difference > _previous ? Signal.StrongBuy : difference.Sign < 0 && difference < _previous ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) _previous = difference;
        return (ExactMeanAccumulator.UnitRatio(value, BigInteger.One), ExactMeanAccumulator.UnitRatio(filter, BigInteger.One), signal);
    }
    internal void Reset() { foreach (var stage in _regressions) stage.Reset(); _first.Reset(); _second.Reset(); _previous = default; }
    public void Dispose() => Reset();
    private sealed class Stage
    {
        private readonly int _length;
        private readonly Queue<BigInteger> _history = new();
        private BigInteger _sum, _weighted;
        internal Stage(int length) => _length = length;
        internal BigInteger Next(BigInteger value, bool commit, bool regression)
        {
            var full = _history.Count == _length; var expired = full ? _history.Peek() : BigInteger.Zero;
            var count = full ? _length : _history.Count + 1; var n = new BigInteger(count);
            var sum = _sum + value - expired;
            var weighted = full ? _weighted - _sum + expired + (n - 1) * value : _weighted + (n - 1) * value;
            var spread = n * n - 1;
            var result = regression && count > 1 ? RocBankValue.RoundUnits(sum * spread + 3 * (2 * weighted - (n - 1) * sum) * (n - 1), n * spread) : RocBankValue.RoundUnits(sum, n);
            if (commit) { if (full) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _weighted = weighted; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; }
    }
}
