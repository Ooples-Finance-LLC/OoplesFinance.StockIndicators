using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveCandleWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly RocBankAverage? _signal;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly double _rate;
    private readonly long _settle;
    private long _count;
    private RocBankValue _body, _range, _body2, _range2;
    internal AdaptiveCandleWindow(MovingAvgType kind, int smoothLength, int stochLength, int signalLength, int capacityHint = int.MaxValue)
    {
        smoothLength = Math.Max(1, smoothLength); stochLength = Math.Max(1, stochLength);
        _rate = 2 / (smoothLength + 1d); _settle = 2L * (stochLength + (long)smoothLength);
        _high = new(Math.Min(stochLength, Math.Max(1, capacityHint))); _low = new(Math.Min(stochLength, Math.Max(1, capacityHint)));
        if (StrengthWindow.Supports(kind)) _signal = new(kind, signalLength, capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, signalLength));
    }
    private static RocBankValue Difference(double a, double b) { var sum = new ExactMeanAccumulator(); sum.Add(a); sum.Add(b, -1); return RocBankValue.Round(sum); }
    private static RocBankValue Blend(RocBankValue previous, RocBankValue current, double gain)
    {
        var sum = new ExactMeanAccumulator(); previous.AddTo(ref sum);
        var incoming = new ExactMeanAccumulator(); incoming.AddProduct(current.Mantissa, gain); incoming.ScaleByPowerOfTwo(current.UpperShift);
        var correction = new ExactMeanAccumulator(); correction.AddProduct(previous.Mantissa, gain); correction.ScaleByPowerOfTwo(previous.UpperShift);
        sum.Subtract(correction); correction = default; correction.Subtract(incoming); sum.Subtract(correction);
        return RocBankValue.Round(sum);
    }
    internal (RocBankValue Eco, double Signal) Next(double open, double high, double low, double close, bool commit)
    {
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _); var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var stochastic = ClampedRangePosition.Percent(close, lowest, highest);
        var gain = _rate * (Math.Abs(stochastic - 50) / 50);
        var currentBody = Difference(close, open); var currentRange = Difference(high, low);
        var seed = _count < _settle;
        var body = seed ? currentBody : Blend(_body, currentBody, gain); var range = seed ? currentRange : Blend(_range, currentRange, gain);
        var body2 = seed ? body : Blend(_body2, body, gain); var range2 = seed ? range : Blend(_range2, range, gain);
        RocBankValue eco = default;
        if (range2.Mantissa != 0)
        {
            var numerator = new ExactMeanAccumulator(); body2.AddTo(ref numerator); numerator.ScaleByPowerOfTwo(-range2.UpperShift);
            eco = RocBankValue.Round(numerator, range2.Mantissa).Multiply(100);
        }
        var signal = _signal is not null ? _signal.Next(eco, commit).Publish() : _fallback!.Next(eco.Publish(), commit);
        if (commit) { _body = body; _range = range; _body2 = body2; _range2 = range2; _count++; }
        return (eco, signal);
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _signal?.Reset(); _fallback?.Reset(); _body = _range = _body2 = _range2 = default; _count = 0; }
    public void Dispose() { _high.Dispose(); _low.Dispose(); _signal?.Dispose(); _fallback?.Dispose(); }
}
