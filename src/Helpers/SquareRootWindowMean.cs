namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class SquareRootWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _denominator;

    internal SquareRootWindowMean(int length)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        _weights = Weights(_values.Capacity, out _denominator);
    }

    private static double[] Weights(int length, out ExactMeanAccumulator denominator)
    {
        var weights = new double[length];
        denominator = default;
        for (var lag = 0; lag < length; lag++)
        {
            weights[lag] = Math.Sqrt(length - lag);
            denominator.Add(weights[lag]);
        }
        return weights;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.Length == 0) return;
        length = Math.Max(1, length);
        var weights = Weights(length, out var denominator);
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ExactMeanAccumulator();
            for (var lag = 0; lag < length && lag <= i; lag++) sum.AddProduct(input[i - lag], weights[lag]);
            output[i] = sum.Ratio(denominator);
        }
    }

    internal double Next(double value, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _weights.Length && lag <= _values.Count; lag++)
            sum.AddProduct(lag == 0 ? value : _values[_values.Count - lag], _weights[lag]);
        var result = sum.Ratio(_denominator);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
