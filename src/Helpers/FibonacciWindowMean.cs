using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class FibonacciWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly BigInteger _newest, _previous;
    private readonly ExactMeanAccumulator _denominator;

    internal FibonacciWindowMean(int length)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        var pair = Pair(_values.Capacity);
        _newest = pair.Current;
        _previous = pair.Next - pair.Current;
        _denominator.Add(1, pair.Current + pair.Next - 1);
    }

    // Doubling identities derive the first descending weights without a floating
    // Binet approximation or an array containing every Fibonacci number.
    private static (BigInteger Current, BigInteger Next) Pair(int index)
    {
        if (index == 0) return (BigInteger.Zero, BigInteger.One);
        var half = Pair(index / 2);
        var even = half.Current * (2 * half.Next - half.Current);
        var odd = half.Current * half.Current + half.Next * half.Next;
        return (index & 1) == 0 ? (even, odd) : (odd, even + odd);
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.Length == 0) return;
        length = Math.Max(1, length);
        var pair = Pair(length);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(1, pair.Current + pair.Next - 1);
        for (var i = 0; i < input.Length; i++)
        {
            var weight = pair.Current;
            var previous = pair.Next - pair.Current;
            var sum = new ExactMeanAccumulator();
            for (var lag = 0; lag < length && lag <= i; lag++)
            {
                sum.Add(input[i - lag], weight);
                (weight, previous) = (previous, weight - previous);
            }
            output[i] = sum.Ratio(denominator);
        }
    }

    internal double Next(double value, bool commit)
    {
        var weight = _newest;
        var previous = _previous;
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _values.Capacity && lag <= _values.Count; lag++)
        {
            sum.Add(lag == 0 ? value : _values[_values.Count - lag], weight);
            (weight, previous) = (previous, weight - previous);
        }
        var result = sum.Ratio(_denominator);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
