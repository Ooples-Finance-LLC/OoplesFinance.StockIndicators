namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DynamicFilterWindow
{
    private readonly int _length;
    private readonly Queue<RocBankValue> _sources = new(), _squares = new();
    private ExactMeanAccumulator _sourceSum, _squareSum;
    private RocBankValue _previous; private double _gain; private bool _started;
    internal DynamicFilterWindow(int length) => _length = Math.Max(1, length);
    private static RocBankValue Difference(RocBankValue left, RocBankValue right)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, -1); return RocBankValue.Round(sum); }
    private static RocBankValue Add(RocBankValue left, RocBankValue right)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum); return RocBankValue.Round(sum); }
    internal double Next(double value, bool commit)
    {
        var current = new RocBankValue(value); var previous = _started ? _previous : current;
        var source = Add(current, Difference(current, previous));
        var line = Add(previous, Difference(source, previous).Multiply(_gain));
        var sum = _sourceSum; source.AddTo(ref sum);
        if (_sources.Count == _length) _sources.Peek().AddTo(ref sum, -1);
        var count = Math.Min(_sources.Count + 1L, _length);
        var mean = RocBankValue.Round(sum, count: count); var error = Difference(source, mean);
        // Keep squared deviations in epsilon-squared units until the square root.
        var squareSum = new ExactMeanAccumulator(); squareSum.AddProduct(error.Mantissa, error.Mantissa);
        squareSum.ScaleByPowerOfTwo(2 * error.UpperShift + 2148);
        var square = RocBankValue.Round(squareSum); var energy = _squareSum; square.AddTo(ref energy);
        if (_squares.Count == _length) _squares.Peek().AddTo(ref energy, -1);
        var variance = RocBankValue.Round(energy, count: count);
        var rootSum = new ExactMeanAccumulator(); variance.AddTo(ref rootSum); rootSum.ScaleByPowerOfTwo(-2148);
        RocBankValue root;
        for (var shift = 0; ; shift += 1024)
        {
            var result = rootSum.SqrtMean(1);
            if (!double.IsInfinity(result)) { root = new(result, shift); break; }
            rootSum.ScaleByPowerOfTwo(-2048);
        }
        var difference = Difference(source, line); var magnitude = new RocBankValue(Math.Abs(difference.Mantissa), difference.UpperShift);
        var denominator = Add(magnitude, root.Multiply(_length));
        var numeratorSum = new ExactMeanAccumulator(); magnitude.AddTo(ref numeratorSum);
        var denominatorSum = new ExactMeanAccumulator(); denominator.AddTo(ref denominatorSum);
        var gain = magnitude.Mantissa == 0 ? 0 : numeratorSum.Ratio(denominatorSum);
        if (commit)
        {
            _sourceSum = sum; _squareSum = energy; _previous = line; _gain = gain; _started = true;
            if (_sources.Count == _length) { _sources.Dequeue(); _squares.Dequeue(); }
            _sources.Enqueue(source); _squares.Enqueue(square);
        }
        return line.Publish();
    }
    internal void Reset() { _sources.Clear(); _squares.Clear(); _sourceSum = _squareSum = default; _previous = default; _gain = 0; _started = false; }
}
