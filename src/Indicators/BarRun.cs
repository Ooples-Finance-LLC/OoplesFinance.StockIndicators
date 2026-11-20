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
        private readonly IReadOnlyList<Bar>? _list;
        private readonly int _count;
        private int _index;

        internal Enumerator(ReadOnlySpan<Bar> rows, IReadOnlyList<Bar>? list)
        {
            _rows = rows;
            _list = list;
            _count = list?.Count ?? rows.Length;
            _index = -1;
        }

        public bool MoveNext() => ++_index < _count;

        public readonly Bar Current => _list is { } list ? list[_index] : _rows[_index];
    }
}
