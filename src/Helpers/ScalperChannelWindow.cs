using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ScalperChannelWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly KeltnerWindow _average;
    internal ScalperChannelWindow(MovingAvgType kind, int rangeLength, int averageLength, int capacityHint = int.MaxValue)
    { _high = new(Math.Max(1, rangeLength)); _low = new(Math.Max(1, rangeLength)); _average = new(kind, averageLength, averageLength, kind, capacityHint); }
    internal static double Scalper(RocBankValue average, RocBankValue atr)
    {
        if (atr.Mantissa <= 0) return average.Publish();
        var scaled = atr.Multiply(Math.PI);
        var logarithm = Math.Log(scaled.Mantissa) + scaled.UpperShift * Math.Log(2);
        var result = new ExactMeanAccumulator(); average.AddTo(ref result); result.Add(logarithm, -1); return result.Mean(1);
    }
    internal (double Upper, double Middle, double Lower, double Scalper) Next(double high, double low, double close, bool commit, RocBankValue? externalAverage = null, RocBankValue? externalAtr = null)
    {
        var upper = commit ? _high.Add(high, out _) : _high.Preview(high, out _); var lower = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var stages = externalAverage.HasValue && externalAtr.HasValue ? (Middle: externalAverage.Value, Atr: externalAtr.Value) : _average.Next(high, low, close, commit);
        var midpoint = new ExactMeanAccumulator(); midpoint.Add(upper); midpoint.Add(lower);
        return (upper, midpoint.Mean(2), lower, Scalper(stages.Middle, stages.Atr));
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _average.Reset(); }
    public void Dispose() { _high.Dispose(); _low.Dispose(); _average.Dispose(); }
}
