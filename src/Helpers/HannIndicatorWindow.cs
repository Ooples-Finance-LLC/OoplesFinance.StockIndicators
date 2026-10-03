namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HannIndicatorWindow : IDisposable
{
    private readonly TriangleIndicatorWindow _stages;
    private readonly double[]? _weights;
    private readonly ExactMeanAccumulator _mass;
    private readonly Queue<RocBankValue> _values = new();
    internal static bool Supports(MovingAvgType kind) => kind == MovingAvgType.EhlersHannMovingAverage || StrengthWindow.Supports(kind);
    internal HannIndicatorWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        _stages = new(kind == MovingAvgType.EhlersHannMovingAverage ? MovingAvgType.SimpleMovingAverage : kind, length);
        if (kind != MovingAvgType.EhlersHannMovingAverage) return;
        _weights = new double[length]; var mass = new ExactMeanAccumulator();
        for (var lag = 0; lag < length; lag++)
        { _weights[lag] = 1 - Math.Cos(2 * Math.PI * ((lag + 1d) / (length + 1d))); mass.Add(_weights[lag]); }
        _mass = mass;
    }
    internal (double Line, double Roc) Finish(RocBankValue line, bool commit) => _stages.Finish(line, commit);
    internal (double Line, double Roc) Next(double open, double close, bool commit)
    {
        if (_weights is null) return _stages.Next(open, close, commit);
        var value = TriangleIndicatorWindow.Difference(close, open);
        var sum = new ExactMeanAccumulator(); Add(ref sum, value, _weights[0]);
        var lag = _values.Count;
        foreach (var previous in _values) { if (lag < _weights.Length) Add(ref sum, previous, _weights[lag]); lag--; }
        RocBankValue line;
        for (var shift = 0; ; shift += 1024)
        {
            var denominator = _mass; denominator.ScaleByPowerOfTwo(shift);
            var rounded = sum.Ratio(denominator);
            if (!double.IsInfinity(rounded)) { line = new(rounded, shift); break; }
        }
        if (commit) { if (_values.Count == _weights.Length) _values.Dequeue(); _values.Enqueue(value); }
        return Finish(line, commit);
    }
    private static void Add(ref ExactMeanAccumulator sum, RocBankValue value, double weight)
    { var product = new ExactMeanAccumulator(); product.AddProduct(value.Mantissa, weight, -1); product.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(product); }
    internal void Reset() { _values.Clear(); _stages.Reset(); }
    public void Dispose() { _values.Clear(); _stages.Dispose(); }
}
