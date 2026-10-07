using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EdgePreservingWindow : IDisposable
{
    private readonly int _length, _smoothLength;
    private readonly MovingAvgType _kind;
    private readonly Queue<double> _prices = new();
    private readonly Queue<BigInteger> _offsets = new();
    private readonly LinkedList<(long Index, BigInteger Value)> _peaks = new();
    private ExactMeanAccumulator _priceSum, _priceWeighted;
    private BigInteger _offsetSum, _offsetWeighted, _runSum, _previousResidual;
    private long _index, _runCount;
    private bool _previousPeak;
    private readonly RocBankAverage? _recursive;
    private readonly IMovingAverageSmoother? _fallback;
    internal EdgePreservingWindow(MovingAvgType kind, int length, int smoothLength)
    {
        _kind = kind; _length = Math.Max(1, length); _smoothLength = Math.Max(1, smoothLength);
        if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, _length, 1);
        else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
    }
    private double Average(double price, bool final)
    {
        if (_recursive is not null) return _recursive.Next(new(price), final).Publish();
        if (_fallback is not null) return _fallback.Next(price, final);
        var sum = _priceSum; var weighted = _priceWeighted; weighted.Subtract(sum); weighted.Add(price, _length);
        if (_prices.Count == _length) sum.Add(_prices.Peek(), -1); sum.Add(price);
        var result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
            : _prices.Count < _length - 1 ? 0 : sum.Mean(_length);
        if (final) { _priceSum = sum; _priceWeighted = weighted; if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price); }
        return result;
    }
    private BigInteger Fit(BigInteger offset, bool final)
    {
        var full = _offsets.Count == _smoothLength; var expired = full ? _offsets.Peek() : BigInteger.Zero;
        var n = new BigInteger(full ? _offsets.Count : _offsets.Count + 1);
        var sum = _offsetSum + offset - expired;
        var weighted = full ? _offsetWeighted - _offsetSum + expired + (n - 1) * offset : _offsetWeighted + (n - 1) * offset;
        var spread = n * n - 1; var covariance = 2 * weighted - (n - 1) * sum;
        var endpoint = n.IsOne ? offset : RocBankValue.RoundUnits(sum * spread + 3 * covariance * (n - 1), n * spread);
        if (final) { if (full) _offsets.Dequeue(); _offsets.Enqueue(offset); _offsetSum = sum; _offsetWeighted = weighted; }
        return endpoint;
    }
    internal static bool NearPeak(BigInteger value, BigInteger peak)
    {
        if (peak.IsZero) return false;
        var ratio = ExactMeanAccumulator.UnitRatio((value * peak.Sign) << 1074, BigInteger.Abs(peak));
        return Math.Abs(ratio - 1) <= 1e-12;
    }
    internal (double Value, Signal Trade, bool Restart) Next(double price, bool final, double? externalAverage = null)
    {
        var mean = externalAverage ?? Average(price, final);
        var offset = RocBankValue.RoundUnits(ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(mean), BigInteger.One);
        var endpoint = Fit(BigInteger.Abs(offset), final); var expiry = _index - _length + 1L;
        var first = _peaks.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var peak = first is null ? endpoint : BigInteger.Max(endpoint, first.Value.Value);
        var atPeak = NearPeak(endpoint, peak);
        var restart = atPeak && !_previousPeak && !offset.IsZero;
        // Before the first reset, the published definition includes price[0] as a seed observation.
        var count = restart ? 1 : (_index == 0 ? 1 : _runCount) + 1;
        var sum = restart ? ExactVarianceWindow.Units(price) : (_index == 0 ? ExactVarianceWindow.Units(price) : _runSum) + ExactVarianceWindow.Units(price);
        var value = ExactMeanAccumulator.UnitRatio(sum, new BigInteger(count));
        var residual = ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(value);
        var trade = residual.Sign > 0 && residual > _previousResidual ? Signal.StrongBuy : residual.Sign < 0 && residual < _previousResidual ? Signal.StrongSell
            : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            while (_peaks.First is { } old && old.Value.Index < expiry) _peaks.RemoveFirst();
            while (_peaks.Last is { } last && last.Value.Value <= endpoint) _peaks.RemoveLast();
            _peaks.AddLast((_index, endpoint)); _index++; _runCount = count; _runSum = sum; _previousPeak = atPeak; _previousResidual = residual;
        }
        return (value, trade, restart);
    }
    internal void Reset()
    { _prices.Clear(); _offsets.Clear(); _peaks.Clear(); _priceSum = _priceWeighted = default; _offsetSum = _offsetWeighted = _runSum = _previousResidual = default; _index = _runCount = 0; _previousPeak = false; _recursive?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
}
