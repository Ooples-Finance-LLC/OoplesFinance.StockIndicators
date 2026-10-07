using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RelativeVigorWindow : IDisposable
{
    private readonly PooledRingBuffer<BigInteger> _bodies = new(3), _ranges = new(3), _indices = new(3);
    private readonly RocBankAverage? _numerator, _denominator;
    private readonly IMovingAverageSmoother? _numeratorFallback, _denominatorFallback;
    internal RelativeVigorWindow(MovingAvgType kind, int length, bool external = false, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length);
        if (!external) { if (StrengthWindow.Supports(kind)) { _numerator = new(kind, length, capacityHint); _denominator = new(kind, length, capacityHint); } else { _numeratorFallback = MovingAverageSmootherFactory.Create(kind, length); _denominatorFallback = MovingAverageSmootherFactory.Create(kind, length); } }
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 1024) { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private static RocBankValue Filter(PooledRingBuffer<BigInteger> history, BigInteger current, bool commit)
    {
        var total = current;
        for (var lag = 1; lag <= 3; lag++) if (history.Count >= lag) total += history[history.Count - lag] * (lag == 3 ? 1 : 2);
        var result = Value(RocBankValue.RoundUnits(total, 6)); if (commit) history.TryAdd(current, out _); return result;
    }
    internal (RocBankValue Numerator, RocBankValue Denominator) Generate(double close, double open, double high, double low, bool commit)
    { return (Filter(_bodies, ExactVarianceWindow.Units(close) - ExactVarianceWindow.Units(open), commit), Filter(_ranges, ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low), commit)); }
    internal (double Value, double Signal) Finish(RocBankValue numerator, RocBankValue denominator, bool commit)
    {
        var d = Units(denominator); var value = d.IsZero ? default : Value(RocBankValue.RoundUnits((Units(numerator) * d.Sign) << 1074, BigInteger.Abs(d))); var signal = Filter(_indices, Units(value), commit); return (value.Publish(), signal.Publish());
    }
    internal (double Value, double Signal) Next(double close, double open, double high, double low, bool commit)
    {
        var legs = Generate(close, open, high, low, commit); var numerator = _numerator is not null ? _numerator.Next(legs.Numerator, commit) : new RocBankValue(_numeratorFallback!.Next(legs.Numerator.Publish(), commit)); var denominator = _denominator is not null ? _denominator.Next(legs.Denominator, commit) : new RocBankValue(_denominatorFallback!.Next(legs.Denominator.Publish(), commit)); return Finish(numerator, denominator, commit);
    }
    internal void Reset() { _bodies.Clear(); _ranges.Clear(); _indices.Clear(); _numerator?.Reset(); _denominator?.Reset(); _numeratorFallback?.Reset(); _denominatorFallback?.Reset(); }
    public void Dispose() { _bodies.Dispose(); _ranges.Dispose(); _indices.Dispose(); _numerator?.Dispose(); _denominator?.Dispose(); _numeratorFallback?.Dispose(); _denominatorFallback?.Dispose(); }
}
