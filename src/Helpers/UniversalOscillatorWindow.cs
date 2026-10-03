using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class UniversalOscillatorWindow : IDisposable
{
    private readonly double _c1, _c2, _c3;
    private double _price1, _price2, _previous;
    private int _count;
    private Scaled _noise, _first, _second, _peak;
    private readonly Average _signal;
    internal UniversalOscillatorWindow(MovingAvgType kind, int length, int signalLength = 9)
    {
        var angle = 1.414 * Math.PI / Math.Max(1, length); var radius = Math.Exp(-Math.Max(.01, Math.Min(.99, angle)));
        _c2 = 2 * radius * Math.Cos(angle); _c3 = -radius * radius; _c1 = 1 - _c2 - _c3;
        _signal = new(kind, Math.Max(1, signalLength));
    }
    // Normalization needs binary64 precision with an exponent in both directions:
    // neither an overflowing filtered value nor an underflowing peak may erase a ratio.
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(1); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(1); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(1); }
            return new(value, shift);
        }
    }
    internal double Line(double price, bool commit)
    {
        var difference = new ExactMeanAccumulator();
        if (_count >= 2) { difference.Add(price); difference.Add(_price2, -1); difference.ScaleByPowerOfTwo(-1); }
        var noise = Scaled.Round(difference);
        var mean = new ExactMeanAccumulator(); noise.Add(ref mean); _noise.Add(ref mean); mean.ScaleByPowerOfTwo(-1);
        var total = new ExactMeanAccumulator(); Scaled.Round(mean).Add(ref total, _c1); _first.Add(ref total, _c2); _second.Add(ref total, _c3); var filtered = Scaled.Round(total);
        var retained = new ExactMeanAccumulator(); _peak.Add(ref retained, .991); var peak = Scaled.Round(retained);
        var magnitude = new Scaled(Math.Abs(filtered.Mantissa), filtered.Shift); var comparison = new ExactMeanAccumulator(); magnitude.Add(ref comparison); peak.Add(ref comparison, -1);
        if (comparison.Sign > 0) peak = magnitude;
        var numerator = new ExactMeanAccumulator(); filtered.Add(ref numerator); var denominator = new ExactMeanAccumulator(); peak.Add(ref denominator);
        var line = peak.Mantissa == 0 ? _previous : numerator.Ratio(denominator);
        if (commit) { _price2 = _price1; _price1 = price; _noise = noise; _second = _first; _first = filtered; _peak = peak; _previous = line; if (_count < 2) _count++; }
        return line;
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    { var line = Line(price, commit); return (line, _signal.Next(new RocBankValue(line), commit).Publish()); }
    internal void Reset() { _price1 = _price2 = _previous = 0; _count = 0; _noise = _first = _second = _peak = default; _signal.Reset(); }
    public void Dispose() => _signal.Dispose();
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
