using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrendTraderWindow : IDisposable
{
    private readonly double _mult, _step;
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly KeltnerWindow _atr;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private RocBankValue _previousAtr, _stop;
    private double _highest, _lowest;
    internal static void Validate(double mult, double step)
    { if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult)); if (double.IsNaN(step) || double.IsInfinity(step)) throw new ArgumentOutOfRangeException(nameof(step)); }
    internal TrendTraderWindow(MovingAvgType kind, int length, double mult, double step, bool external = false, int capacityHint = int.MaxValue)
    {
        Validate(mult, step); _mult = mult; _step = step; length = Math.Max(1, length); _high = new(length); _low = new(length);
        _atr = new(MovingAvgType.SimpleMovingAverage, 1, length, external ? MovingAvgType.SimpleMovingAverage : kind, capacityHint);
        if (!external) { if (StrengthWindow.Supports(kind)) _average = new(kind, length, capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    private static RocBankValue Limit(double price, RocBankValue width, int sign)
    { var sum = new ExactMeanAccumulator(); width.AddTo(ref sum, sign); sum.Add(price); return RocBankValue.Round(sum); }
    private static int Compare(double price, RocBankValue limit)
    { var difference = new ExactMeanAccumulator(); difference.Add(price); limit.AddTo(ref difference, -1); return difference.Sign; }
    internal static (double Upper, double Middle, double Lower) Bands(RocBankValue middle, double step)
    { var upper = new ExactMeanAccumulator(); middle.AddTo(ref upper); upper.Add(step); var lower = new ExactMeanAccumulator(); middle.AddTo(ref lower); lower.Add(step, -1); return (upper.Mean(1), middle.Publish(), lower.Mean(1)); }
    internal (double Raw, double Upper, double Middle, double Lower) Next(double high, double low, double close, bool commit, RocBankValue? externalAtr = null)
    {
        var atr = externalAtr ?? _atr.Next(high, low, close, commit).Atr;
        var width = _previousAtr.Multiply(_mult); var highLimit = Limit(_highest, width, -1); var lowLimit = Limit(_lowest, width, 1);
        var highSide = Compare(close, highLimit); var lowSide = Compare(close, lowLimit);
        var stop = highSide > 0 && lowSide > 0 ? highLimit : highSide < 0 && lowSide < 0 ? lowLimit : _stop;
        var average = _average is not null ? _average.Next(stop, commit) : _fallback is not null ? new RocBankValue(_fallback.Next(stop.Publish(), commit)) : stop;
        var bands = Bands(average, _step);
        if (commit) { _previousAtr = atr; _stop = stop; _highest = _high.Add(close, out _); _lowest = _low.Add(close, out _); }
        return (stop.Publish(), bands.Upper, bands.Middle, bands.Lower);
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _atr.Reset(); _average?.Reset(); _fallback?.Reset(); _previousAtr = _stop = default; _highest = _lowest = 0; }
    public void Dispose() { _high.Dispose(); _low.Dispose(); _atr.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
