namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HannWindowMean : IDisposable
{
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _denominator;
    private readonly PooledRingBuffer<double> _values;

    internal HannWindowMean(int length)
    {
        length = Math.Max(1, length);
        _weights = new double[length];
        var denominator = new ExactMeanAccumulator();
        for (var lag = 0; lag < length; lag++)
        {
            _weights[lag] = 1 - Math.Cos(2 * Math.PI * ((lag + 1d) / (length + 1d)));
            denominator.Add(_weights[lag]);
        }
        _denominator = denominator;
        _values = new PooledRingBuffer<double>(length);
    }

    internal double Next(double value, bool commit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _weights.Length && lag <= _values.Count; lag++)
            sum.AddProduct(lag == 0 ? value : _values[_values.Count - lag], _weights[lag]);
        var result = sum.Ratio(_denominator);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var mean = new HannWindowMean(length);
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
