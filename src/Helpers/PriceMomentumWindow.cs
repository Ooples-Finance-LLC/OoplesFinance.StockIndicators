namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PriceMomentumWindow : IDisposable
{
    private readonly int _firstPeriod, _secondPeriod;
    private readonly RocBankAverage _signal;
    private RocBankValue _first, _second;
    private double _price;
    internal PriceMomentumWindow(MovingAvgType kind, int first, int second, int signal, int capacityHint = int.MaxValue)
    {
        _firstPeriod = Math.Max(1, first); _secondPeriod = Math.Max(1, second);
        _signal = new RocBankAverage(kind, signal, capacityHint);
    }
    internal (double Value, double Signal, double Histogram) Next(double price, bool final)
    {
        var change = RocBankValue.Return(price, _price);
        var firstSum = new ExactMeanAccumulator();
        _first.AddTo(ref firstSum, _firstPeriod - 2L); change.AddTo(ref firstSum, 2);
        var first = RocBankValue.Round(firstSum, count: _firstPeriod);
        var scaledSum = new ExactMeanAccumulator(); first.AddTo(ref scaledSum, 10);
        var scaled = RocBankValue.Round(scaledSum);
        var secondSum = new ExactMeanAccumulator();
        _second.AddTo(ref secondSum, _secondPeriod - 2L); scaled.AddTo(ref secondSum, 2);
        var second = RocBankValue.Round(secondSum, count: _secondPeriod);
        var signal = _signal.Next(second, final);
        var difference = new ExactMeanAccumulator(); second.AddTo(ref difference); signal.AddTo(ref difference, -1);
        if (final) { _price = price; _first = first; _second = second; }
        return (second.Publish(), signal.Publish(), RocBankValue.Round(difference).Publish());
    }
    internal void Reset() { _price = 0; _first = _second = default; _signal.Reset(); }
    public void Dispose() => _signal.Dispose();
}
