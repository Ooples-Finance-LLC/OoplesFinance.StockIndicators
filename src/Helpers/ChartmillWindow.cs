using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ChartmillWindow : IDisposable
{
    private readonly KeltnerWindow _range;
    private readonly RocBankAverage? _center;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly double _scale;
    internal ChartmillWindow(MovingAvgType kind, int length, int capacity = int.MaxValue)
    {
        length = Math.Max(1, length); _scale = Math.Sqrt(length); _range = new(kind, 1, length, kind, capacity);
        if (StrengthWindow.Supports(kind)) _center = new(kind, length, capacity); else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    private static double Normalize(double price, RocBankValue center, RocBankValue denominator)
    {
        if (denominator.Mantissa == 0) return 0;
        var difference = new ExactMeanAccumulator(); difference.Add(price); center.AddTo(ref difference, -1);
        var numerator = new ExactMeanAccumulator(); RocBankValue.Round(difference).AddTo(ref numerator);
        var divisor = new ExactMeanAccumulator(); denominator.AddTo(ref divisor);
        return Math.Max(-1, Math.Min(1, numerator.Ratio(divisor)));
    }
    internal (double Close, double Open, double High, double Low) Next(double high, double low, double open, double close, double input, bool commit, double? externalCenter = null)
    {
        var center = externalCenter.HasValue ? new RocBankValue(externalCenter.Value) : _center is null ? new RocBankValue(_fallback!.Next(input, commit)) : _center.Next(new RocBankValue(input), commit);
        var denominator = _range.Next(high, low, close, commit).Atr.Multiply(_scale);
        return (Normalize(close, center, denominator), Normalize(open, center, denominator), Normalize(high, center, denominator), Normalize(low, center, denominator));
    }
    internal void Reset() { _range.Reset(); _center?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _range.Dispose(); _center?.Dispose(); _fallback?.Dispose(); }
}
