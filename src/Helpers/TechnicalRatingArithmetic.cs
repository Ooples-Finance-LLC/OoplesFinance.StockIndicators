using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

// Composite votes must be decided before projecting an overflowing component to double.
// Keep binary64 stage precision, with an extended upper exponent.
internal readonly struct TechnicalRatingValue
{
    internal static readonly BigInteger Unit = BigInteger.One << 1074;
    internal readonly BigInteger Units;
    internal TechnicalRatingValue(BigInteger units) => Units = units;
    internal static TechnicalRatingValue Ratio(BigInteger numerator, BigInteger denominator) =>
        new(RocBankValue.RoundUnits(denominator.Sign < 0 ? -numerator : numerator, BigInteger.Abs(denominator)));
    public static implicit operator TechnicalRatingValue(double value) => new(ExactVarianceWindow.Units(value));
    public static TechnicalRatingValue operator +(TechnicalRatingValue a, TechnicalRatingValue b) => Ratio(a.Units + b.Units, BigInteger.One);
    public static TechnicalRatingValue operator -(TechnicalRatingValue a, TechnicalRatingValue b) => Ratio(a.Units - b.Units, BigInteger.One);
    public static TechnicalRatingValue operator *(TechnicalRatingValue a, TechnicalRatingValue b) => Ratio(a.Units * b.Units, Unit);
    public static TechnicalRatingValue operator /(TechnicalRatingValue a, TechnicalRatingValue b) => b.Units.IsZero ? default : Ratio(a.Units * Unit, b.Units);
    internal double Publish() => ExactMeanAccumulator.UnitRatio(Units, BigInteger.One);
}

internal sealed class TechnicalRatingAverage : IDisposable
{
    private readonly RocBankAverage _average;
    internal TechnicalRatingAverage(MovingAvgType kind, int length) => _average = new(kind, length, int.MaxValue);
    internal TechnicalRatingValue Next(TechnicalRatingValue value, bool final)
    {
        var exact = new ExactMeanAccumulator(); exact.Add(double.Epsilon, value.Units);
        var result = _average.Next(RocBankValue.Round(exact), final);
        return new TechnicalRatingValue(ExactVarianceWindow.Units(result.Mantissa) << result.UpperShift);
    }
    internal void Reset() => _average.Reset();
    public void Dispose() => _average.Dispose();
}

internal sealed class TechnicalRatingVolume : IDisposable
{
    private readonly int _length;
    private readonly Queue<(BigInteger PriceVolume, BigInteger Volume)> _history = new();
    private BigInteger _priceVolume, _volume;
    internal TechnicalRatingVolume(int length) => _length = Math.Max(1, length);
    internal TechnicalRatingValue Next(double price, double volume, bool final)
    {
        var v = ExactVarianceWindow.Units(volume); var pv = ExactVarianceWindow.Units(price) * v;
        var sum = _priceVolume + pv; var mass = _volume + v;
        if (_history.Count == _length) { var old = _history.Peek(); sum -= old.PriceVolume; mass -= old.Volume; }
        var result = _history.Count < _length - 1 || mass.IsZero ? default : TechnicalRatingValue.Ratio(sum, mass);
        if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((pv, v)); _priceVolume = sum; _volume = mass; }
        return result;
    }
    internal void Reset() { _history.Clear(); _priceVolume = _volume = default; }
    public void Dispose() => _history.Clear();
}

internal sealed class TechnicalRatingHull : IDisposable
{
    private readonly TechnicalRatingAverage _full, _half, _root;
    internal TechnicalRatingHull(int length)
    {
        _full = new(MovingAvgType.WeightedMovingAverage, length);
        _half = new(MovingAvgType.WeightedMovingAverage, HullWindow.Half(length));
        _root = new(MovingAvgType.WeightedMovingAverage, HullWindow.Root(length));
    }
    internal TechnicalRatingValue Next(double value, bool final)
    {
        var full = _full.Next(value, final); var half = _half.Next(value, final);
        return _root.Next(TechnicalRatingValue.Ratio(2 * half.Units - full.Units, BigInteger.One), final);
    }
    internal void Reset() { _full.Reset(); _half.Reset(); _root.Reset(); }
    public void Dispose() { _full.Dispose(); _half.Dispose(); _root.Dispose(); }
}

internal sealed class TechnicalRatingMomentum : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _history = new();
    internal TechnicalRatingMomentum(int length) => _length = Math.Max(1, length);
    internal TechnicalRatingValue Next(double value, bool final)
    {
        var previous = _history.Count == _length ? _history.Peek() : 0;
        var result = previous == 0 ? default : TechnicalRatingValue.Ratio(ExactVarianceWindow.Units(value) * 100 * TechnicalRatingValue.Unit, ExactVarianceWindow.Units(previous));
        if (final) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
        return result;
    }
    internal void Reset() => _history.Clear();
    public void Dispose() => _history.Clear();
}

internal sealed class TechnicalRatingWilliams : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    internal TechnicalRatingWilliams(int length) { _high = new(Math.Max(1, length)); _low = new(Math.Max(1, length)); }
    internal TechnicalRatingValue Next(double high, double low, double value, bool final)
    {
        var upper = final ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lower = final ? _low.Add(low, out _) : _low.Preview(low, out _);
        var span = ExactVarianceWindow.Units(upper) - ExactVarianceWindow.Units(lower);
        return span.IsZero ? (TechnicalRatingValue)(-100d) : TechnicalRatingValue.Ratio(
            100 * (ExactVarianceWindow.Units(value) - ExactVarianceWindow.Units(upper)) * TechnicalRatingValue.Unit, span);
    }
    internal void Reset() { _high.Reset(); _low.Reset(); }
    public void Dispose() { _high.Dispose(); _low.Dispose(); }
}
