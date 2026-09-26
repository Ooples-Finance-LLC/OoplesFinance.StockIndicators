namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RollingRootMeanSquare : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private ExactMeanAccumulator _squares;

    internal RollingRootMeanSquare(int length) => _values = new PooledRingBuffer<double>(Math.Max(1, length));

    internal double Next(double value, bool commit)
    {
        var squares = _squares;
        var full = _values.Count == _values.Capacity;
        if (full) squares.AddProduct(_values[0], _values[0], -1);
        squares.AddProduct(value, value);
        var result = squares.SqrtMean(full ? _values.Capacity : _values.Count + 1);
        if (commit) { _squares = squares; _values.TryAdd(value, out _); }
        return result;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var squares = new ExactMeanAccumulator();
        for (var i = 0; i < input.Length; i++)
        {
            if (i >= length) squares.AddProduct(input[i - length], input[i - length], -1);
            squares.AddProduct(input[i], input[i]);
            output[i] = squares.SqrtMean(Math.Min(i + 1, length));
        }
    }

    internal void Reset() { _values.Clear(); _squares = default; }
    public void Dispose() => _values.Dispose();
}
