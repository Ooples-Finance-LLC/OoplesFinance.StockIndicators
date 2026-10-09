using System.Collections;

namespace OoplesFinance.StockIndicators.Indicators;

// Independently owned snapshots. Large fused batches can own one contiguous array;
// incremental sources retain small chunks. No buffers
// are returned to a pool: runs and later legacy builds can retain this history.
internal sealed class OwnedBarHistory : IReadOnlyList<Bar>
{
    private const int ChunkShift = 10;
    private const int ChunkSize = 1 << ChunkShift;
    private readonly List<Bar[]> _chunks = new();
    private int _expectedCount;
    private Bar[]? _contiguous;
    public int Count { get; private set; }
    internal int ChunkCount => _contiguous is null ? _chunks.Count : Count == 0 ? 0 : ((Count - 1) >> ChunkShift) + 1;

    // The fused caller transfers a fresh fully initialized array; it is never pooled.
    internal void TakeOwnedArray(Bar[] owned)
    {
        if (Count != 0 || _contiguous is not null) throw new InvalidOperationException("History already owns bars.");
        _contiguous = owned;
        Count = owned.Length;
    }

    private void RequireAppendable()
    {
        if (_contiguous is not null) throw new InvalidOperationException("Contiguous history is complete.");
    }

    // Only the fused drain transfers newly allocated, fully initialized chunks.
    internal void AppendOwnedChunk(Bar[] chunk)
    {
        RequireAppendable();
        _chunks.Add(chunk);
        Count += chunk.Length;
    }
    public Bar this[int index] => unchecked((uint)index) < (uint)Count
        ? _contiguous is not null ? _contiguous[index] : _chunks[index >> ChunkShift][index & (ChunkSize - 1)]
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal void ExpectAdditional(int count)
    {
        if (count <= int.MaxValue - Count) _expectedCount = Math.Max(_expectedCount, Count + count);
    }

    internal void Add(in Bar bar)
    {
        RequireAppendable();
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

    internal ReadOnlySpan<Bar> Chunk(int index) => _contiguous is null
        ? _chunks[index].AsSpan(0, Math.Min(ChunkSize, Count - (index << ChunkShift)))
        : _contiguous.AsSpan(index << ChunkShift, Math.Min(ChunkSize, Count - (index << ChunkShift)));

    internal void AppendValidated(ReadOnlySpan<Bar> source, CancellationToken cancellationToken)
    {
        RequireAppendable();
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
        if (_contiguous is not null)
        {
            for (var i = 0; i < Count; i++) yield return _contiguous[i];
            yield break;
        }
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
