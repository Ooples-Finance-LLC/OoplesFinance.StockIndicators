using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SmoothedVolatilityWindow : IDisposable
{
    private readonly KeltnerWindow? _range;
    private readonly RocBankAverage? _middle;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly double _deviation, _adjustment;
    internal static int AtrPeriod(int length1)
    {
        var period = 2L * Math.Max(1, length1) - 1;
        if (period > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(length1), "The derived ATR period must fit in Int32.");
        return (int)period;
    }
    internal SmoothedVolatilityWindow(MovingAvgType kind, int length1, int length2, double deviation, double adjustment, bool external = false, int capacityHint = int.MaxValue)
    {
        var period = AtrPeriod(length1); _deviation = deviation; _adjustment = adjustment;
        if (!external)
        {
            _range = new(kind, length1, period, kind, capacityHint);
            if (StrengthWindow.Supports(kind)) _middle = new(kind, length2, capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length2));
        }
    }
    private static double Boundary(double close, RocBankValue basis, RocBankValue offset, double adjustment, int sign)
    {
        if (close == 0) return basis.Publish();
        // Exact affine quotient: avoid overflow in products that cancel or divide back into range.
        var average = ExactVarianceWindow.Units(basis.Mantissa) << basis.UpperShift;
        var width = ExactVarianceWindow.Units(offset.Mantissa) << offset.UpperShift;
        var factor = ExactVarianceWindow.Units(adjustment);
        var denominator = ExactVarianceWindow.Units(close) << 1074;
        var numerator = average * denominator + sign * average * width * factor;
        return ExactMeanAccumulator.UnitRatio(denominator.Sign < 0 ? -numerator : numerator, BigInteger.Abs(denominator));
    }
    internal (double Upper, double Middle, double Lower) Next(double high, double low, double close, bool commit, RocBankValue? suppliedAtr = null, RocBankValue? suppliedBasis = null, RocBankValue? suppliedMiddle = null)
    {
        var stages = suppliedAtr.HasValue ? (suppliedBasis!.Value, suppliedAtr.Value) : _range!.Next(high, low, close, commit);
        var middle = suppliedMiddle ?? (_middle is not null ? _middle.Next(new RocBankValue(close), commit) : new RocBankValue(_fallback!.Next(close, commit)));
        var offset = stages.Item2.Multiply(_deviation);
        return (Boundary(close, stages.Item1, offset, 1, 1), middle.Publish(), Boundary(close, stages.Item1, offset, _adjustment, -1));
    }
    internal void Reset() { _range?.Reset(); _middle?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _range?.Dispose(); _middle?.Dispose(); _fallback?.Dispose(); }
}
