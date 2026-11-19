using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class QuickWindowMean : IDisposable
{
    private readonly int _length;
    private readonly int _peak, _newWeight;
    private readonly PooledRingBuffer<double> _values;
    private readonly ExactMeanAccumulator _denominator;
    private ExactMeanAccumulator _sum, _recent, _weighted;

    internal QuickWindowMean(int length, int capacityHint = int.MaxValue)
    {
        // Periods one and two both have the historical weights [1, 2].
        _length = Math.Max(2, length);
        _peak = (int)Peak(_length);
        _newWeight = _length - _peak + 1;
        _values = new PooledRingBuffer<double>(Math.Min(_length, Math.Max(1, capacityHint)));
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
        // Advancing the tent adds L+1 times the newest P-1 observations and
        // subtracts P times the complete prior window. All stages stay exact.
        var weighted = _weighted;
        weighted.Add(value, _newWeight);
        var recent = _recent; recent.Multiply(_length + 1d); weighted.AddExact(recent);
        var total = _sum; total.Multiply(_peak); weighted.Subtract(total);
        var result = weighted.Ratio(_denominator);
        if (commit)
        {
            if (_values.Count == _length) _sum.Add(_values[0], -1);
            if (_values.Count >= _peak - 1) _recent.Add(_values[_values.Count - (_peak - 1)], -1);
            _sum.Add(value); _recent.Add(value); _weighted = weighted;
            _values.TryAdd(value, out _);
        }
        return result;
    }

    internal void Reset() { _values.Clear(); _sum = _recent = _weighted = default; }
    public void Dispose() => _values.Dispose();
}
