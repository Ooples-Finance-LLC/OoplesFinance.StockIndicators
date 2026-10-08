using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RoofingStochasticWindow : IDisposable
{
    private readonly bool _modified;
    private readonly int _length;
    private readonly double _pole, _highGain;
    private readonly BigInteger _gain, _feedback1, _feedback2;
    private readonly Average _roofAverage;
    private readonly Average? _finalAverage;
    private readonly LinkedList<(long Index, Scaled Value)> _minimum = new(), _maximum = new();
    private long _index;
    private double _price1, _price2, _price3, _rank;
    private Scaled _high1, _high2, _output1, _output2;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.Ehlers2PoleSuperSmootherFilterV1 or MovingAvgType.WeightedMovingAverage;
    internal RoofingStochasticWindow(MovingAvgType kind, int high, int low, int length, bool modified, bool external = false)
    {
        high = Math.Max(1, high); low = Math.Max(1, low); _length = Math.Max(2, length); _modified = modified;
        var alpha = EhlersFirstOrderCoefficient.Alpha(high * Math.Sqrt(2)); _pole = 1 - alpha; _highGain = Math.Pow(1 - alpha / 2, 2);
        _roofAverage = new(kind, low, external); if (!modified) _finalAverage = new(kind, Math.Max(1, length), external);
        var angle = Math.Sqrt(2) * Math.PI / high; var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(Math.Min(angle, .99)));
        _feedback1 = 2 * radius * cosine; _feedback2 = -radius * radius; _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
    }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal void Add(ref ExactMeanAccumulator sum, BigInteger coefficient)
        { var negative = new ExactMeanAccumulator(); negative.Add(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum, long divisor = 1)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(divisor); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(divisor); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(divisor); }
            return new(value, shift);
        }
    }
    private static double Publish(Scaled value) { var sum = new ExactMeanAccumulator(); value.Add(ref sum); return sum.Mean(1); }
    private static int Compare(Scaled left, Scaled right) { var sum = new ExactMeanAccumulator(); left.Add(ref sum); right.Add(ref sum, -1d); return sum.Sign; }
    private sealed class Average : IDisposable
    {
        private readonly bool _super;
        private readonly int _length;
        private readonly IMovingAverageSmoother? _fallback;
        private readonly BigInteger _gain, _feedback1, _feedback2;
        private readonly Queue<Scaled> _history = new();
        private ExactMeanAccumulator _sum, _weighted;
        private Scaled _previous, _older;
        private int _count;
        internal Average(MovingAvgType kind, int length, bool external)
        {
            _length = length; _super = kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV1;
            if (!Supports(kind) && !external) _fallback = MovingAverageSmootherFactory.Create(kind, length);
            var angle = Math.Sqrt(2) * Math.PI / Math.Max(2, length); var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(angle));
            _feedback1 = 2 * radius * cosine; _feedback2 = -radius * radius; _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
        }
        internal Scaled Next(Scaled input, bool commit)
        {
            if (_fallback is not null) return new(_fallback.Next(Publish(input), commit), 0);
            var sum = _sum; var weighted = _weighted; Scaled result;
            if (_super)
            {
                if (_count < 3) result = input;
                else { var filter = new ExactMeanAccumulator(); input.Add(ref filter, _gain); _previous.Add(ref filter, _feedback1); _older.Add(ref filter, _feedback2); filter.ScaleByPowerOfTwo(-2148); result = Scaled.Round(filter); }
            }
            else
            {
                weighted.Subtract(sum); input.Add(ref weighted, (double)_length); input.Add(ref sum); if (_history.Count == _length) _history.Peek().Add(ref sum, -1d);
                result = Scaled.Round(weighted, (long)_length * (_length + 1L) / 2);
            }
            if (commit)
            {
                _older = _previous; _previous = result; if (_count < 3) _count++;
                if (!_super) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(input); _sum = sum; _weighted = weighted; }
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _previous = _older = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    private Scaled HighPass(double price, bool commit)
    {
        var numerator = new ExactMeanAccumulator(); numerator.Add(price); numerator.Add(_price1, -1); numerator.Add(_price2, -1); numerator.Add(_price3); var drive = Scaled.Round(numerator, 2);
        var firstSum = new ExactMeanAccumulator(); drive.Add(ref firstSum, _highGain); _high1.Add(ref firstSum, _pole); var first = Scaled.Round(firstSum);
        var secondSum = new ExactMeanAccumulator(); first.Add(ref secondSum); _high2.Add(ref secondSum, _pole); var second = Scaled.Round(secondSum);
        if (commit) { _price3 = _price2; _price2 = _price1; _price1 = price; _high1 = first; _high2 = second; }
        return second;
    }
    internal double Prepare(double price, bool commit) => Publish(HighPass(price, commit));
    private double Rank(Scaled value, bool commit)
    {
        var start = _index - _length + 1; var minNode = _minimum.First; var maxNode = _maximum.First;
        while (minNode is not null && minNode.Value.Index < start) minNode = minNode.Next;
        while (maxNode is not null && maxNode.Value.Index < start) maxNode = maxNode.Next;
        var min = minNode is null || Compare(value, minNode.Value.Value) < 0 ? value : minNode.Value.Value;
        var max = maxNode is null || Compare(value, maxNode.Value.Value) > 0 ? value : maxNode.Value.Value;
        var numerator = new ExactMeanAccumulator(); value.Add(ref numerator, _modified ? 100d : 1d); min.Add(ref numerator, _modified ? -100d : -1d);
        var denominator = new ExactMeanAccumulator(); max.Add(ref denominator); min.Add(ref denominator, -1d); var rank = denominator.IsExactlyZero ? 0 : numerator.Ratio(denominator);
        if (commit)
        {
            while (_minimum.First is not null && _minimum.First.Value.Index < start) _minimum.RemoveFirst();
            while (_maximum.First is not null && _maximum.First.Value.Index < start) _maximum.RemoveFirst();
            while (_minimum.Last is not null && Compare(_minimum.Last.Value.Value, value) >= 0) _minimum.RemoveLast();
            while (_maximum.Last is not null && Compare(_maximum.Last.Value.Value, value) <= 0) _maximum.RemoveLast();
            _minimum.AddLast((_index, value)); _maximum.AddLast((_index, value)); _index++;
        }
        return rank;
    }
    internal double Rank(double roof, bool commit) => Rank(new Scaled(roof, 0), commit);
    private Scaled Argument(double rank, bool commit)
    { var sum = new ExactMeanAccumulator(); sum.Add(rank); sum.Add(_rank); var result = Scaled.Round(sum, 2); if (commit) _rank = rank; return result; }
    internal double PrepareFinal(double rank, bool commit) => Publish(Argument(rank, commit));
    internal double Finish(double rank, bool commit)
    {
        var argument = Argument(rank, commit);
        if (!_modified) return Publish(_finalAverage!.Next(argument, commit));
        var sum = new ExactMeanAccumulator(); argument.Add(ref sum, _gain); _output1.Add(ref sum, _feedback1); _output2.Add(ref sum, _feedback2); sum.ScaleByPowerOfTwo(-2148); var result = Scaled.Round(sum);
        if (commit) { _output2 = _output1; _output1 = result; } return Publish(result);
    }
    internal double Next(double price, bool commit) => Finish(Rank(_roofAverage.Next(HighPass(price, commit), commit), commit), commit);
    internal void Reset() { _roofAverage.Reset(); _finalAverage?.Reset(); _minimum.Clear(); _maximum.Clear(); _index = 0; _price1 = _price2 = _price3 = _rank = 0; _high1 = _high2 = _output1 = _output2 = default; }
    public void Dispose() { _roofAverage.Dispose(); _finalAverage?.Dispose(); }
}
