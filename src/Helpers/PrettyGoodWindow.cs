namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PrettyGoodWindow : IDisposable
{
    private readonly KeltnerWindow _averages;
    internal PrettyGoodWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
        => _averages = new(kind, Math.Max(1, length), Math.Max(1, length), kind, capacityHint);
    internal static double Finish(double price, RocBankValue average, RocBankValue atr)
    {
        if (atr.Mantissa == 0) return 0;
        var numerator = new ExactMeanAccumulator(); numerator.Add(price); average.AddTo(ref numerator, -1);
        var denominator = new ExactMeanAccumulator(); atr.AddTo(ref denominator);
        return numerator.Ratio(denominator);
    }
    internal double Next(double high, double low, double price, bool commit)
    {
        var values = _averages.Next(high, low, price, commit);
        return Finish(price, values.Middle, values.Atr);
    }
    internal static double[] TrueRanges(IReadOnlyList<double> high, IReadOnlyList<double> low, IReadOnlyList<double> prices)
    {
        using var range = new KeltnerWindow(MovingAvgType.SimpleMovingAverage, 1, 1, MovingAvgType.SimpleMovingAverage, Math.Max(1, prices.Count));
        var values = new double[prices.Count];
        for (var i = 0; i < prices.Count; i++) values[i] = range.Next(high[i], low[i], prices[i], true).Atr.Publish();
        return values;
    }
    internal void Reset() => _averages.Reset();
    public void Dispose() => _averages.Dispose();
}
