using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KeltnerWindow : IDisposable
{
    private readonly RocBankAverage? _middle, _atr;
    private readonly IMovingAverageSmoother? _middleFallback, _atrFallback;
    private double _previous;
    private bool _hasPrevious;
    internal KeltnerWindow(MovingAvgType kind, int centerLength, int rangeLength, MovingAvgType rangeKind = MovingAvgType.WildersSmoothingMethod, int capacityHint = int.MaxValue)
    {
        if (StrengthWindow.Supports(kind)) _middle = new(kind, centerLength, capacityHint); else _middleFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, centerLength));
        if (StrengthWindow.Supports(rangeKind)) _atr = new(rangeKind, rangeLength, capacityHint); else _atrFallback = MovingAverageSmootherFactory.Create(rangeKind, Math.Max(1, rangeLength));
    }
    private static RocBankValue Difference(double a, double b) { var sum = new ExactMeanAccumulator(); sum.Add(a); sum.Add(b, -1); return RocBankValue.Round(sum); }
    internal RocBankValue NextMiddle(double price, bool commit) => _middle is not null ? _middle.Next(new RocBankValue(price), commit) : new RocBankValue(_middleFallback!.Next(price, commit));
    internal (RocBankValue Middle, RocBankValue Atr) Next(double high, double low, double close, bool commit)
    {
        var previous = _hasPrevious ? _previous : close;
        var span = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
        var highGap = BigInteger.Abs(ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(previous));
        var lowGap = BigInteger.Abs(ExactVarianceWindow.Units(low) - ExactVarianceWindow.Units(previous));
        var range = highGap > span && highGap >= lowGap ? high >= previous ? Difference(high, previous) : Difference(previous, high)
            : lowGap > span ? low >= previous ? Difference(low, previous) : Difference(previous, low) : Difference(high, low);
        var atr = _atr is not null ? _atr.Next(range, commit) : new RocBankValue(_atrFallback!.Next(range.Publish(), commit));
        var middle = NextMiddle(close, commit);
        if (commit) { _previous = close; _hasPrevious = true; }
        return (middle, atr);
    }
    private static ExactMeanAccumulator Distance(RocBankValue atr, double multiplier, int weight = 1)
    {
        var result = new ExactMeanAccumulator(); result.AddProduct(atr.Mantissa, multiplier, weight); result.ScaleByPowerOfTwo(atr.UpperShift); return result;
    }
    internal static (double Upper, double Middle, double Lower) Bands(RocBankValue middle, RocBankValue atr, double multiplier)
    {
        var upper = Distance(atr, multiplier); middle.AddTo(ref upper);
        var lower = Distance(atr, multiplier, -1); middle.AddTo(ref lower);
        return (upper.Mean(1), middle.Publish(), lower.Mean(1));
    }
    internal static double Width(RocBankValue middle, RocBankValue atr, double multiplier)
    {
        if (middle.Mantissa == 0) return 0;
        var numerator = Distance(atr, multiplier, 200); var denominator = new ExactMeanAccumulator(); middle.AddTo(ref denominator);
        return numerator.Ratio(denominator);
    }
    internal void Reset() { _middle?.Reset(); _atr?.Reset(); _middleFallback?.Reset(); _atrFallback?.Reset(); _previous = 0; _hasPrevious = false; }
    public void Dispose() { _middle?.Dispose(); _atr?.Dispose(); _middleFallback?.Dispose(); _atrFallback?.Dispose(); }
}
