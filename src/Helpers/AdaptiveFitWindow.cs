using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveFitWindow : IDisposable
{
    private readonly int _length;
    private readonly double _smooth;
    private readonly Queue<BigInteger> _ranges = new();
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
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - previous), BigInteger.Abs(l - previous))); var maximum = range; var skip = _ranges.Count == _length;
        foreach (var retained in _ranges) { if (skip) { skip = false; continue; } maximum = BigInteger.Max(maximum, retained); }
        var ratio = maximum.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(range << 1074, maximum);
        var gain = maximum.IsZero ? .01 : Math.Max(.01, Math.Min(.99, Math.Pow(ratio, _smooth))); var moments = _moments; var value = moments.Next(price, gain); var difference = input - moments.ExactOutput;
        var signal = difference.Sign > 0 && difference > _difference ? Signal.StrongBuy : difference.Sign < 0 && difference < _difference ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_ranges.Count == _length) _ranges.Dequeue(); _ranges.Enqueue(range); _moments = moments; _previous = input; _difference = difference; _seeded = true; } return (value, signal);
    }
    internal void Reset() { _ranges.Clear(); _moments = default; _previous = _difference = default; _seeded = false; }
    public void Dispose() => Reset();
}
