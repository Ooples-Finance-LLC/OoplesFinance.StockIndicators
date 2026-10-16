using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveFitWindow : IDisposable
{
    private readonly int _length;
    private readonly double _smooth;
    private (long Index, BigInteger Range)[] _maxima = Array.Empty<(long, BigInteger)>();
    private int _head, _count;
    private long _index;
    private AdaptiveLeastSquaresMoments _moments;
    private BigInteger _previous, _difference;
    private bool _seeded;
    internal AdaptiveFitWindow(int length, double smooth = 1.5)
    {
        if (double.IsNaN(smooth) || double.IsInfinity(smooth)) throw new ArgumentOutOfRangeException(nameof(smooth));
        _length = Math.Max(1, length); _smooth = smooth;
    }
    internal (double Value, Signal Signal) Next(double price, double high, double low, bool commit)
    {
        var input = ExactVarianceWindow.Units(price); var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var previous = _seeded ? _previous : input;
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - previous), BigInteger.Abs(l - previous))); var first = 0;
        while (first < _count && _maxima[Slot(first)].Index <= _index - _length) first++;
        var maximum = first == _count ? range : BigInteger.Max(range, _maxima[Slot(first)].Range);
        var ratio = maximum.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(range << 1074, maximum);
        var gain = maximum.IsZero ? .01 : Math.Max(.01, Math.Min(.99, Math.Pow(ratio, _smooth))); var moments = _moments; var value = moments.Next(price, gain); var difference = input - moments.ExactOutput;
        var signal = difference.Sign > 0 && difference > _difference ? Signal.StrongBuy : difference.Sign < 0 && difference < _difference ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { CommitMaximum(range); _moments = moments; _previous = input; _difference = difference; _seeded = true; } return (value, signal);
    }
    private int Slot(int offset) => (int)(((long)_head + offset) % _maxima.Length);

    // Descending candidates retain only values that can become a future maximum.
    // Storage grows with observations and is reused; previews never mutate it.
    private void CommitMaximum(BigInteger range)
    {
        while (_count > 0 && _maxima[_head].Index <= _index - _length)
        {
            _maxima[_head] = default;
            _head = (_head + 1) % _maxima.Length;
            _count--;
        }
        while (_count > 0 && _maxima[Slot(_count - 1)].Range <= range)
        {
            _maxima[Slot(_count - 1)] = default;
            _count--;
        }
        if (_count == _maxima.Length)
        {
            var capacity = (int)Math.Min(_length, Math.Max(4L, 2L * _maxima.Length));
            var grown = new (long Index, BigInteger Range)[capacity];
            for (var i = 0; i < _count; i++) grown[i] = _maxima[Slot(i)];
            _maxima = grown;
            _head = 0;
        }
        _maxima[Slot(_count++)] = (_index++, range);
    }

    internal void Reset() { Array.Clear(_maxima, 0, _maxima.Length); _head = _count = 0; _index = 0; _moments = default; _previous = _difference = default; _seeded = false; }
    public void Dispose() => Reset();
}
