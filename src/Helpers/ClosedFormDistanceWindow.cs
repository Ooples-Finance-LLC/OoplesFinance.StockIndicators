using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

// sqrt(1 - fourthRoot(4*H*L/(H+L)^2)). Exact window sums and
// polynomial midpoint comparisons avoid both overflowing powers and cancellation.
internal sealed class ClosedFormDistanceWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<(BigInteger High, BigInteger Low)> _history = new();
    private BigInteger _high, _low, _previousResidual;
    private double _previousVolatility;
    private readonly Average? _average;
    internal ClosedFormDistanceWindow(MovingAvgType kind, int length, bool external = false)
    { _length = Math.Max(1, length); if (!external) _average = new(kind, _length); }
    internal static void ValidateRange(double high, double low)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        if (high < 0) throw new ArgumentOutOfRangeException(nameof(high));
        if (low < 0) throw new ArgumentOutOfRangeException(nameof(low));
    }
    internal static void ValidateRanges(IReadOnlyList<double> high, IReadOnlyList<double> low)
    { for (var i = 0; i < high.Count; i++) ValidateRange(high[i], low[i]); }
    internal static double Distance(BigInteger high, BigInteger low)
    {
        if (high == low) return 0;
        if (high.IsZero || low.IsZero) return 1;
        var sum = high + low; var sumSquared = sum * sum; var product = 4 * high * low;
        var normalizedGap = ExactMeanAccumulator.UnitRatio(BigInteger.Abs(high - low) << 1074, sum);
        var geometric = ExactPopulationDeviation.RootRatio(product << 2148, sumSquared);
        var estimate = normalizedGap / Math.Sqrt((1 + geometric) * (1 + Math.Sqrt(geometric)));
        var bits = BitConverter.DoubleToInt64Bits(estimate);
        const long one = 0x3ff0000000000000;
        // A midpoint is represented exactly in half-subnormal units. The
        // polynomial decreases on [0,1], so its sign locates the desired root.
        int Compare(long left, long right)
        {
            var midpoint = ExactVarianceWindow.Units(BitConverter.Int64BitsToDouble(left)) + ExactVarianceWindow.Units(BitConverter.Int64BitsToDouble(right));
            var residual = (BigInteger.One << 2150) - midpoint * midpoint;
            return (BigInteger.Pow(residual, 4) * sumSquared).CompareTo(product << 8600);
        }
        while (true)
        {
            if (bits > 0)
            {
                var lower = Compare(bits - 1, bits);
                if (lower < 0 || lower == 0 && (bits & 1) != 0) { bits--; continue; }
            }
            if (bits < one)
            {
                var upper = Compare(bits, bits + 1);
                if (upper > 0 || upper == 0 && (bits & 1) != 0) { bits++; continue; }
            }
            return BitConverter.Int64BitsToDouble(bits);
        }
    }
    internal (double Value, Signal Signal) Next(double high, double low, double price, bool final, double? externalMean = null)
    {
        ValidateRange(high, low); var current = ExactVarianceWindow.Units(price);
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low);
        var full = _history.Count == _length; var old = full ? _history.Peek() : default;
        var sumHigh = _high + h - old.High; var sumLow = _low + l - old.Low;
        var value = Distance(sumHigh, sumLow);
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _average!.Next(new(price), final);
        var residual = current - (ExactVarianceWindow.Units(mean.Mantissa) << mean.UpperShift);
        var signal = value < _previousVolatility ? Signal.None : residual.Sign > 0 && residual > _previousResidual ? Signal.StrongBuy
            : residual.Sign < 0 && residual < _previousResidual ? Signal.StrongSell : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { if (full) _history.Dequeue(); _history.Enqueue((h, l)); _high = sumHigh; _low = sumLow; _previousResidual = residual; _previousVolatility = value; }
        return (value, signal);
    }
    internal void Reset() { _history.Clear(); _high = _low = _previousResidual = default; _previousVolatility = 0; _average?.Reset(); }
    public void Dispose() => _average?.Dispose();
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(value, final);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), final));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count < _length - 1 ? default : RocBankValue.Round(sum, count: _length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
