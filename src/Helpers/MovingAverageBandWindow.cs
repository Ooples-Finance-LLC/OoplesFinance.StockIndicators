using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MovingAverageBandWindow : IDisposable
{
    private readonly RocBankAverage? _fast, _slow;
    private readonly IMovingAverageSmoother? _fastFallback, _slowFallback;
    private readonly PooledRingBuffer<RocBankValue> _gaps;
    private readonly double _mult;
    private ExactMeanAccumulator _squares;
    internal MovingAverageBandWindow(MovingAvgType kind, int fastLength, int slowLength, double mult, bool external = false)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
        fastLength = Math.Max(1, fastLength); slowLength = Math.Max(1, slowLength); _mult = mult; _gaps = new(fastLength);
        if (external) return;
        if (StrengthWindow.Supports(kind)) { _fast = new(kind, fastLength, int.MaxValue); _slow = new(kind, slowLength, int.MaxValue); }
        else { _fastFallback = MovingAverageSmootherFactory.Create(kind, fastLength); _slowFallback = MovingAverageSmootherFactory.Create(kind, slowLength); }
    }
    private static void Square(ref ExactMeanAccumulator sum, RocBankValue value, int sign)
    {
        var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, value.Mantissa, -sign);
        negative.ScaleByPowerOfTwo(2 * value.UpperShift); sum.Subtract(negative);
    }
    internal (double Upper, double Middle, double Lower, double Fast, double Width) Next(double price, bool commit, double? fastValue = null, double? slowValue = null)
    {
        var fast = fastValue ?? (_fast is null ? _fastFallback!.Next(price, commit) : _fast.Next(new RocBankValue(price), commit).Publish());
        var slow = slowValue ?? (_slow is null ? _slowFallback!.Next(price, commit) : _slow.Next(new RocBankValue(price), commit).Publish());
        var difference = new ExactMeanAccumulator(); difference.Add(slow); difference.Add(fast, -1); var gap = RocBankValue.Round(difference);
        var squares = _squares; var full = _gaps.Count == _gaps.Capacity;
        if (full) Square(ref squares, _gaps[0], -1); Square(ref squares, gap, 1);
        var count = full ? _gaps.Capacity : _gaps.Count + 1; var root = squares.SqrtMean(count);
        RocBankValue deviation;
        if (double.IsInfinity(root)) { var scaled = squares; scaled.ScaleByPowerOfTwo(-2); deviation = new(scaled.SqrtMean(count), 1); }
        else deviation = new(root);
        var width = deviation.Multiply(_mult);
        var upper = new ExactMeanAccumulator(); upper.Add(slow); width.AddTo(ref upper);
        var lower = new ExactMeanAccumulator(); lower.Add(slow); width.AddTo(ref lower, -1);
        var numerator = new ExactMeanAccumulator(); width.AddTo(ref numerator, 200); var denominator = new ExactMeanAccumulator(); denominator.Add(slow);
        var bandWidth = slow == 0 ? 0 : numerator.Ratio(denominator);
        if (commit) { _squares = squares; _gaps.TryAdd(gap, out _); }
        return (upper.Mean(1), slow, lower.Mean(1), fast, bandWidth);
    }
    internal void Reset() { _fast?.Reset(); _slow?.Reset(); _fastFallback?.Reset(); _slowFallback?.Reset(); _gaps.Clear(); _squares = default; }
    public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); _fastFallback?.Dispose(); _slowFallback?.Dispose(); _gaps.Dispose(); }
}
