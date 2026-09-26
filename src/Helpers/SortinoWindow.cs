using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

// Downside squares are stored multiplied by 2^2148 so squaring a finite return
// cannot underflow. Binary64 precision is retained at each mean/square stage;
// only the final normalized root is restricted to the published exponent range.
internal sealed class SortinoWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _prices;
    private readonly RocBankAverage? _mean, _downside;
    private readonly IMovingAverageSmoother? _meanFallback, _downsideFallback;
    private readonly double _benchmark;
    internal SortinoWindow(MovingAvgType kind, int length, double benchmark)
    {
        length = Math.Max(1, length);
        if (!double.IsFinite(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
        _benchmark = Math.Pow(1 + benchmark, length / 360d) - 1;
        if (!double.IsFinite(_benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
        _prices = new(length);
        if (StrengthWindow.Supports(kind))
        { _mean = new RocBankAverage(kind, length, int.MaxValue); _downside = new RocBankAverage(kind, length, int.MaxValue); }
        else
        { _meanFallback = MovingAverageSmootherFactory.Create(kind, length); _downsideFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal RocBankValue ReturnValue(double price, double previous)
    {
        if (previous == 0) return default;
        var difference = new ExactMeanAccumulator(); difference.Add(price); difference.Add(previous, -1);
        difference.AddProduct(previous, _benchmark, -1);
        return RocBankValue.Round(difference, previous);
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal static RocBankValue DownsideSquare(RocBankValue value)
    {
        if (value.Mantissa >= 0) return default;
        var units = Units(value); var square = new ExactMeanAccumulator(); square.Add(1, units * units);
        return RocBankValue.Round(square);
    }
    internal static double PublishSquare(RocBankValue scaled) => ExactMeanAccumulator.UnitRatio(Units(scaled), BigInteger.One << 2148);
    internal double Next(double price, bool commit, double? customerMean = null, double? customerDownside = null)
    {
        var previous = _prices.Count == _prices.Capacity ? _prices[0] : 0;
        var change = ReturnValue(price, previous); var square = DownsideSquare(change);
        var mean = customerMean.HasValue ? new RocBankValue(customerMean.Value)
            : _mean is null ? new RocBankValue(_meanFallback!.Next(change.Publish(), commit)) : _mean.Next(change, commit);
        var downside = customerDownside.HasValue ? new RocBankValue(customerDownside.Value, 2148)
            : _downside is null ? new RocBankValue(_downsideFallback!.Next(PublishSquare(square), commit), 2148) : _downside.Next(square, commit);
        var numerator = Units(mean); var denominator = Units(downside);
        var result = denominator.Sign <= 0 ? 0
            : numerator.Sign * ExactPopulationDeviation.RootRatio((numerator * numerator) << 3222, denominator);
        if (commit) _prices.TryAdd(price, out _);
        return result;
    }
    internal void Reset() { _prices.Clear(); _mean?.Reset(); _downside?.Reset(); _meanFallback?.Reset(); _downsideFallback?.Reset(); }
    public void Dispose() { _prices.Dispose(); _mean?.Dispose(); _downside?.Dispose(); _meanFallback?.Dispose(); _downsideFallback?.Dispose(); }
}
