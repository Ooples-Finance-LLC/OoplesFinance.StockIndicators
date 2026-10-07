using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CandlePowerWindow : IDisposable
{
    private readonly bool _bull;
    private readonly RocBankAverage? _signal;
    private readonly IMovingAverageSmoother? _fallback;
    private double _previous;
    internal CandlePowerWindow(bool bull, MovingAvgType kind, int length, bool external = false, int capacityHint = int.MaxValue)
    {
        _bull = bull; if (!external) { if (StrengthWindow.Supports(kind)) _signal = new(kind, Math.Max(1, length), capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length)); }
    }
    internal static RocBankValue Power(double close, double open, double high, double low, double previous, bool bull)
    {
        var c = ExactVarianceWindow.Units(close); var o = ExactVarianceWindow.Units(open); var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var p = ExactVarianceWindow.Units(previous); BigInteger power;
        if (bull)
        {
            if (c < o) power = BigInteger.Max(h - o, c - l);
            else if (p < o) power = BigInteger.Max(h - p, c - l);
            else if (c > o) power = BigInteger.Max(o - p, h - l);
            else if (p > o) power = h - l;
            else if (h - c > c - l) power = h - o;
            else if (h - c < c - l) power = BigInteger.Max(o - c, h - l);
            else power = h - l;
        }
        else
        {
            if (c < o) power = h - l;
            else if (p > o) power = BigInteger.Max(c - o, h - l);
            else if (c > o) power = BigInteger.Max(o - l, h - c);
            else if (h - c > c - l) power = h - l;
            else if (h - c < c - l) power = o - l;
            else if (p < o) power = BigInteger.Max(o - l, h - c);
            else power = h - l;
        }
        var value = ExactMeanAccumulator.UnitRatio(power, BigInteger.One);
        return double.IsInfinity(value) ? new RocBankValue(ExactMeanAccumulator.UnitRatio(power, BigInteger.One << 1024), 1024) : new RocBankValue(value);
    }
    internal (double Value, double Signal) Next(double close, double open, double high, double low, bool commit)
    {
        var power = Power(close, open, high, low, _previous, _bull); var signal = _signal is not null ? _signal.Next(power, commit).Publish() : _fallback?.Next(power.Publish(), commit) ?? 0;
        if (commit) _previous = close; return (power.Publish(), signal);
    }
    internal void Reset() { _previous = 0; _signal?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _signal?.Dispose(); _fallback?.Dispose(); }
}
