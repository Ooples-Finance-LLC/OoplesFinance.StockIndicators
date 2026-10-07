namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AtrTrailingWindow : IDisposable
{
    private readonly KeltnerWindow? _range;
    private readonly double _factor;
    private RocBankValue _previous;
    private bool _hasPrevious;
    internal AtrTrailingWindow(MovingAvgType kind, int trendLength, int rangeLength, double factor, bool external = false, int capacityHint = int.MaxValue)
    { if (!external) _range = new(kind, trendLength, rangeLength, kind, capacityHint); _factor = factor; }
    internal double Next(double high, double low, double close, bool commit, RocBankValue? suppliedTrend = null, RocBankValue? suppliedAtr = null)
    {
        var stages = suppliedTrend.HasValue ? (suppliedTrend.Value, suppliedAtr!.Value) : _range!.Next(high, low, close, commit);
        var trend = new ExactMeanAccumulator(); trend.Add(close); stages.Item1.AddTo(ref trend, -1); var up = trend.Sign > 0;
        var boundary = new ExactMeanAccumulator(); boundary.AddProduct(stages.Item2.Mantissa, _factor, up ? -1 : 1); boundary.ScaleByPowerOfTwo(stages.Item2.UpperShift); boundary.Add(close);
        var candidate = RocBankValue.Round(boundary); var previous = _hasPrevious ? _previous : new RocBankValue(close);
        var movement = new ExactMeanAccumulator(); candidate.AddTo(ref movement); previous.AddTo(ref movement, -1);
        var stop = (up ? movement.Sign > 0 : movement.Sign < 0) ? candidate : previous;
        if (commit) { _previous = stop; _hasPrevious = true; }
        return stop.Publish();
    }
    internal void Reset() { _range?.Reset(); _previous = default; _hasPrevious = false; }
    public void Dispose() => _range?.Dispose();
}
