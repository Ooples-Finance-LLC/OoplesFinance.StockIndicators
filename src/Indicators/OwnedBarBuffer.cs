#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

// Fixed storage allocated before workers start. Each bar has one writer; the
// completed chunks transfer to a run without a copy or pool lifetime dependency.
internal sealed class OwnedBarBuffer
{
    private const int Shift = 10;
    private readonly Bar[][]? _chunks;
    private readonly Bar[]? _contiguous;
    private readonly int _count;

    internal OwnedBarBuffer(int count)
    {
        _count = count;
        _chunks = new Bar[(int)(((long)count + (1 << Shift) - 1) >> Shift)][];
        for (int i = 0; i < _chunks.Length; i++)
            _chunks[i] = GC.AllocateUninitializedArray<Bar>(Math.Min(1 << Shift, count - (i << Shift)));
    }

    // Existing pilot callers still own their array and retain their chosen layout.
    private OwnedBarBuffer(Bar[] array) { _contiguous = array; _count = array.Length; }
    public static implicit operator OwnedBarBuffer?(Bar[]? array) => array is null ? null : new(array);

    internal ref Bar this[int index] => ref (_contiguous is not null
        ? ref _contiguous[index] : ref _chunks![index >> Shift][index & ((1 << Shift) - 1)]);

    internal void TransferTo(OwnedBarHistory history)
    {
        if (_contiguous is not null) history.TakeOwnedArray(_contiguous);
        else history.TakeOwnedChunks(_chunks!, _count);
    }
}
#endif
