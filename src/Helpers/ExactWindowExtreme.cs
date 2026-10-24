using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact rolling minimum/maximum with non-mutating preview and reusable candidate storage.
internal sealed class ExactWindowExtreme
{
    private readonly int _length;
    private readonly bool _maximum;
    private (long Index, BigInteger Value)[] _candidates = Array.Empty<(long, BigInteger)>();
    private int _head, _count;
    private long _index;

    internal ExactWindowExtreme(int period, bool maximum)
    {
        _length = Math.Max(1, period);
        _maximum = maximum;
    }

    internal BigInteger Preview(BigInteger value)
    {
        var first = 0;
        while (first < _count && _candidates[Slot(first)].Index <= _index - _length) first++;
        if (first == _count) return value;
        var retained = _candidates[Slot(first)].Value;
        return _maximum ? BigInteger.Max(value, retained) : BigInteger.Min(value, retained);
    }

    private int Slot(int offset) => (int)(((long)_head + offset) % _candidates.Length);

    // Ordered candidates retain only values that can become a future extreme.
    // Storage grows with observations and is reused; previews never mutate it.
    internal void Commit(BigInteger value)
    {
        while (_count > 0 && _candidates[_head].Index <= _index - _length)
        {
            _candidates[_head] = default;
            _head = (_head + 1) % _candidates.Length;
            _count--;
        }
        while (_count > 0 && (_maximum ? _candidates[Slot(_count - 1)].Value <= value : _candidates[Slot(_count - 1)].Value >= value))
        {
            _candidates[Slot(_count - 1)] = default;
            _count--;
        }
        if (_count == _candidates.Length)
        {
            var capacity = (int)Math.Min(_length, Math.Max(4L, 2L * _candidates.Length));
            var grown = new (long Index, BigInteger Value)[capacity];
            for (var i = 0; i < _count; i++) grown[i] = _candidates[Slot(i)];
            _candidates = grown;
            _head = 0;
        }
        _candidates[Slot(_count++)] = (_index++, value);
    }

    internal void Reset()
    {
        Array.Clear(_candidates, 0, _candidates.Length);
        _head = _count = 0;
        _index = 0;
    }
}
