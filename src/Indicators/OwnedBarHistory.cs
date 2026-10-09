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

    // Only the fused drain transfers newly allocated, fully initialized chunks.
    internal void AppendOwnedChunk(Bar[] chunk)
    {
        _chunks.Add(chunk);
        Count += chunk.Length;
    }
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

    internal void AppendValidated(ReadOnlySpan<Bar> source, CancellationToken cancellationToken)
    {
        ExpectAdditional(source.Length);
        var offset = 0;
        // Complete a partially filled chunk before transferring whole chunks.
        while (offset < source.Length && (Count & (ChunkSize - 1)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bar = source[offset++];
            Validation.IndicatorInputDomain.Finite.Validate(in bar);
            Add(in bar);
        }
        while (offset < source.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(ChunkSize, source.Length - offset);
#if NETFRAMEWORK
            var owned = new Bar[length];
#else
            var owned = GC.AllocateUninitializedArray<Bar>(length);
#endif
            source.Slice(offset, length).CopyTo(owned);
            // The caller can mutate its array: certify the owned copy, never
            // validate caller memory and then copy possibly different values.
            for (var i = 0; i < owned.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ref readonly var bar = ref owned[i];
                if (!Helpers.FrameworkCompatibility.IsFinite(bar.Open)
                    || !Helpers.FrameworkCompatibility.IsFinite(bar.High)
                    || !Helpers.FrameworkCompatibility.IsFinite(bar.Low)
                    || !Helpers.FrameworkCompatibility.IsFinite(bar.Close)
                    || !Helpers.FrameworkCompatibility.IsFinite(bar.Volume))
                    Validation.IndicatorInputDomain.Finite.Validate(in bar);
            }
            _chunks.Add(owned);
            Count += length;
            offset += length;
        }
    }

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
