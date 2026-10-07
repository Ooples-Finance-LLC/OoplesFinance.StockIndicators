using System.Numerics;
namespace OoplesFinance.StockIndicators.Streaming;
internal sealed class EhlersRoofingV1Window : IDisposable
{
    private readonly EhlersRoofingInputKernel _input;
    private readonly Average _average;
    internal static bool Supports(MovingAvgType kind) => kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV1 || StrengthWindow.Supports(kind);
    internal EhlersRoofingV1Window(MovingAvgType kind, int high, int low) { _input = new(high); _average = new(kind, Math.Max(1, low)); }
    internal double Next(double price, bool commit) => _average.Next(_input.NextExtended(price, commit), commit).Publish();
    internal void Reset() { _input.Reset(); _average.Reset(); }
    public void Dispose() => _average.Dispose();
    // Preserve the exact binary64 radius/cosine polynomial and three-input startup
    // of TwoPoleWindow variant 2, while accepting an extended high-pass value.
    private sealed class ExtendedSuperSmoother
    {
        private readonly BigInteger _gain, _feedback1, _feedback2;
        private RocBankValue _previous, _older;
        private int _count;
        internal ExtendedSuperSmoother(int length)
        {
            var angle = Math.Sqrt(2) * Math.PI / Math.Max(2, length);
            var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(angle));
            _feedback1 = 2 * radius * cosine; _feedback2 = -radius * radius; _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
        }
        private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, BigInteger coefficient)
        { var term = new ExactMeanAccumulator(); term.Add(value.Mantissa, -coefficient); term.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(term); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            RocBankValue result;
            if (_count < 3) result = value;
            else { var sum = new ExactMeanAccumulator(); Product(ref sum, value, _gain); Product(ref sum, _previous, _feedback1); Product(ref sum, _older, _feedback2); sum.ScaleByPowerOfTwo(-2148); result = RocBankValue.Round(sum); }
            if (commit) { _older = _previous; _previous = result; if (_count < 3) _count++; }
            return result;
        }
        internal void Reset() { _previous = _older = default; _count = 0; }
    }
    private sealed class Average : IDisposable
    {
        private readonly ExtendedSuperSmoother? _super;
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV1) _super = new(length); else if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_super is not null) return _super.Next(value, commit);
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _super?.Reset(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
