using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AtrPercentBandWindow : IDisposable
{
    private readonly RocBankAverage? _basis;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly int _length;
    private readonly double _multiplier;
    private double _previousClose;
    private RocBankValue _previousPercent;
    internal AtrPercentBandWindow(MovingAvgType kind, int length, int bbLength, double multiplier, bool external = false, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); _multiplier = multiplier;
        if (!external) { if (StrengthWindow.Supports(kind)) _basis = new(kind, bbLength, capacityHint); else _fallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, bbLength)); }
    }
    private static RocBankValue Divide(ExactMeanAccumulator numerator, ExactMeanAccumulator denominator)
    {
        if (denominator.IsExactlyZero) return default;
        for (var shift = 0; ; shift += 1024)
        {
            var divisor = denominator; divisor.ScaleByPowerOfTwo(shift); var value = numerator.Ratio(divisor);
            if (!double.IsInfinity(value)) return new(value, shift);
        }
    }
    internal (double Upper, double Middle, double Lower) Next(double high, double low, double close, bool commit, RocBankValue? suppliedBasis = null)
    {
        // Compare exact ranges before division: ties prefer high gap, then low gap.
        var span = new ExactMeanAccumulator(); span.Add(high); span.Add(low, -1);
        var hc = new ExactMeanAccumulator(); hc.Add(high >= _previousClose ? high : _previousClose); hc.Add(high >= _previousClose ? _previousClose : high, -1);
        var lc = new ExactMeanAccumulator(); lc.Add(low >= _previousClose ? low : _previousClose); lc.Add(low >= _previousClose ? _previousClose : low, -1);
        var highVsSpan = hc; highVsSpan.Subtract(span); var highVsLow = hc; highVsLow.Subtract(lc); var lowVsSpan = lc; lowVsSpan.Subtract(span);
        var highWins = highVsSpan.Sign >= 0 && highVsLow.Sign >= 0;
        var numerator = highWins ? hc : lowVsSpan.Sign >= 0 ? lc : span;
        var denominator = numerator; denominator.ScaleByPowerOfTwo(-1); denominator.Add(highWins ? _previousClose : low);
        var fraction = Divide(numerator, denominator);
        var feedback = new ExactMeanAccumulator(); fraction.AddTo(ref feedback, 200); _previousPercent.AddTo(ref feedback, _length - 1L);
        var percent = RocBankValue.Round(feedback, count: _length + 1L);
        var deviation = percent.Multiply(_multiplier);
        var basis = suppliedBasis ?? (_basis is not null ? _basis.Next(new RocBankValue(close), commit) : new RocBankValue(_fallback!.Next(close, commit)));
        double Band(int sign)
        {
            var total = new ExactMeanAccumulator(); total.AddProduct(basis.Mantissa, deviation.Mantissa, sign); total.ScaleByPowerOfTwo(basis.UpperShift + deviation.UpperShift); basis.AddTo(ref total, 100); return total.Mean(100);
        }
        var result = (Band(1), basis.Publish(), Band(-1));
        if (commit) { _previousClose = close; _previousPercent = percent; }
        return result;
    }
    internal void Reset() { _basis?.Reset(); _fallback?.Reset(); _previousClose = 0; _previousPercent = default; }
    public void Dispose() { _basis?.Dispose(); _fallback?.Dispose(); }
}
