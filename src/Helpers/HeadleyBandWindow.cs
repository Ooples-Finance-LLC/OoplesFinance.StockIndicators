using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HeadleyBandWindow : IDisposable
{
    private readonly RocBankAverage? _upper, _middle, _lower;
    private readonly IMovingAverageSmoother? _upperFallback, _middleFallback, _lowerFallback;
    private readonly double _factor;
    internal HeadleyBandWindow(MovingAvgType kind, int length, double factor, int capacityHint = int.MaxValue)
    {
        _factor = factor;
        if (StrengthWindow.Supports(kind)) { _upper = new(kind, length, capacityHint); _middle = new(kind, length, capacityHint); _lower = new(kind, length, capacityHint); }
        else { _upperFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _middleFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); _lowerFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal static (RocBankValue Upper, RocBankValue Lower) Boundaries(double high, double low, double factor)
    {
        var denominator = new ExactMeanAccumulator(); denominator.Add(high); denominator.Add(low);
        var numerator = new ExactMeanAccumulator(); numerator.AddProduct(high, factor, 4000); numerator.AddProduct(low, factor, -4000);
        var shift = default(RocBankValue);
        if (!denominator.IsExactlyZero)
            for (var exponent = 0; ; exponent += 1024)
            {
                var divisor = denominator; divisor.ScaleByPowerOfTwo(exponent); var ratio = numerator.Ratio(divisor);
                if (!double.IsInfinity(ratio)) { shift = new(ratio, exponent); break; }
            }
        var upper = new ExactMeanAccumulator(); upper.AddProduct(high, shift.Mantissa); upper.ScaleByPowerOfTwo(shift.UpperShift); upper.Add(high);
        var lower = new ExactMeanAccumulator(); lower.AddProduct(low, shift.Mantissa, -1); lower.ScaleByPowerOfTwo(shift.UpperShift); lower.Add(low);
        return (RocBankValue.Round(upper), RocBankValue.Round(lower));
    }
    internal (double Upper, double Middle, double Lower) Next(double high, double low, double close, bool commit)
    {
        var boundaries = Boundaries(high, low, _factor);
        var upper = _upper is not null ? _upper.Next(boundaries.Upper, commit).Publish() : _upperFallback!.Next(boundaries.Upper.Publish(), commit);
        var middle = _middle is not null ? _middle.Next(new RocBankValue(close), commit).Publish() : _middleFallback!.Next(close, commit);
        var lower = _lower is not null ? _lower.Next(boundaries.Lower, commit).Publish() : _lowerFallback!.Next(boundaries.Lower.Publish(), commit);
        return (upper, middle, lower);
    }
    internal void Reset() { _upper?.Reset(); _middle?.Reset(); _lower?.Reset(); _upperFallback?.Reset(); _middleFallback?.Reset(); _lowerFallback?.Reset(); }
    public void Dispose() { _upper?.Dispose(); _middle?.Dispose(); _lower?.Dispose(); _upperFallback?.Dispose(); _middleFallback?.Dispose(); _lowerFallback?.Dispose(); }
}
