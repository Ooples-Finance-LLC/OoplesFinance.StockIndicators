using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DeviationSuperSmootherWindow
{
    private readonly int _length, _rmsLength;
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _mass;
    private readonly Queue<double> _prices = new();
    private readonly List<Scaled> _momentum = new();
    private readonly Queue<Scaled> _filtered = new();
    private BigInteger _squares;
    private int _grid;
    private double _previousPrice;
    private Scaled _first, _second;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.EhlersHannMovingAverage or MovingAvgType.WeightedMovingAverage;
    internal DeviationSuperSmootherWindow(MovingAvgType kind, int length, int rmsLength)
    {
        _length = Math.Max(1, length); _rmsLength = Math.Max(1, rmsLength);
        var smoothing = (int)Math.Ceiling(_length / 1.4m); _weights = new double[smoothing]; var mass = new ExactMeanAccumulator();
        for (var lag = 0; lag < smoothing; lag++)
        { _weights[lag] = kind == MovingAvgType.EhlersHannMovingAverage ? 1 - Math.Cos(2 * Math.PI * ((lag + 1d) / (smoothing + 1d))) : smoothing - lag; mass.Add(_weights[lag]); }
        _mass = mass;
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

    private static Scaled Ratio(ExactMeanAccumulator sum, ExactMeanAccumulator denominator)
    {
        if (sum.IsExactlyZero) return default;
        var shift = 0; var value = sum.Ratio(denominator);
        while (Math.Abs(value) < Math.Pow(2, -256)) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Ratio(denominator); }
        while (double.IsInfinity(value) || Math.Abs(value) >= Math.Pow(2, 256)) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Ratio(denominator); }
        return new(value, shift);
    }
    private static BigInteger Units(Scaled value, int grid) => value.Mantissa == 0 ? BigInteger.Zero : ExactVarianceWindow.Units(value.Mantissa) << (value.Shift - grid);
    private static Scaled Root(BigInteger squares, int count, int grid)
    {
        if (squares.IsZero) return default;
        var divisor = new BigInteger(count); var shift = 0; var value = ExactPopulationDeviation.RootRatio(squares, divisor);
        while (value < Math.Pow(2, -256)) { squares <<= 1024; shift -= 512; value = ExactPopulationDeviation.RootRatio(squares, divisor); }
        while (double.IsInfinity(value) || value >= Math.Pow(2, 256)) { divisor <<= 1024; shift += 512; value = ExactPopulationDeviation.RootRatio(squares, divisor); }
        return new(value, grid + shift);
    }
    internal double Next(double price, bool commit)
    {
        var difference = new ExactMeanAccumulator(); difference.Add(price); if (_prices.Count == _length) difference.Add(_prices.Peek(), -1); var momentum = Scaled.Round(difference);
        var sum = new ExactMeanAccumulator(); momentum.Add(ref sum, _weights[0]);
        for (var lag = 1; lag < _weights.Length && lag <= _momentum.Count; lag++) _momentum[_momentum.Count - lag].Add(ref sum, _weights[lag]);
        var filtered = Ratio(sum, _mass); var grid = filtered.Mantissa == 0 ? _grid : Math.Min(_grid, filtered.Shift);
        var current = Units(filtered, grid); var squares = (_squares << (2 * (_grid - grid))) + current * current;
        if (_filtered.Count == _rmsLength) { var expired = Units(_filtered.Peek(), grid); squares -= expired * expired; }
        var rms = Root(squares, Math.Min(_filtered.Count + 1, _rmsLength), grid);
        var magnitude = 1d;
        if (rms.Mantissa != 0 && filtered.Mantissa != 0)
        {
            var numerator = new ExactMeanAccumulator(); numerator.Add(Math.Abs(filtered.Mantissa)); numerator.ScaleByPowerOfTwo(filtered.Shift - rms.Shift);
            var denominator = new ExactMeanAccumulator(); denominator.Add(rms.Mantissa); magnitude = numerator.Ratio(denominator);
        }
        if (magnitude == 0) magnitude = 1;
        var angle = Math.Sqrt(2) * Math.PI * magnitude / _length;
        var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(angle));
        var feedback1 = 2 * radius * cosine; var feedback2 = -radius * radius; var gain = (BigInteger.One << 2148) - feedback1 - feedback2;
        var resultSum = new ExactMeanAccumulator(); resultSum.Add(price, gain); resultSum.Add(_previousPrice, gain); _first.Add(ref resultSum, feedback1 << 1); _second.Add(ref resultSum, feedback2 << 1); resultSum.ScaleByPowerOfTwo(-2149); var result = Scaled.Round(resultSum);
        var output = new ExactMeanAccumulator(); result.Add(ref output); var published = output.Mean(1);
        if (commit)
        {
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            if (_momentum.Count == _weights.Length) _momentum.RemoveAt(0); _momentum.Add(momentum);
            if (_filtered.Count == _rmsLength) _filtered.Dequeue(); _filtered.Enqueue(filtered);
            _squares = squares; _grid = grid; _second = _first; _first = result; _previousPrice = price;
        }
        return published;
    }
    internal void Reset() { _prices.Clear(); _momentum.Clear(); _filtered.Clear(); _squares = default; _grid = 0; _first = _second = default; _previousPrice = 0; }
}
