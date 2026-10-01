using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PolarizedEfficiencyWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private readonly Queue<BigInteger> _steps = new();
    private readonly Mean _average;
    private BigInteger _path;
    private double _previousPrice;
    internal PolarizedEfficiencyWindow(MovingAvgType kind, int length, int smooth)
    { _length = Math.Max(1, length); _average = new(kind, Math.Max(1, smooth)); }
    // Correctly rounded norm with one extra exponent bit for a difference of
    // finite prices. Squares and cancellation are exact before the square root.
    internal static BigInteger Distance(double current, double previous, int horizontal)
    {
        var square = new ExactMeanAccumulator();
        square.AddProduct(current, current); square.AddProduct(previous, previous);
        square.AddProduct(current, previous, -2); square.AddProduct(horizontal, horizontal);
        var root = square.SqrtMean(1);
        if (!double.IsInfinity(root)) return ExactVarianceWindow.Units(root);
        square.ScaleByPowerOfTwo(-2);
        return ExactVarianceWindow.Units(square.SqrtMean(1)) << 1;
    }
    internal double Raw(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var full = _prices.Count == _length;
        var step = Distance(price, _prices.Count == 0 ? price : _previousPrice, 1);
        var path = _path + step - (_steps.Count == _length ? _steps.Peek() : BigInteger.Zero);
        var value = 0d;
        if (full)
        {
            var lagged = _prices.Peek(); var direction = price.CompareTo(lagged);
            var direct = Distance(price, lagged, _length);
            var ratio = ExactMeanAccumulator.UnitRatio((100 * direct) << 1074, path);
            // Independent rounding of the norms may exceed the geometric bound
            // by an ulp. Preserve the triangle-inequality bound at publication.
            value = direction * Math.Min(100, ratio);
        }
        if (final)
        {
            if (full) _prices.Dequeue(); _prices.Enqueue(price);
            if (_steps.Count == _length) _steps.Dequeue(); _steps.Enqueue(step);
            _path = path; _previousPrice = price;
        }
        return value;
    }
    internal double Next(double price, bool final) => _average.Next(Raw(price, final), final);
    internal static double[] Calculate(StockData data, List<double> prices, MovingAvgType kind, int length, int smooth, bool callbacks)
    {
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var caller = data.CaptureInputSeries();
        try
        {
            using var window = new PolarizedEfficiencyWindow(kind, length, smooth);
            var raw = prices.Select(p => window.Raw(p, true)).ToArray(); smooth = Math.Max(1, smooth);
            var custom = callbacks ? ComponentAverage.Take(raw, smooth) : null;
            if (custom is not null) return custom.ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, smooth, raw.ToList()).ToArray();
            return raw.Select(v => window._average.Next(v, true)).ToArray();
        }
        finally { data.RestoreInputSeries(caller); }
    }
    private sealed class Mean : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<double> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private ExactMeanAccumulator _sum, _weighted; private double _previous; private long _count;
        internal Mean(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal double Next(double value, bool final)
        {
            if (_fallback is not null) return _fallback.Next(value, final);
            var sum = _sum; var weighted = _weighted; double result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                weighted.Subtract(sum); weighted.Add(value, _length);
                if (_history.Count == _length) sum.Add(_history.Peek(), -1); sum.Add(value);
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? 0 : sum.Mean(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum.Add(value); result = sum.Mean(_count + 1); }
            else
            {
                var next = new ExactMeanAccumulator(); next.Add(_previous, _length - 1);
                next.Add(value, _kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1);
                result = next.Mean(_kind == MovingAvgType.ExponentialMovingAverage ? _length + 1L : _length);
            }
            if (final)
            {
                if (finite) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _previous = 0; _count = 0; _fallback?.Reset(); }
        public void Dispose() { _fallback?.Dispose(); _history.Clear(); }
    }
    internal void Reset() { _prices.Clear(); _steps.Clear(); _average.Reset(); _path = BigInteger.Zero; _previousPrice = 0; }
    public void Dispose() { _prices.Clear(); _steps.Clear(); _average.Dispose(); }
}
