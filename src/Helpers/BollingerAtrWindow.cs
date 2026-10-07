namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class BollingerAtrWindow : IDisposable
{
    private readonly KeltnerWindow? _range;
    private readonly ExactPopulationWindow _deviation;
    private readonly double _multiplier;
    internal BollingerAtrWindow(MovingAvgType kind, int atrLength, int length, double multiplier, bool external = false, int capacityHint = int.MaxValue)
    {
        if (!external) _range = new(kind, atrLength, atrLength, kind, capacityHint);
        _deviation = new(length); _multiplier = multiplier;
    }
    internal (double Value, double SignalCenter) Next(double high, double low, double close, bool commit, RocBankValue? suppliedAtr = null, double suppliedCenter = 0)
    {
        var deviation = _deviation.Next(close, commit);
        var stages = suppliedAtr.HasValue ? (new RocBankValue(suppliedCenter), suppliedAtr.Value) : _range!.Next(high, low, close, commit);
        var denominator = new ExactMeanAccumulator(); denominator.AddProduct(deviation, _multiplier, 2);
        if (denominator.IsExactlyZero) return (0, stages.Item1.Publish());
        var numerator = new ExactMeanAccumulator(); stages.Item2.AddTo(ref numerator);
        return (numerator.Ratio(denominator), stages.Item1.Publish());
    }
    internal void Reset() { _range?.Reset(); _deviation.Reset(); }
    public void Dispose() { _range?.Dispose(); _deviation.Dispose(); }
}
