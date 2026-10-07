using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
// The binomial form of (1 - lag)^order, with zero prehistory and one final rounding.
internal sealed class NthDifferenceWindow : IDisposable
{
    private readonly int _lag, _order;
    private readonly PooledRingBuffer<double> _values;
    internal NthDifferenceWindow(int lag, int order, int capacityHint = int.MaxValue)
    {
        _lag = Math.Max(1, lag); _order = Math.Max(0, order);
        _values = new((int)Math.Max(1, Math.Min((long)_lag * _order, capacityHint)));
    }
    internal double Next(double value, bool commit)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(value); var coefficient = BigInteger.One;
        for (var j = 1; j <= Math.Min(_order, _values.Count / _lag); j++)
        {
            coefficient = coefficient * (_order - j + 1L) / j;
            sum.Add(_values[_values.Count - _lag * j], j % 2 == 0 ? coefficient : -coefficient);
        }
        var result = sum.Mean(1); if (commit) _values.TryAdd(value, out _); return result;
    }
    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
