using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DeviationScaledWindow
{
    private readonly int _slow;
    private readonly BigInteger _gain, _feedback1, _feedback2;
    private readonly Queue<Scaled> _samples = new();
    private BigInteger _sum, _squares;
    private int _grid;
    private double _price1, _price2;
    private int _count;
    private Scaled _change, _drive, _first, _second, _ratio, _average;
    internal static int ResolveSlow(int length) => checked(2 * Math.Max(1, length));
    internal DeviationScaledWindow(int fast, int slow)
    {
        _slow = Math.Max(1, slow); fast = Math.Max(2, fast);
        var angle = Math.Sqrt(2) * Math.PI / fast;
        var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(angle));
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
        internal static Scaled Round(ExactMeanAccumulator sum)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(1); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(1); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(1); }
            return new(value, shift);
        }
    }
    private static BigInteger Units(Scaled value, int grid) => value.Mantissa == 0 ? BigInteger.Zero : ExactVarianceWindow.Units(value.Mantissa) << (value.Shift - grid);
    private static Scaled Root(BigInteger radicand, BigInteger divisor, int grid)
    {
        if (radicand.IsZero) return default;
        var shift = 0; var value = ExactPopulationDeviation.RootRatio(radicand, divisor); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
        while (value < lower) { radicand <<= 1024; shift -= 512; value = ExactPopulationDeviation.RootRatio(radicand, divisor); }
        while (double.IsInfinity(value) || value >= upper) { divisor <<= 1024; shift += 512; value = ExactPopulationDeviation.RootRatio(radicand, divisor); }
        return new(value, grid + shift);
    }
    private static Scaled Divide(Scaled numerator, Scaled denominator)
    {
        var top = new ExactMeanAccumulator(); top.Add(numerator.Mantissa); var bottom = new ExactMeanAccumulator(); bottom.Add(denominator.Mantissa);
        var value = new ExactMeanAccumulator(); value.Add(top.Ratio(bottom)); value.ScaleByPowerOfTwo(numerator.Shift - denominator.Shift); return Scaled.Round(value);
    }
    internal double Next(double price, bool commit, out double scaledFilter)
    {
        var difference = new ExactMeanAccumulator(); if (_count >= 2) { difference.Add(price); difference.Add(_price2, -1); } var change = Scaled.Round(difference);
        var averaged = new ExactMeanAccumulator(); change.Add(ref averaged); _change.Add(ref averaged); averaged.ScaleByPowerOfTwo(-1); var drive = Scaled.Round(averaged);
        var filtered = new ExactMeanAccumulator(); drive.Add(ref filtered, _gain); _drive.Add(ref filtered, _gain); _first.Add(ref filtered, _feedback1 << 1); _second.Add(ref filtered, _feedback2 << 1); filtered.ScaleByPowerOfTwo(-2149); var filter = Scaled.Round(filtered);
        var grid = filter.Mantissa == 0 ? _grid : Math.Min(_grid, filter.Shift);
        var sum = _sum << (_grid - grid); var squares = _squares << (2 * (_grid - grid)); var current = Units(filter, grid);
        sum += current; squares += current * current;
        if (_samples.Count == _slow) { var expired = Units(_samples.Peek(), grid); sum -= expired; squares -= expired * expired; }
        var ratio = _ratio;
        if (_samples.Count >= _slow - 1)
        {
            var radicand = _slow * squares - sum * sum;
            if (!radicand.IsZero) ratio = Divide(filter, Root(radicand, new BigInteger(_slow) * _slow, grid));
        }
        var ratioValue = new ExactMeanAccumulator(); ratio.Add(ref ratioValue); scaledFilter = ratioValue.Mean(1);
        var gainValue = new ExactMeanAccumulator(); new Scaled(Math.Abs(ratio.Mantissa), ratio.Shift).Add(ref gainValue, 5d);
        var gain = Math.Max(.01, Math.Min(.99, gainValue.Mean(_slow)));
        var averageSum = new ExactMeanAccumulator(); _average.Add(ref averageSum); averageSum.AddProduct(price, gain); _average.Add(ref averageSum, -gain); var average = Scaled.Round(averageSum);
        var output = new ExactMeanAccumulator(); average.Add(ref output); var result = output.Mean(1);
        if (commit)
        {
            if (_samples.Count == _slow) _samples.Dequeue(); _samples.Enqueue(filter); _sum = sum; _squares = squares; _grid = grid;
            _price2 = _price1; _price1 = price; if (_count < 2) _count++; _change = change; _drive = drive; _second = _first; _first = filter; _ratio = ratio; _average = average;
        }
        return result;
    }
    internal void Reset() { _samples.Clear(); _sum = _squares = default; _grid = _count = 0; _price1 = _price2 = 0; _change = _drive = _first = _second = _ratio = _average = default; }
}
