using System.Collections;

namespace OoplesFinance.StockIndicators.Indicators;

// Independently owned snapshots without a large-object-heap bar array. No buffers
// are returned to a pool: runs and later legacy builds can retain this history.
internal sealed class OwnedBarHistory : IReadOnlyList<Bar>
{
    private const int ChunkShift = 10;
    private const int ChunkSize = 1 << ChunkShift;
    private readonly List<Bar[]> _chunks = new();
    private int _expectedCount;
    public int Count { get; private set; }
    internal int ChunkCount => _chunks.Count;
    public Bar this[int index] => unchecked((uint)index) < (uint)Count
        ? _chunks[index >> ChunkShift][index & (ChunkSize - 1)]
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal void ExpectAdditional(int count)
    {
        if (count <= int.MaxValue - Count) _expectedCount = Math.Max(_expectedCount, Count + count);
    }

    internal void Add(in Bar bar)
    {
        var slot = Count & (ChunkSize - 1);
        var chunkIndex = Count >> ChunkShift;
        if (slot == 0)
            _chunks.Add(new Bar[Math.Min(ChunkSize, Math.Max(16, _expectedCount - Count))]);
        var chunk = _chunks[chunkIndex];
        if (slot == chunk.Length)
        {
            Array.Resize(ref chunk, Math.Min(ChunkSize, chunk.Length * 2));
            _chunks[chunkIndex] = chunk;
        }
        chunk[slot] = bar;
        Count++;
    }

    internal ReadOnlySpan<Bar> Chunk(int index) => _chunks[index].AsSpan(0,
        Math.Min(ChunkSize, Count - (index << ChunkShift)));

    internal IReadOnlyList<Bar> AfterWarmup(int count) => count == 0 ? this : new HistorySlice(this, count);

    private sealed class HistorySlice(OwnedBarHistory owner, int start) : IReadOnlyList<Bar>
    {
        public int Count => owner.Count - start;
        public Bar this[int index] => unchecked((uint)index) < (uint)Count
            ? owner[start + index] : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<Bar> GetEnumerator()
        {
            for (var i = start; i < owner.Count; i++) yield return owner[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public IEnumerator<Bar> GetEnumerator()
    {
        for (var chunk = 0; chunk < _chunks.Count; chunk++)
        {
            var values = _chunks[chunk];
            var count = Math.Min(ChunkSize, Count - (chunk << ChunkShift));
            for (var i = 0; i < count; i++) yield return values[i];
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Only trusted internal states can opt in, after components have run and raw
// finite input has been validated. Chained/custom-domain execution stays general.
internal interface IOwnedHistoryBatchState
{
    bool TryComputeBatch(OwnedBarHistory bars, double[][] output);
}
