namespace OoplesFinance.StockIndicators.Helpers;

// Zero-padded sine-pedestal window with exact binary64 coefficient products.
// The final quotient may overflow for signed coefficient choices.
internal sealed class HammingWindowMean : IDisposable
{
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _denominator;
    private readonly PooledRingBuffer<double> _values;

    internal HammingWindowMean(int length, double pedestal)
    {
        if (double.IsNaN(pedestal) || double.IsInfinity(pedestal)) throw new ArgumentOutOfRangeException(nameof(pedestal));
        length = Math.Max(1, length);
        _weights = new double[length];
        var denominator = new ExactMeanAccumulator();
        for (var lag = 0; lag < length; lag++)
        {
            var position = length == 1 ? 0 : lag / (length - 1d);
            // Interpolate the phase without forming 2*pedestal, which can
            // overflow even when every interpolated phase is representable.
            var phase = pedestal * (1 - 2 * position) + Math.PI * position;
            var weight = length == 1 ? 1 : Math.Sin(phase);
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

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, double pedestal)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var mean = new HammingWindowMean(length, pedestal);
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
