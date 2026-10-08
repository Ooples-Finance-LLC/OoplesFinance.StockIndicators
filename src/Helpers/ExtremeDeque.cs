namespace OoplesFinance.StockIndicators.Helpers;

// Bounded monotonic deque. Equal candidates are retained: callers can distinguish
// a unique extremum from a tied one without rounding price differences.
internal sealed class ExtremeDeque(int capacity, bool maximum)
{
    private readonly (long Index, double Value)[] _items = new (long, double)[capacity];
    private int _head, _count;
    private int Slot(int offset)
    {
        var untilWrap = _items.Length - _head;
        return offset >= untilWrap ? offset - untilWrap : _head + offset;
    }
    internal double Value => _items[_head].Value;
    internal void Reset() { _head = _count = 0; }
    internal double NextValue(double value, long first)
    {
        for (var i = 0; i < _count; i++)
        {
            var item = _items[Slot(i)];
            if (item.Index < first) continue;
            return maximum ? Math.Max(value, item.Value) : Math.Min(value, item.Value);
        }
        return value;
    }
    internal double? NextUniqueAt(long index, double value, long first, long center)
    {
        for (var i = 0; i < _count; i++)
        {
            var head = _items[Slot(i)];
            if (head.Index < first) continue;
            if (maximum ? value > head.Value : value < head.Value)
                return index == center ? value : null;
            if (value == head.Value || head.Index != center) return null; // NOSONAR: strict extrema exclude exact ties.
            return i + 1 < _count && _items[Slot(i + 1)].Value == head.Value // NOSONAR: exact ties.
                ? null : head.Value;
        }
        return index == center ? value : null;
    }

    internal void Add(long index, double value, long first)
    {
        while (_count > 0 && _items[_head].Index < first)
        {
            _head = Slot(1);
            _count--;
        }
        while (_count > 0)
        {
            var tail = Slot(_count - 1);
            if (!(maximum ? _items[tail].Value < value : _items[tail].Value > value)) break;
            _count--;
        }
        if (_count == _items.Length) throw new InvalidOperationException("Extrema capacity exceeded.");
        _items[Slot(_count)] = (index, value);
        _count++;
    }
    internal double? UniqueAt(long index)
    {
        if (_count == 0 || _items[_head].Index != index) return null;
        return _count > 1 && _items[Slot(1)].Value == Value // NOSONAR: strict extrema exclude exact ties.
            ? null : Value;
    }
}
