namespace OoplesFinance.StockIndicators.Helpers;

// Zero-padded Gaussian window. Binary64 coefficients are accumulated exactly;
// only the final quotient is rounded, so a convex mean cannot overflow.
internal sealed class AlmaWindowMean : IDisposable
{
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _denominator;
    private readonly PooledRingBuffer<double> _values;

    internal AlmaWindowMean(int length, double offset, double sigma)
    {
        if (double.IsNaN(offset) || double.IsInfinity(offset)) throw new ArgumentOutOfRangeException(nameof(offset));
        if (double.IsNaN(sigma) || double.IsInfinity(sigma)) throw new ArgumentOutOfRangeException(nameof(sigma));
        length = Math.Max(1, length);
        _weights = new double[length];
        var denominator = new ExactMeanAccumulator();
        for (var lag = 0; lag < length; lag++)
        {
            // Scale each location before multiplying by sigma. This avoids an
            // overflowing offset*(length-1) or a squared window width.
            var distance = ((length - 1d - lag) / length - offset * ((length - 1d) / length)) * sigma;
            var weight = Math.Exp(-0.5 * distance * distance);
            _weights[lag] = weight;
            denominator.Add(weight);
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

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, double offset, double sigma)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var mean = new AlmaWindowMean(length, offset, sigma);
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
