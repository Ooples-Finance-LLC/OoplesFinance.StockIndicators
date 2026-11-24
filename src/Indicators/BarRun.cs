namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// A list of bars to be read from first to last. An array or a list is read in place; asking the interface
/// for each bar in turn costs several times what reading it does, which on a short calculation is most of it.
/// </summary>
internal readonly ref struct BarRun
{
    private readonly ReadOnlySpan<Bar> _rows;
    private readonly IReadOnlyList<Bar>? _list;

    private BarRun(ReadOnlySpan<Bar> rows, IReadOnlyList<Bar>? list)
    {
        _rows = rows;
        _list = list;
    }

    internal static BarRun Of(IReadOnlyList<Bar> bars)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (bars is Bar[] array) return new BarRun(array, null);
#if NET5_0_OR_GREATER
        if (bars is List<Bar> list) return new BarRun(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list), null);
#endif
        return new BarRun(default, bars);
    }

    public Enumerator GetEnumerator() => new(_rows, _list);

    internal ref struct Enumerator
    {
        private readonly ReadOnlySpan<Bar> _rows;
        // Any other list is read the way it offers to be read from first to last: its indexer may cost a
        // search on every call, and a caller may be counting what is asked of it.
        private readonly IEnumerator<Bar>? _source;
        private int _index;

        internal Enumerator(ReadOnlySpan<Bar> rows, IReadOnlyList<Bar>? list)
        {
            _rows = rows;
            _source = list?.GetEnumerator();
            _index = -1;
        }

        public bool MoveNext() => _source is { } source ? source.MoveNext() : ++_index < _rows.Length;

        public readonly Bar Current => _source is { } source ? source.Current : _rows[_index];

        public readonly void Dispose() => _source?.Dispose();
    }
}
