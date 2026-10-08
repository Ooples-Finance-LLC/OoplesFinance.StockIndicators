using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

// corr(time, price) * population-deviation(price) = covariance(time, price) / deviation(time).
// Centered integer time eliminates absolute-index cancellation; history grows only as bars arrive.
internal sealed class OneLcWindow : IDisposable
{
    private readonly int _length;
    private readonly MovingAvgType _kind;
    private readonly Queue<double> _prices = new();
    private readonly RocBankAverage? _recursive;
    private readonly IMovingAverageSmoother? _fallback;
    private BigInteger _sum, _weighted, _difference;
    internal OneLcWindow(MovingAvgType kind, int length, bool externalAverage = false)
    {
        _kind = kind; _length = Math.Max(1, length);
        if (!externalAverage)
        {
            if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, _length, 1);
            else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
        }
    }
    internal (double Value, Signal Signal) Next(double price, bool commit, double? externalAverage = null)
    {
        var current = ExactVarianceWindow.Units(price); var weighted = _weighted - _sum + _length * current;
        var sum = _sum + current; if (_prices.Count == _length) sum -= ExactVarianceWindow.Units(_prices.Peek());
        var full = _prices.Count >= _length - 1; RocBankValue mean;
        if (externalAverage.HasValue) mean = new(externalAverage.Value);
        else if (_recursive is not null) mean = _recursive.Next(new(price), commit);
        else if (_fallback is not null) mean = new(_fallback.Next(price, commit));
        else mean = new(_kind == MovingAvgType.WeightedMovingAverage
            ? ExactMeanAccumulator.UnitRatio(weighted, (BigInteger)_length * (_length + 1L) / 2)
            : full ? ExactMeanAccumulator.UnitRatio(sum, _length) : 0);
        var correction = full && _length > 1 ? Correction(2 * weighted - (_length + 1L) * sum) : default;
        var total = new ExactMeanAccumulator(); mean.AddTo(ref total); correction.AddTo(ref total); var estimate = RocBankValue.Round(total);
        var difference = current - (ExactVarianceWindow.Units(estimate.Mantissa) << estimate.UpperShift);
        var signal = difference.Sign > 0 && difference > _difference ? Signal.StrongBuy : difference.Sign < 0 && difference < _difference ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price); _sum = sum; _weighted = weighted; _difference = difference; }
        return (estimate.Publish(), signal);
    }
    private RocBankValue Correction(BigInteger centered)
    {
        var n = new BigInteger(_length); var gain = ExactVarianceWindow.Units(1.7);
        var numerator = 3 * centered * centered * gain * gain; var denominator = (n * n * (n * n - 1)) << 2148;
        for (var shift = 0; ; shift += 32)
        {
            var magnitude = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift));
            if (!double.IsInfinity(magnitude)) return new(centered.Sign * magnitude, shift);
        }
    }
    internal void Reset() { _prices.Clear(); _sum = _weighted = _difference = default; _recursive?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); _prices.Clear(); }
}
