using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RmseBandWindow : IDisposable
{
    private readonly RocBankAverage? _mean, _squares;
    private readonly IMovingAverageSmoother? _meanFallback, _squaresFallback;
    private readonly double _factor;
    internal RmseBandWindow(MovingAvgType kind, int length, double factor, bool external = false, int capacityHint = int.MaxValue)
    {
        HighLowBandsWindow.ValidateShift(factor); _factor = factor; length = Math.Max(1, length);
        if (!external) { if (StrengthWindow.Supports(kind)) { _mean = new(kind, length, capacityHint); _squares = new(kind, length, capacityHint); } else { _meanFallback = MovingAverageSmootherFactory.Create(kind, length); _squaresFallback = MovingAverageSmootherFactory.Create(kind, length); } }
    }
    // Carry squares in units of epsilon squared. Their exponent can grow, while
    // a tiny residual is not lost before its root becomes a representable output.
    internal static RocBankValue ScaledVariance(double value)
    { var sum = new ExactMeanAccumulator(); sum.Add(value); sum.ScaleByPowerOfTwo(2148); return RocBankValue.Round(sum); }
    private static RocBankValue Root(RocBankValue variance)
    {
        if (variance.Mantissa <= 0) return default;
        var sum = new ExactMeanAccumulator(); variance.AddTo(ref sum); sum.ScaleByPowerOfTwo(-2148);
        for (var shift = 0; ; shift += 1024)
        {
            var root = sum.SqrtMean(1); if (!double.IsInfinity(root)) return new RocBankValue(root, shift);
            sum.ScaleByPowerOfTwo(-2048);
        }
    }
    internal static (double Upper, double Middle, double Lower) Bands(double middle, RocBankValue variance, double factor) => KeltnerWindow.Bands(new RocBankValue(middle), Root(variance), factor);
    internal (double RawSquare, double Upper, double Middle, double Lower) Next(double close, bool commit, double? externalMean = null)
    {
        var middle = externalMean ?? (_mean is not null ? _mean.Next(new RocBankValue(close), commit).Publish() : _meanFallback!.Next(close, commit));
        var square = new ExactMeanAccumulator(); square.AddProduct(close, close); square.AddProduct(middle, middle); square.AddProduct(close, middle, -2);
        var rawSquare = square.Mean(1); square.ScaleByPowerOfTwo(2148); var scaledSquare = RocBankValue.Round(square);
        var variance = _squares is not null ? _squares.Next(scaledSquare, commit) : _squaresFallback is not null ? ScaledVariance(_squaresFallback.Next(rawSquare, commit)) : default;
        var bands = Bands(middle, variance, _factor); return (rawSquare, bands.Upper, bands.Middle, bands.Lower);
    }
    internal void Reset() { _mean?.Reset(); _squares?.Reset(); _meanFallback?.Reset(); _squaresFallback?.Reset(); }
    public void Dispose() { _mean?.Dispose(); _squares?.Dispose(); _meanFallback?.Dispose(); _squaresFallback?.Dispose(); }
}
