using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DynamicSupportWindow : IDisposable
{
    private readonly KeltnerWindow? _range;
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly double _multiplier;
    internal DynamicSupportWindow(MovingAvgType kind, int length, bool external = false, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length); _multiplier = Math.Sqrt(length);
        _high = new(length); _low = new(length);
        if (!external) _range = new(kind, length, length, kind, capacityHint);
    }
    internal (double Support, double Resistance, double Middle) Next(double high, double low, double close, bool commit, RocBankValue? suppliedAtr = null)
    {
        var atr = suppliedAtr ?? _range!.Next(high, low, close, commit).Atr;
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var support = new ExactMeanAccumulator(); support.AddProduct(atr.Mantissa, _multiplier, -1); support.ScaleByPowerOfTwo(atr.UpperShift); support.Add(highest);
        var resistance = new ExactMeanAccumulator(); resistance.AddProduct(atr.Mantissa, _multiplier); resistance.ScaleByPowerOfTwo(atr.UpperShift); resistance.Add(lowest);
        // The opposite ATR offsets cancel before midpoint rounding or publication.
        var middle = new ExactMeanAccumulator(); middle.Add(highest); middle.Add(lowest);
        return (support.Mean(1), resistance.Mean(1), middle.Mean(2));
    }
    internal void Reset() { _range?.Reset(); _high.Reset(); _low.Reset(); }
    public void Dispose() { _range?.Dispose(); _high.Dispose(); _low.Dispose(); }
}
