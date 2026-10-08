using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PriceCycleWindow : IDisposable
{
    private readonly KeltnerWindow _range;
    private readonly RocBankAverage? _distance;
    private readonly IMovingAverageSmoother? _fallback;
    internal PriceCycleWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length);
        _range = new(kind, 1, length, kind, capacityHint);
        if (StrengthWindow.Supports(kind)) _distance = new(kind, length, capacityHint);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal static RocBankValue Distance(double price, double low)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(price); sum.Add(low, -1);
        return RocBankValue.Round(sum);
    }
    internal static double Finish(RocBankValue distance, RocBankValue atr)
    {
        if (atr.Mantissa == 0) return 0;
        var numerator = new ExactMeanAccumulator(); distance.AddTo(ref numerator, 100);
        var denominator = new ExactMeanAccumulator(); atr.AddTo(ref denominator);
        return numerator.Ratio(denominator);
    }
    internal double Next(double high, double low, double price, bool commit)
    {
        var atr = _range.Next(high, low, price, commit).Atr;
        var difference = Distance(price, low);
        var distance = _distance is not null ? _distance.Next(difference, commit) : new RocBankValue(_fallback!.Next(difference.Publish(), commit));
        return Finish(distance, atr);
    }
    internal void Reset() { _range.Reset(); _distance?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _range.Dispose(); _distance?.Dispose(); _fallback?.Dispose(); }
}
