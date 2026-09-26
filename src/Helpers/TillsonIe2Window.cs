using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TillsonIe2Window : IDisposable
{
    private readonly ExactLinearFitWindow _fit;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private BigInteger _previous;
    internal TillsonIe2Window(MovingAvgType kind, int length, bool externalAverage = false)
    {
        length = Math.Max(1, length); _fit = new(length);
        if (externalAverage) return;
        if (StrengthWindow.Supports(kind)) _average = new(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal double Next(double price, bool commit, double? average = null)
    {
        var mean = average ?? (_average is null ? _fallback!.Next(price, commit) : _average.Next(new RocBankValue(price), commit).Publish());
        var fit = _fit.Next(price, commit).RoundedLastUnits;
        var value = ExactMeanAccumulator.UnitRatio(2 * fit - _previous + ExactVarianceWindow.Units(mean), new BigInteger(2));
        if (commit) _previous = fit;
        return value;
    }
    internal void Reset() { _fit.Reset(); _average?.Reset(); _fallback?.Reset(); _previous = default; }
    public void Dispose() { _fit.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
