using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ElderThermometerWindow : IDisposable
{
    private readonly StrengthAverage? _wide;
    private readonly IMovingAverageSmoother? _fallback;
    private double _high, _low;
    internal ElderThermometerWindow(MovingAvgType kind, int length)
    {
        if (StrengthWindow.Supports(kind)) _wide = new StrengthAverage(kind, length);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal static StrengthValue Expansion(double high, double low, double previousHigh, double previousLow)
    {
        var rising = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(previousHigh);
        var falling = ExactVarianceWindow.Units(previousLow) - ExactVarianceWindow.Units(low);
        var magnitude = BigInteger.Max(BigInteger.Zero, BigInteger.Max(rising, falling));
        var value = ExactMeanAccumulator.UnitRatio(magnitude, BigInteger.One);
        return double.IsInfinity(value) ? new StrengthValue(ExactMeanAccumulator.UnitRatio(magnitude, new BigInteger(2)), true) : new StrengthValue(value);
    }
    internal static double Publish(StrengthValue value) => value.Mantissa * (value.Doubled ? 2 : 1);
    internal (double Value, double Signal) Next(double high, double low, bool commit)
    {
        var expansion = Expansion(high, low, _high, _low);
        var value = Publish(expansion);
        var signal = _wide is not null ? Publish(_wide.Next(expansion, commit)) : _fallback!.Next(value, commit);
        if (commit) { _high = high; _low = low; }
        return (value, signal);
    }
    internal void Reset() { _high = _low = 0; _wide?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _wide?.Dispose(); _fallback?.Dispose(); }
}
