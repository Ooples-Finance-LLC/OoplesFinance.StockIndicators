using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PriceVolumeTrendTotal
{
    private readonly bool _modified;
    private ExactMeanAccumulator _total;
    private double _previous;
    internal PriceVolumeTrendTotal(bool modified = false) => _modified = modified;
    internal RocBankValue Next(double price, double volume, bool commit)
    {
        var total = _modified && _previous == 0 ? new ExactMeanAccumulator() : _total;
        if (_previous != 0)
        {
            var numerator = new ExactMeanAccumulator(); numerator.AddProduct(price, volume); numerator.AddProduct(_previous, volume, -1);
            RocBankValue.Round(numerator, _previous, _modified ? 50000 : 1).AddTo(ref total);
        }
        if (commit) { _total = total; _previous = price; }
        return RocBankValue.Round(total);
    }
    internal void Reset() { _total = default; _previous = 0; }
}

internal sealed class PriceVolumeTrendWindow : IDisposable
{
    private readonly PriceVolumeTrendTotal _total;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal PriceVolumeTrendWindow(MovingAvgType kind, int length, bool modified = false)
    {
        _total = new(modified);
        if (StrengthWindow.Supports(kind)) _average = new RocBankAverage(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal (double Line, double Signal) Next(double price, double volume, bool commit, double? customerSignal = null)
    {
        var line = _total.Next(price, volume, commit);
        var signal = customerSignal.HasValue ? new RocBankValue(customerSignal.Value) : _average is null
            ? new RocBankValue(_fallback!.Next(line.Publish(), commit)) : _average.Next(line, commit);
        return (line.Publish(), signal.Publish());
    }
    internal void Reset() { _total.Reset(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}
