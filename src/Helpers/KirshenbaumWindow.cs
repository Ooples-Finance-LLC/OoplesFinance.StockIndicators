using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KirshenbaumWindow : IDisposable
{
    private readonly ExactLinearFitWindow _fit;
    private readonly PooledRingBuffer<BigInteger> _squares;
    private readonly RocBankAverage? _mean;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly int _length;
    private readonly double _factor;
    private BigInteger _sum;
    private long _count;
    internal KirshenbaumWindow(MovingAvgType kind, int meanLength, int errorLength, double factor, bool external = false, int capacityHint = int.MaxValue)
    {
        HighLowBandsWindow.ValidateShift(factor); _factor = factor; _length = Math.Max(1, errorLength); _fit = new(_length); _squares = new(Math.Min(_length, Math.Max(1, capacityHint)));
        if (!external) { if (StrengthWindow.Supports(kind)) _mean = new(kind, Math.Max(1, meanLength), capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, meanLength)); }
    }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit, double? externalMean = null)
    {
        var middle = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean is not null ? _mean.Next(new RocBankValue(close), commit) : new RocBankValue(_fallback!.Next(close, commit));
        var endpoint = _fit.Next(close, commit).RoundedLastUnits; var error = endpoint - ExactVarianceWindow.Units(close); var square = error * error; var sum = _sum + square; if (_count >= _length) sum -= _squares[0]; var denominator = new BigInteger(Math.Min(_length, _count + 1)); var root = ExactPopulationDeviation.RootRatio(sum, denominator);
        var deviation = double.IsInfinity(root) ? new RocBankValue(ExactPopulationDeviation.RootRatio(sum, denominator << 2048), 1024) : new RocBankValue(root);
        if (commit) { _squares.TryAdd(square, out _); _sum = sum; _count++; }
        return KeltnerWindow.Bands(middle, deviation, _factor);
    }
    internal void Reset() { _fit.Reset(); _squares.Clear(); _mean?.Reset(); _fallback?.Reset(); _sum = default; _count = 0; }
    public void Dispose() { _fit.Dispose(); _squares.Dispose(); _mean?.Dispose(); _fallback?.Dispose(); }
}
