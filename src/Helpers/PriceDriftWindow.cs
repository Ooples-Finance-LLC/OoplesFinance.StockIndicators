using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PriceDriftWindow : IDisposable
{
    private readonly int _length;
    private readonly bool _curve;
    private readonly KeltnerWindow _atr;
    private double _upper, _lower, _olderUpper, _olderLower;
    private RocBankValue _sizeA, _sizeB;
    private long _count, _lastRise = -1, _lastFall = -1;
    internal PriceDriftWindow(MovingAvgType kind, int length, bool curve, int capacityHint = int.MaxValue)
    { _length = Math.Max(1, length); _curve = curve; _atr = new(MovingAvgType.SimpleMovingAverage, 1, _length, kind, capacityHint); }
    private RocBankValue InitialSize(RocBankValue atr)
    { var sum = new ExactMeanAccumulator(); atr.AddTo(ref sum); return RocBankValue.Round(sum, count: _length); }
    private static double Drift(double boundary, RocBankValue size, long age, long denominator, int sign)
    { var sum = new ExactMeanAccumulator(); sum.Add(boundary, new BigInteger(denominator)); size.AddTo(ref sum, age * sign); return sum.Mean(denominator); }
    internal (double Upper, double Middle, double Lower) Next(double high, double low, double close, bool commit, RocBankValue? externalAtr = null)
    {
        var atr = externalAtr ?? _atr.Next(high, low, close, commit).Atr;
        var previousUpper = _count > 0 ? _upper : close; var previousLower = _count > 0 ? _lower : close;
        var olderUpper = _count >= 2 ? _olderUpper : 0; var olderLower = _count >= 2 ? _olderLower : 0;
        var rise = previousUpper > olderUpper; var fall = previousLower < olderLower;
        var initial = _count == 0 ? InitialSize(atr) : default;
        var sizeA = rise || (_curve && fall) ? atr : _count == 0 ? initial : _sizeA;
        var sizeB = fall || (_curve && rise) ? atr : _count == 0 ? initial : _sizeB;
        var lastRise = rise ? _count : _lastRise; var lastFall = fall ? _count : _lastFall;
        var denominator = _curve ? (long)_length * _length : _length;
        var upper = Math.Max(close, Drift(Math.Max(close, previousUpper), sizeA, _curve ? _count - lastRise + 1 : 1, denominator, -1));
        var lower = Math.Min(close, Drift(Math.Min(close, previousLower), sizeB, _curve ? _count - lastFall + 1 : 1, denominator, 1));
        var midpoint = new ExactMeanAccumulator(); midpoint.Add(upper); midpoint.Add(lower);
        if (commit) { _olderUpper = previousUpper; _olderLower = previousLower; _upper = upper; _lower = lower; _sizeA = sizeA; _sizeB = sizeB; _lastRise = lastRise; _lastFall = lastFall; _count++; }
        return (upper, midpoint.Mean(2), lower);
    }
    internal void Reset() { _atr.Reset(); _upper = _lower = _olderUpper = _olderLower = 0; _sizeA = _sizeB = default; _count = 0; _lastRise = _lastFall = -1; }
    public void Dispose() => _atr.Dispose();
}
