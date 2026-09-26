using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// The common price scale and division by eight cancel from the fourth-degree
// force ratio. Keep its integer numerator until normalization, even for tiny prices.
internal sealed class ExactTrendForceWindow : IDisposable
{
    private readonly StrengthAverage _first, _second;
    private readonly LinkedList<(long Index, BigInteger Force)> _maxima = new();
    private readonly int _lookback;
    private readonly bool _center;
    private double _anchor;
    private long _count;
    private BigInteger _previousFirst, _previousSecond;
    internal ExactTrendForceWindow(MovingAvgType kind, int length, int lookback)
    {
        var period = Math.Max(1, Math.Min(530, (int)Math.Ceiling(Math.Max(1, length) / 2d)));
        _first = new StrengthAverage(kind, period); _second = new StrengthAverage(kind, period);
        _lookback = Math.Max(1, lookback); _center = kind == MovingAvgType.ExponentialMovingAverage;
    }
    private static BigInteger Units(StrengthValue value) => ExactVarianceWindow.Units(value.Mantissa) * (value.Doubled ? 2 : 1);
    internal double Next(double price, bool commit)
    {
        var source = new ExactMeanAccumulator(); source.Add(price);
        var anchor = _count == 0 ? price : _anchor;
        if (_center) source.Add(anchor, -1);
        var first = _first.Next(StrengthValue.Round(source, 1), commit);
        var second = _second.Next(first, commit);
        var f = Units(first); var s = Units(second);
        var change = f - _previousFirst + s - _previousSecond;
        var force = BigInteger.Abs(f - s) * change * change * change;
        var magnitude = BigInteger.Abs(force);
        var oldest = _maxima.First;
        while (oldest is not null && oldest.Value.Index <= _count - _lookback) oldest = oldest.Next;
        var maximum = oldest is null ? magnitude : BigInteger.Max(magnitude, oldest.Value.Force);
        var result = maximum.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(force << 1074, maximum);
        if (commit)
        {
            while (_maxima.First is not null && _maxima.First.Value.Index <= _count - _lookback) _maxima.RemoveFirst();
            while (_maxima.Last is not null && _maxima.Last.Value.Force <= magnitude) _maxima.RemoveLast();
            _maxima.AddLast((_count, magnitude)); _count++;
            _previousFirst = f; _previousSecond = s; _anchor = anchor;
        }
        return result;
    }
    internal void Reset()
    { _first.Reset(); _second.Reset(); _maxima.Clear(); _count = 0; _anchor = 0; _previousFirst = _previousSecond = 0; }
    public void Dispose() { _first.Dispose(); _second.Dispose(); }
}
