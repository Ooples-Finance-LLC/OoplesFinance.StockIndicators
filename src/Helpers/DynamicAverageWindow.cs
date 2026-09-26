using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Select the nearest even period directly from exact population variances.
// Squared midpoint comparisons avoid overflow, underflow and double rounding.
internal sealed class DynamicAverageWindow
{
    private readonly int _fast, _slow;
    private readonly Queue<double> _prices = new();
    private readonly Moments _short, _long;
    internal DynamicAverageWindow(int fast, int slow)
    {
        _fast = Math.Max(1, fast); _slow = Math.Max(1, slow);
        _short = new(_fast); _long = new(_slow);
    }
    internal double Next(double price, bool commit)
    {
        var a = _short.Next(price, commit); var b = _long.Next(price, commit);
        var period = Math.Min(_fast, _slow);
        if (_slow > _fast && a.Sign > 0)
        {
            var numerator = b * _fast * _fast * 4;
            var denominator = a * _slow * _slow;
            var low = _fast; var high = _slow;
            while (low < high)
            {
                var mid = low + (high - low) / 2;
                var boundary = new BigInteger(2L * (mid - _fast) + 1);
                var comparison = numerator.CompareTo(denominator * boundary * boundary);
                if (comparison > 0 || comparison == 0 && (mid & 1) != 0) low = mid + 1;
                else high = mid;
            }
            period = low;
        }
        var sum = new ExactMeanAccumulator(); sum.Add(price);
        var skip = Math.Max(0, _prices.Count - (period - 1)); var index = 0;
        foreach (var previous in _prices) if (index++ >= skip) sum.Add(previous);
        var result = sum.Mean(period);
        if (commit)
        {
            if (_prices.Count == _slow) _prices.Dequeue();
            _prices.Enqueue(price);
        }
        return result;
    }
    internal void Reset() { _prices.Clear(); _short.Reset(); _long.Reset(); }
    private sealed class Moments
    {
        private readonly int _length;
        private readonly Queue<BigInteger> _values = new();
        private BigInteger _sum, _squares;
        internal Moments(int length) => _length = length;
        internal BigInteger Next(double value, bool commit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            var bits = BitConverter.DoubleToInt64Bits(value); var exponent = (int)((bits >> 52) & 2047);
            var integer = new BigInteger(bits & ((1L << 52) - 1));
            if (exponent != 0) integer = (integer + (BigInteger.One << 52)) << (exponent - 1);
            if (bits < 0) integer = -integer;
            var sum = _sum + integer; var squares = _squares + integer * integer;
            if (_values.Count == _length) { var old = _values.Peek(); sum -= old; squares -= old * old; }
            var full = _values.Count >= _length - 1;
            if (commit)
            {
                if (_values.Count == _length) _values.Dequeue();
                _values.Enqueue(integer); _sum = sum; _squares = squares;
            }
            return full ? _length * squares - sum * sum : BigInteger.Zero;
        }
        internal void Reset() { _values.Clear(); _sum = _squares = BigInteger.Zero; }
    }
}
