using BigInteger = OoplesFinance.StockIndicators.Helpers.ExactInteger;
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ExactVarianceWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private BigInteger _sum;
    private BigInteger _squares;

    internal ExactVarianceWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));

    internal static BigInteger Units(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047) throw new ArgumentOutOfRangeException(nameof(value));
        // Signed 53-bit significand in a long, then one shift: ExactInteger strips its trailing zeros once and the
        // shift only moves the exponent, so no full-width value is built, added or negated.
        var significand = bits & ((1L << 52) - 1);
        if (exponent != 0) significand |= 1L << 52;
        var integer = new BigInteger(bits < 0 ? -significand : significand);
        return exponent > 1 ? integer << (exponent - 1) : integer;
    }

    internal double Next(double value, bool commit)
    {
        var current = Units(value);
        var sum = _sum + current;
        var squares = _squares + current * current;
        if (_window.Count == _window.Capacity)
        {
            var expired = Units(_window[0]);
            sum -= expired;
            squares -= expired * expired;
        }
        var length = _window.Capacity;
        var result = _window.Count < length - 1 ? 0
            : ExactMeanAccumulator.SquaredUnitMean(length * squares - sum * sum, (long)length * length);
        if (commit)
        {
            _window.TryAdd(value, out _);
            _sum = sum;
            _squares = squares;
        }
        return result;
    }

    internal void Reset() { _window.Clear(); _sum = default; _squares = default; }
    public void Dispose() => _window.Dispose();
}
