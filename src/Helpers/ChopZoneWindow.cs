using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ChopZoneWindow : IDisposable
{
    private readonly int _length;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly LinkedList<(long Index, double Value)> _high = new(), _low = new();
    private long _count;
    private double _previous;
    internal ChopZoneWindow(MovingAvgType kind, int length, int smoothing, int capacity = int.MaxValue)
    { _length = Math.Max(1, length); if (StrengthWindow.Supports(kind)) _average = new(kind, Math.Max(1, smoothing), capacity); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, smoothing)); }
    private void Commit(LinkedList<(long Index, double Value)> values, double value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _length) values.RemoveFirst();
        while (values.Last is not null && (maximum ? values.Last.Value.Value <= value : values.Last.Value.Value >= value)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    private static BigInteger Divide(BigInteger numerator, BigInteger denominator) => denominator.Sign < 0 ? RocBankValue.RoundUnits(-numerator, -denominator) : RocBankValue.RoundUnits(numerator, denominator);
    internal double Next(double high, double low, double close, bool selected, bool commit, double? externalAverage = null)
    {
        var average = externalAverage ?? (_average is null ? _fallback!.Next(close, commit) : _average.Next(new RocBankValue(close), commit).Publish());
        var max = _high.First; while (max is not null && max.Value.Index <= _count - _length) max = max.Next;
        var min = _low.First; while (min is not null && min.Value.Index <= _count - _length) min = min.Next;
        var highest = max is null ? high : Math.Max(high, max.Value.Value); var lowest = min is null ? low : Math.Min(low, min.Value.Value);
        var total = new ExactMeanAccumulator(); total.Add(high); total.Add(low); total.Add(close); var typical = selected ? close : total.Mean(3);
        var span = RocBankValue.RoundUnits(ExactVarianceWindow.Units(highest) - ExactVarianceWindow.Units(lowest), BigInteger.One);
        var result = 0d;
        if (span.Sign != 0 && typical != 0)
        {
            var scale = Divide(ExactVarianceWindow.Units(25) << 1074, span);
            scale = RocBankValue.RoundUnits(scale * ExactVarianceWindow.Units(lowest), BigInteger.One << 1074);
            var change = RocBankValue.RoundUnits(ExactVarianceWindow.Units(_previous) - ExactVarianceWindow.Units(average), BigInteger.One);
            var normalized = Divide(change << 1074, ExactVarianceWindow.Units(typical));
            var slope = RocBankValue.RoundUnits(normalized * scale, BigInteger.One << 1074);
            var published = ExactMeanAccumulator.UnitRatio(slope, BigInteger.One);
            result = -Math.Round(Math.Atan(published) * (180 / Math.PI));
        }
        if (commit) { Commit(_high, high, true); Commit(_low, low, false); _previous = average; _count++; }
        return result;
    }
    internal void Reset() { _high.Clear(); _low.Clear(); _count = 0; _previous = 0; _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
