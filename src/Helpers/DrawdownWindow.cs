using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class DrawdownWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly PooledRingBuffer<BigInteger> _squares;
    private BigInteger _sum;
    internal DrawdownWindow(int length) { length = Math.Max(1, length); _high = new(length); _squares = new(length); }
    internal (BigInteger Sum, int Count) NextMoments(double price, bool commit)
    {
        var high = commit ? _high.Add(price, out _) : _high.Preview(price, out _);
        var drawdown = RocBankValue.Return(price, high);
        var units = ExactVarianceWindow.Units(drawdown.Mantissa) << drawdown.UpperShift;
        var square = units * units;
        var sum = _sum + square - (_squares.Count == _squares.Capacity ? _squares[0] : BigInteger.Zero);
        var count = Math.Min(_squares.Count + 1, _squares.Capacity);
        if (commit) { _squares.TryAdd(square, out _); _sum = sum; }
        return (sum, count);
    }
    internal double Next(double price, bool commit)
    {
        var (sum, count) = NextMoments(price, commit);
        return ExactPopulationDeviation.RootRatio(sum, count);
    }
    internal void Reset() { _high.Reset(); _squares.Clear(); _sum = 0; }
    public void Dispose() { _high.Dispose(); _squares.Dispose(); }
}

internal sealed class MartinWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _prices;
    private readonly DrawdownWindow _drawdown;
    private readonly RocBankAverage? _mean;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly double _benchmark;
    internal MartinWindow(MovingAvgType kind, int length, double benchmark)
    {
        length = Math.Max(1, length);
        if (!double.IsFinite(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
        _benchmark = Math.Pow(1 + benchmark, length / 360d) - 1;
        if (!double.IsFinite(_benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
        _prices = new(length); _drawdown = new(length);
        if (StrengthWindow.Supports(kind)) _mean = new RocBankAverage(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal RocBankValue ReturnValue(double price, double previous)
    {
        if (previous == 0) return default;
        var difference = new ExactMeanAccumulator(); difference.Add(price, 100); difference.Add(previous, -100);
        difference.AddProduct(previous, _benchmark, -100);
        return RocBankValue.Round(difference, previous);
    }
    internal double Next(double price, bool commit, double? customerMean = null)
    {
        var previous = _prices.Count == _prices.Capacity ? _prices[0] : 0;
        var change = ReturnValue(price, previous);
        var mean = customerMean.HasValue ? new RocBankValue(customerMean.Value)
            : _mean is null ? new RocBankValue(_fallback!.Next(change.Publish(), commit)) : _mean.Next(change, commit);
        var numerator = ExactVarianceWindow.Units(mean.Mantissa) << mean.UpperShift;
        var (sum, count) = _drawdown.NextMoments(price, commit);
        var result = sum.IsZero ? 0 : numerator.Sign * ExactPopulationDeviation.RootRatio((numerator * numerator * count) << 2148, sum);
        if (commit) _prices.TryAdd(price, out _);
        return result;
    }
    internal void Reset() { _prices.Clear(); _drawdown.Reset(); _mean?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _prices.Dispose(); _drawdown.Dispose(); _mean?.Dispose(); _fallback?.Dispose(); }
}
