using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PseudoPolynomialWindow : IDisposable
{
    private readonly int _length;
    private readonly BigInteger _morph;
    private readonly PooledRingBuffer<BigInteger> _history;
    private readonly RocBankAverage? _mean;
    private readonly IMovingAverageSmoother? _fallback;
    private long _count, _bandCount;
    private BigInteger _errorSum;
    internal PseudoPolynomialWindow(MovingAvgType kind, int length, double morph, bool external = false, int capacityHint = int.MaxValue)
    {
        HighLowBandsWindow.ValidateShift(morph); _length = Math.Max(1, length); _morph = ExactVarianceWindow.Units(morph); _history = new((int)Math.Min(2L * _length, Math.Max(1, capacityHint)));
        if (!external) { if (StrengthWindow.Supports(kind)) _mean = new(kind, _length, capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
    }
    private static BigInteger Round(BigInteger units) => RocBankValue.RoundUnits(units, BigInteger.One);
    private BigInteger Blend(BigInteger old, BigInteger price) => RocBankValue.RoundUnits((price << 1074) + _morph * (old - price), BigInteger.One << 1074);
    private static RocBankValue Value(BigInteger units) { var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); return RocBankValue.Round(sum); }
    internal RocBankValue Generate(double close, bool commit)
    {
        var price = ExactVarianceWindow.Units(close); var first = _count >= _length ? _history[_history.Count - _length] : price; var second = _count >= 2L * _length ? _history[_history.Count - 2 * _length] : price;
        var firstIndex = Math.Max(0, _count - _length); var secondIndex = Math.Max(0, _count - 2L * _length); var distance = firstIndex - secondIndex; var ky = Blend(first, price); var ky2 = Blend(second, price);
        var k = distance == 0 ? BigInteger.Zero : RocBankValue.RoundUnits(ky * distance + (_count - firstIndex) * (ky - ky2), new BigInteger(distance));
        if (commit) { _history.TryAdd(k, out _); _count++; }
        return Value(k);
    }
    internal (double Upper, double Middle, double Lower) Finish(double close, RocBankValue mean, bool commit)
    {
        var center = ExactVarianceWindow.Units(mean.Mantissa) << mean.UpperShift; var error = Round(BigInteger.Abs(ExactVarianceWindow.Units(close) - center)); var sum = _errorSum + error; var width = _bandCount == 0 ? BigInteger.Zero : RocBankValue.RoundUnits(sum, new BigInteger(_bandCount));
        var upper = Round(center + width); var lower = Round(center - width);
        if (commit) { _errorSum = sum; _bandCount++; }
        return (ExactMeanAccumulator.UnitRatio(upper, BigInteger.One), ExactMeanAccumulator.UnitRatio(upper + lower, new BigInteger(2)), ExactMeanAccumulator.UnitRatio(lower, BigInteger.One));
    }
    internal (double Raw, double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var k = Generate(close, commit); var mean = _mean is not null ? _mean.Next(k, commit) : new RocBankValue(_fallback!.Next(k.Publish(), commit)); var bands = Finish(close, mean, commit); return (k.Publish(), bands.Upper, bands.Middle, bands.Lower);
    }
    internal void Reset() { _history.Clear(); _mean?.Reset(); _fallback?.Reset(); _count = _bandCount = 0; _errorSum = default; }
    public void Dispose() { _history.Dispose(); _mean?.Dispose(); _fallback?.Dispose(); }
}
