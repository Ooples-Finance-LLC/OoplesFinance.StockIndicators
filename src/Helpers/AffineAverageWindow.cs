using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Signed affine FIR sums stay exact until the published result is rounded.
// The divisor includes the zero-padded startup portion of the window.
internal sealed class AffineAverageWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly long _firstWeight, _weightStep;
    private readonly BigInteger _divisor;
    private readonly bool _sharp, _exactSimple;
    private readonly int _length;

    internal AffineAverageWindow(int length, int offset = 4, bool sharp = false, int capacityHint = int.MaxValue, bool exactSimple = false)
    {
        length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(Math.Min(length, Math.Max(1, capacityHint)));
        _sharp = sharp;
        _exactSimple = exactSimple;
        _length = length;
        _firstWeight = sharp ? 3L * (length - 1L) : (long)length - offset;
        _weightStep = sharp ? -6 : -1;
        var n = new BigInteger(length);
        _divisor = sharp ? n * (n + 1) : n * (n + 1 - 2 * new BigInteger(offset)) / 2;
    }

    internal double Next(double value, double average = 0, bool isFinal = true)
    {
        var sum = new ExactMeanAccumulator();
        if (_sharp && _exactSimple)
        {
            var mean = new ExactMeanAccumulator();
            mean.Add(value);
            for (var lag = 1; lag <= Math.Min(_values.Count, _values.Capacity - 1); lag++)
                mean.Add(_values[_values.Count - lag]);
            average = _values.Count >= _length - 1 ? mean.Mean(_length) : 0;
        }
        if (_sharp) sum.Add(average, _divisor);
        var count = Math.Min(_values.Count, _values.Capacity - 1);
        for (var lag = 0; lag <= count; lag++)
        {
            var price = lag == 0 ? value : _values[_values.Count - lag];
            sum.Add(price, new BigInteger(_firstWeight + _weightStep * lag));
        }
        var denominator = new ExactMeanAccumulator();
        denominator.Add(1, _divisor);
        var result = sum.Ratio(denominator);
        if (isFinal) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
