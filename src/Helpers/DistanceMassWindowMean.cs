using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Public InverseDistanceWeightedMovingAverage semantics: weights are distance
// sums, not reciprocals. Integer-grid distances cancel out of the final ratio.
internal sealed class DistanceMassWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly bool _reciprocal;
    internal DistanceMassWindowMean(int length, bool reciprocal = false)
    {
        _values = new PooledRingBuffer<double>(Math.Max(1, length));
        _reciprocal = reciprocal;
    }

    private static BigInteger Units(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047) throw new ArgumentOutOfRangeException(nameof(value));
        var mantissa = (bits & 0x000fffffffffffffL) + (exponent == 0 ? 0 : 1L << 52);
        var units = new BigInteger(mantissa) << Math.Max(0, exponent - 1);
        return bits < 0 ? -units : units;
    }

    private static double Evaluate(List<double> present, int length, double current, bool reciprocal)
    {
        var entries = present.Select(value => (Value: value, Units: Units(value), Count: 1L)).ToList();
        if (present.Count < length) entries.Add((0d, BigInteger.Zero, (long)length - present.Count));
        entries.Sort((left, right) => left.Value.CompareTo(right.Value));
        var total = BigInteger.Zero;
        foreach (var entry in entries) total += entry.Units * entry.Count;
        var prefix = BigInteger.Zero;
        var mass = BigInteger.Zero;
        long rank = 0;
        var numerator = new ExactMeanAccumulator();
        var inverseEntries = reciprocal ? new List<(double Value, BigInteger Distance, int Count)>() : null;
        foreach (var entry in entries)
        {
            var weight = entry.Units * (2 * rank - length) + total - 2 * prefix;
            var repeatedWeight = weight * entry.Count;
            if (inverseEntries is null) numerator.Add(entry.Value, repeatedWeight);
            else inverseEntries.Add((entry.Value, weight, (int)entry.Count));
            mass += repeatedWeight;
            prefix += entry.Units * entry.Count;
            rank += entry.Count;
        }
        if (mass.IsZero) return current;
        var denominator = new ExactMeanAccumulator();
        if (inverseEntries is null) denominator.Add(1d, mass);
        else
        {
            var minimum = inverseEntries.Min(entry => entry.Distance);
            var scale = new ExactMeanAccumulator();
            scale.Add(1d, minimum);
            foreach (var entry in inverseEntries)
            {
                var distance = new ExactMeanAccumulator();
                distance.Add(1d, entry.Distance);
                var weight = scale.Ratio(distance);
                numerator.AddProduct(entry.Value, weight, entry.Count);
                denominator.Add(weight, entry.Count);
            }
        }
        return numerator.Ratio(denominator);
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, bool reciprocal = false)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        for (var i = 0; i < input.Length; i++)
        {
            var present = new List<double>();
            for (var lag = 0; lag < length && lag <= i; lag++) present.Add(input[i - lag]);
            output[i] = Evaluate(present, length, input[i], reciprocal);
        }
    }

    internal double Next(double value, bool commit)
    {
        var present = new List<double> { value };
        for (var lag = 1; lag < _values.Capacity && lag <= _values.Count; lag++) present.Add(_values[_values.Count - lag]);
        var result = Evaluate(present, _values.Capacity, value, _reciprocal);
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
