using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

// Round returns once, retaining an extended upper exponent. Normalize their
// mean by exact population moments before publishing any unbounded intermediate.
internal sealed class ReturnScoreWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<BigInteger> _returns;
    private readonly RocBankAverage? _mean;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly bool _information, _simple;
    private readonly double _benchmark;
    private BigInteger _sum, _squares;
    internal ReturnScoreWindow(MovingAvgType kind, int length, double benchmark, bool information)
    {
        length = Math.Max(1, length);
        if (!double.IsFinite(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
        _benchmark = Math.Pow(1 + benchmark, length / 360d) - 1;
        if (!double.IsFinite(_benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
        _information = information; _simple = kind == MovingAvgType.SimpleMovingAverage;
        _prices = new(length); _returns = new(length);
        if (StrengthWindow.Supports(kind)) _mean = new RocBankAverage(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal RocBankValue ReturnValue(double price, double previous)
    {
        if (previous == 0) return default;
        var difference = new ExactMeanAccumulator(); difference.Add(price); difference.Add(previous, -1);
        if (!_information) difference.AddProduct(previous, _benchmark, -1);
        return RocBankValue.Round(difference, previous);
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal double Next(double price, bool commit, double? customerMean = null)
    {
        var previous = _prices.Count == _prices.Capacity ? _prices[0] : 0;
        var change = ReturnValue(price, previous); var value = Units(change);
        var mean = customerMean.HasValue ? new RocBankValue(customerMean.Value)
            : _mean is null ? new RocBankValue(_fallback!.Next(change.Publish(), commit)) : _mean.Next(change, commit);
        var expired = _returns.Count == _returns.Capacity ? _returns[0] : BigInteger.Zero;
        var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired;
        var n = new BigInteger(_returns.Capacity);
        var variance = n * squares - sum * sum;
        var numerator = _simple && !customerMean.HasValue ? sum : n * Units(mean);
        if (_information) numerator -= n * ExactVarianceWindow.Units(_benchmark);
        var result = _returns.Count < _returns.Capacity - 1 || variance.IsZero ? 0
            : numerator.Sign * ExactPopulationDeviation.RootRatio((numerator * numerator) << 2148, variance);
        if (commit) { _prices.TryAdd(price, out _); _returns.TryAdd(value, out _); _sum = sum; _squares = squares; }
        return result;
    }
    internal void Reset() { _prices.Clear(); _returns.Clear(); _mean?.Reset(); _fallback?.Reset(); _sum = _squares = 0; }
    public void Dispose() { _prices.Dispose(); _returns.Dispose(); _mean?.Dispose(); _fallback?.Dispose(); }
}
