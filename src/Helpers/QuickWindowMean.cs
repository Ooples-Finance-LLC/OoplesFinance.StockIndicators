using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class QuickWindowMean : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly ExactMeanAccumulator _denominator;

    internal QuickWindowMean(int length)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(Math.Max(2, _length));
        _denominator = Denominator(_length);
    }

    private static long Peak(int length) => Math.Max(2, Math.Min(530, (length + 2L) / 3));

    private static long Weight(int length, int lag)
    {
        // At period one the historical minimum peak of two leaves two nonzero taps.
        if (length == 1) return lag + 1;
        var peak = Peak(length);
        return lag < peak ? (lag + 1L) * (length + 1L - peak) : (length - (long)lag) * peak;
    }

    private static ExactMeanAccumulator Denominator(int length)
    {
        var peak = Peak(length);
        var weight = length == 1 ? new BigInteger(3) : new BigInteger(peak) * (length + 1L - peak) * (length + 1L) / 2;
        var denominator = new ExactMeanAccumulator();
        denominator.Add(1, weight);
        return denominator;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var denominator = Denominator(length);
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ExactMeanAccumulator();
            for (var lag = 0; lag < Math.Max(2, length) && lag <= i; lag++)
                sum.Add(input[i - lag], new BigInteger(Weight(length, lag)));
            output[i] = sum.Ratio(denominator);
        }
    }

    internal double Next(double value, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _values.Capacity && lag <= _values.Count; lag++)
            sum.Add(lag == 0 ? value : _values[_values.Count - lag], new BigInteger(Weight(_length, lag)));
        var result = sum.Ratio(_denominator);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
