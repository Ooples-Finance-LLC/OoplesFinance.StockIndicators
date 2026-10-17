using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class FibonacciWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly BigInteger _newest, _previous;
    private readonly ExactMeanAccumulator _denominator;
    private readonly int _length;
    private ExactMeanAccumulator _weighted, _previousWeighted;

    internal FibonacciWindowMean(int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(Math.Min(_length, Math.Max(1, capacityHint)));
        var pair = Pair(_length);
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
        // A uses F(L)..F(1); B uses F(L-1)..F(0). Shifting the window
        // gives A' = B + F(L)*value and B' = A-B + F(L-1)*value-expired.
        // Both sums remain exact; only the published quotient is rounded.
        var weighted = _previousWeighted;
        weighted.Add(value, _newest);
        var previousWeighted = _weighted;
        previousWeighted.Subtract(_previousWeighted);
        previousWeighted.Add(value, _previous);
        if (_values.Count == _length) previousWeighted.Add(_values[0], -1);
        var result = weighted.Ratio(_denominator);
        if (commit)
        {
            _weighted = weighted;
            _previousWeighted = previousWeighted;
            _values.TryAdd(value, out _);
        }
        return result;
    }

    internal void Reset() { _values.Clear(); _weighted = _previousWeighted = default; }
    public void Dispose() => _values.Dispose();
}
