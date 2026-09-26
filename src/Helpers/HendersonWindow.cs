namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HendersonWindow : IDisposable
{
    private readonly System.Numerics.BigInteger[] _weights;
    private readonly ExactMeanAccumulator _denominator;
    private readonly PooledRingBuffer<double> _values;

    internal HendersonWindow(int length)
    {
        length = Math.Max(1, length);
        _weights = new System.Numerics.BigInteger[length];
        var m = new System.Numerics.BigInteger(Math.Max(2, Math.Min(530, (length - 1) / 2)));
        var denominator = new ExactMeanAccumulator();
        for (var lag = 0; lag < length; lag++)
        {
            var n = lag - m; var square = n * n;
            _weights[lag] = ((m + 1) * (m + 1) - square) * ((m + 2) * (m + 2) - square)
                * ((m + 3) * (m + 3) - square) * (3 * (m + 2) * (m + 2) - 11 * square - 16);
            denominator.Add(1, _weights[lag]);
        }
        _denominator = denominator;
        _values = new PooledRingBuffer<double>(length);
    }

    internal double Next(double value, bool commit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _weights.Length && lag <= _values.Count; lag++)
            sum.Add(lag == 0 ? value : _values[_values.Count - lag], _weights[lag]);
        var result = _denominator.IsExactlyZero ? 0 : sum.Ratio(_denominator);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var mean = new HendersonWindow(length);
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
