using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Zero-padded integer-power weights, normalized before the only binary64 rounding.
internal sealed class IntegerPowerWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly int _power;
    private readonly ExactMeanAccumulator _denominator;

    internal IntegerPowerWindowMean(int length, int power)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        _power = power;
        _denominator = Denominator(_values.Capacity, power);
    }

    private static ExactMeanAccumulator Denominator(int length, int power)
    {
        var n = new BigInteger(length);
        var weight = power == 2 ? n * (n + 1) * (2 * n + 1) / 6 : BigInteger.Pow(n * (n + 1) / 2, 2);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(1, weight);
        return denominator;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, int power)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var denominator = Denominator(length, power);
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ExactMeanAccumulator();
            for (var lag = 0; lag < length && lag <= i; lag++)
                sum.Add(input[i - lag], BigInteger.Pow(new BigInteger(length - lag), power));
            output[i] = sum.Ratio(denominator);
        }
    }

    internal double Next(double value, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _values.Capacity && lag <= _values.Count; lag++)
            sum.Add(lag == 0 ? value : _values[_values.Count - lag], BigInteger.Pow(new BigInteger(_values.Capacity - lag), _power));
        var result = sum.Ratio(_denominator);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
