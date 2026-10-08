namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class FareyWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _denominator;

    internal FareyWindowMean(int length)
    {
        _weights = Weights(Math.Max(1, length), out _denominator);
        _values = new PooledRingBuffer<double>(_weights.Length);
    }

    private static double[] Weights(int length, out ExactMeanAccumulator denominator)
    {
        var ascending = new List<double>();
        long a = 0, b = 1, c = 1, d = length;
        while (c <= length)
        {
            ascending.Add(Math.Round((double)c / d, 3));
            var k = (length + b) / d;
            var nextNumerator = k * c - a;
            var nextDenominator = k * d - b;
            a = c; b = d; c = nextNumerator; d = nextDenominator;
        }
        ascending.Reverse();
        var weights = ascending.ToArray();
        denominator = default;
        foreach (var weight in weights) denominator.Add(weight);
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
            for (var lag = 0; lag < weights.Length && lag <= i; lag++) sum.AddProduct(input[i - lag], weights[lag]);
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
