using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SmoothedAdaptiveMomentumWindow
{
    private readonly AdaptiveCyberWindow _period;
    private readonly BigInteger _gain, _feedback1, _feedback2, _feedback3;
    private readonly int _length;
    private readonly bool _weightedAverage, _windowAverage;
    private readonly double[] _prices = new double[64];
    private readonly Queue<Scaled> _history = new();
    private int _position, _priceCount, _count;
    private Scaled _first, _second, _third, _average;
    private ExactMeanAccumulator _sum, _weighted;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WeightedMovingAverage;
    internal SmoothedAdaptiveMomentumWindow(int length1, int length2, MovingAvgType kind)
    {
        _period = new(length1, .07); _length = Math.Max(2, length2); _weightedAverage = kind == MovingAvgType.WeightedMovingAverage; _windowAverage = _weightedAverage || kind == MovingAvgType.SimpleMovingAverage;
        var radius = ExactVarianceWindow.Units(Math.Exp(-Math.PI / _length)); var cosine = ExactVarianceWindow.Units(Math.Cos(1.738 * Math.PI / _length));
        var pair = 2 * radius * cosine; var real = radius * radius;
        _feedback1 = (pair + real) << 2148; _feedback2 = -((real << 2148) + pair * real); _feedback3 = real * real;
        _gain = (BigInteger.One << 4296) - _feedback1 - _feedback2 - _feedback3;
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
    internal (double Line, double Signal) Next(double price, bool commit)
    {
        var period = _period.Next(price, commit).Period; var lag = (int)Math.Ceiling(Math.Abs(period - 1));
        // The shared period is below 64, regardless of its median-history length.
        var difference = new ExactMeanAccumulator();
        if (lag > 0 && _priceCount >= lag) { difference.Add(price); difference.Add(_prices[(_position + 64 - lag) % 64], -1); }
        var momentum = Scaled.Round(difference); var filtered = new ExactMeanAccumulator(); momentum.Add(ref filtered, _gain);
        _first.Add(ref filtered, _feedback1); _second.Add(ref filtered, _feedback2); _third.Add(ref filtered, _feedback3); filtered.ScaleByPowerOfTwo(-4296);
        var line = Scaled.Round(filtered); var sum = _sum; var weighted = _weighted; Scaled signal;
        if (_windowAverage)
        {
            weighted.Subtract(sum); line.Add(ref weighted, (double)_length); line.Add(ref sum);
            if (_history.Count == _length) _history.Peek().Add(ref sum, -1d);
            signal = _weightedAverage ? Scaled.Round(weighted, (long)_length * (_length + 1L) / 2) : _count < _length - 1 ? default : Scaled.Round(sum, _length);
        }
        else if (_count < _length) { line.Add(ref sum); signal = Scaled.Round(sum, _count + 1L); }
        else
        {
            var average = new ExactMeanAccumulator(); _average.Add(ref average, _length - 1d); line.Add(ref average, 2d); signal = Scaled.Round(average, _length + 1L);
        }
        if (commit)
        {
            _prices[_position] = price; _position = (_position + 1) % 64; if (_priceCount < 64) _priceCount++;
            _third = _second; _second = _first; _first = line; _average = signal; _sum = sum; _weighted = weighted; if (_count < _length) _count++;
            if (_windowAverage) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(line); }
        }
        return (Publish(line), Publish(signal));
    }
    internal void Reset() { _period.Reset(); Array.Clear(_prices, 0, _prices.Length); _history.Clear(); _position = _priceCount = _count = 0; _first = _second = _third = _average = default; _sum = _weighted = default; }
}
