using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrigonometricWindow : IDisposable
{
    private readonly FitWindow _price, _angle;
    private BigInteger _previous;
    internal TrigonometricWindow(int length) { _price = new(length); _angle = new(length); }
    internal double Next(double price, bool commit)
    {
        var fit = _price.Next(price, commit).RoundedLastUnits;
        var direction = fit.CompareTo(_previous); var angle = direction > 0 ? Math.PI : direction < 0 ? -3 * Math.PI : 0;
        var result = Math.Atan(_angle.Next(angle, commit).Last);
        if (commit) _previous = fit;
        return result;
    }
    internal void Reset() { _previous = default; _price.Reset(); _angle.Reset(); }
    public void Dispose() { }
    // Grow only with observed bars, including when the configured period is Int32.MaxValue.
    private sealed class FitWindow
    {
        private readonly int _length; private readonly Queue<double> _history = new();
        private BigInteger _sum, _weighted;
        internal FitWindow(int length) => _length = Math.Max(1, length);
        internal ExactLinearFitWindow.Fit Next(double value, bool commit)
        {
            var current = ExactVarianceWindow.Units(value); var full = _history.Count == _length;
            var expired = full ? ExactVarianceWindow.Units(_history.Peek()) : BigInteger.Zero;
            var count = full ? _history.Count : _history.Count + 1; var n = new BigInteger(count);
            var sum = _sum + current - expired; var weighted = full ? _weighted - _sum + expired + (count - 1) * current : _weighted + (count - 1) * current;
            var fit = new ExactLinearFitWindow.Fit(sum, 2 * weighted - (n - 1) * sum, n, n * n - 1, BigInteger.Zero);
            if (commit) { if (full) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _weighted = weighted; }
            return fit;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; }
    }
}
