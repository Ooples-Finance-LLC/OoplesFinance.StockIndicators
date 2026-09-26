using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class LinearExtrapolationWindow
{
    private readonly int _length;
    private readonly List<double> _prices = new();
    private int _next;
    private long _count;
    internal LinearExtrapolationWindow(int length) => _length = Math.Max(1, length);
    private double Lag(long lag) => lag > _prices.Count ? 0 : _prices[(int)((_next - lag + _prices.Count) % _prices.Count)];
    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var first = Lag(_length); var second = Lag(2L * _length);
        var total = new ExactMeanAccumulator(); long divisor = 1;
        if (_count == _length) total.Add(first);
        else if (_count > _length && _count < 2L * _length)
        {
            total.Add(first, new BigInteger(_count)); divisor = _count - _length;
        }
        else if (_count >= 2L * _length) { total.Add(first, 2); total.Add(second, -1); }
        var result = total.Mean(divisor);
        if (commit)
        {
            if (_prices.Count < 2L * _length) { _prices.Add(price); _next = _prices.Count; }
            else { if (_next == _prices.Count) _next = 0; _prices[_next++] = price; }
            _count++;
        }
        return result;
    }
    internal void Reset() { _prices.Clear(); _next = 0; _count = 0; }
}
