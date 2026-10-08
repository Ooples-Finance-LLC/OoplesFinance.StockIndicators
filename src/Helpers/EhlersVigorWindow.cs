using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EhlersVigorWindow : IDisposable
{
    private readonly RocBankAverage? _first, _signal;
    private readonly IMovingAverageSmoother? _firstFallback, _signalFallback;
    internal EhlersVigorWindow(MovingAvgType kind, int length, int signalLength, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength); if (StrengthWindow.Supports(kind)) { _first = new(kind, length, capacityHint); _signal = new(kind, signalLength, capacityHint); } else { _firstFallback = MovingAverageSmootherFactory.Create(kind, length); _signalFallback = MovingAverageSmootherFactory.Create(kind, signalLength); }
    }
    internal static RocBankValue TrueValue(double close, double open, double high, double low)
    { var numerator = ExactVarianceWindow.Units(close) - ExactVarianceWindow.Units(open);
        var denominator = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
        if (denominator.IsZero) return default;
        var units = RocBankValue.RoundUnits((numerator * denominator.Sign) << 1074, BigInteger.Abs(denominator));
        for (var shift = 0; ; shift += 1024) { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); } }
    internal (double Value, double Signal) Next(double close, double open, double high, double low, bool commit)
    {
        var input = TrueValue(close, open, high, low); var first = _first is not null ? _first.Next(input, commit) : new RocBankValue(_firstFallback!.Next(input.Publish(), commit)); var signal = _signal is not null ? _signal.Next(first, commit) : new RocBankValue(_signalFallback!.Next(first.Publish(), commit)); return (first.Publish(), signal.Publish());
    }
    internal void Reset() { _first?.Reset(); _signal?.Reset(); _firstFallback?.Reset(); _signalFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _signal?.Dispose(); _firstFallback?.Dispose(); _signalFallback?.Dispose(); }
}
