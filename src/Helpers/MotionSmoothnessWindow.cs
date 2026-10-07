using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MotionSmoothnessWindow
{
    private readonly Moments _prices, _changes;
    private double _previous; private bool _seeded;
    internal MotionSmoothnessWindow(int length) { _prices = new(length); _changes = new(length); }
    internal double Next(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator(); if (_seeded) { sum.Add(price); sum.Add(_previous, -1); }
        var change = RocBankValue.Round(sum); var prices = _prices.Next(new RocBankValue(price), commit); var changes = _changes.Next(change, commit);
        var numerator = new ExactMeanAccumulator(); changes.AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); prices.AddTo(ref denominator);
        if (commit) { _previous = price; _seeded = true; }
        return numerator.Ratio(denominator);
    }
    internal void Reset() { _prices.Reset(); _changes.Reset(); _previous = 0; _seeded = false; }
    private sealed class Moments
    {
        private readonly int _length; private readonly Queue<BigInteger> _history = new(); private BigInteger _sum, _squares;
        internal Moments(int length) { _length = Math.Max(1, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            var units = ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift; var full = _history.Count == _length; var expired = full ? _history.Peek() : BigInteger.Zero;
            var sum = _sum + units - expired; var squares = _squares + units * units - expired * expired; var n = new BigInteger(_length); var result = default(RocBankValue);
            if (_history.Count + 1L >= _length)
            {
                for (var shift = 0; ; shift += 32)
                { var root = ExactPopulationDeviation.RootRatio(n * squares - sum * sum, n * n << (2 * shift)); if (!double.IsInfinity(root)) { result = new(root, shift); break; } }
            }
            if (commit) { _sum = sum; _squares = squares; if (full) _history.Dequeue(); _history.Enqueue(units); }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _squares = default; }
    }
}
