#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillThreeInsideWords(Bar[] source, double[][] output, ThreeInsideCandle indicator, int period,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        cancellation.ThrowIfCancellationRequested();
        int chunks = period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;

        bool lockTaken = false;
        Bar[][]? tails = null;
        try
        {
            if (chunks >= 2 && CanParallelize(source.Length, BoundedParallelMinimum(indicator)))
                Monitor.TryEnter(ParallelBarGate, ref lockTaken);
            if (!lockTaken)
            {
                var region = ComputeThreeInsideWordRegion(source, output, owned, 0, source.Length, period, [], [], cancellation, indicator);
                cancellation.ThrowIfCancellationRequested();
                ValidatePointwiseInput(in region);
                ValidatePointwiseOutput(indicator, in region);
                return region.Last;
            }
            tails = new Bar[chunks - 1][];
            var regions = new PointwiseRegion[chunks];
            // Each tail belongs to one disjoint input region. Its owner consumes
            // this capture too, so no source bar is read a second time for seeding.
            for (int chunk = 0; chunk < tails.Length; chunk++)
            {
                cancellation.ThrowIfCancellationRequested();
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                tails[chunk] = period == 1 ? [] : System.Buffers.ArrayPool<Bar>.Shared.Rent(period - 1);
                source.AsSpan(end - period + 1, period - 1).CopyTo(tails[chunk]);
            }
            AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                regions[chunk] = ComputeThreeInsideWordRegion(source, output, owned, start, end, period,
                    chunk == 0 ? [] : tails[chunk - 1].AsSpan(0, period - 1),
                    chunk == chunks - 1 ? [] : tails[chunk].AsSpan(0, period - 1), cancellation, indicator);
            });
            cancellation.ThrowIfCancellationRequested();
            foreach (var region in regions) ValidatePointwiseInput(in region);
            foreach (var region in regions) ValidatePointwiseOutput(indicator, in region);
            return regions[^1].Last;
        }
        finally
        {
            // Workers have quiesced. Release the gate even if pool cleanup fails.
            if (lockTaken) Monitor.Exit(ParallelBarGate);
            if (tails is not null)
                foreach (var tail in tails)
                    if (tail is { Length: > 0 }) System.Buffers.ArrayPool<Bar>.Shared.Return(tail);
        }
    }


    private static PointwiseRegion ComputeThreeInsideWordRegion(Bar[] source, double[][] output,
        OwnedBarBuffer? owned, int start, int end, int period, ReadOnlySpan<Bar> seed,
        ReadOnlySpan<Bar> tail, CancellationToken cancellation, ThreeInsideCandle indicator)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        Span<ulong> longRing = stackalloc ulong[indicator.LongPeriod];
        Span<ulong> shortRing = indicator.ShortPeriod == indicator.LongPeriod
            ? Span<ulong>.Empty : stackalloc ulong[indicator.ShortPeriod];
        Span<Bar> captured = stackalloc Bar[period - 1];
        var state = new ThreeInsideWordState(indicator, longRing, shortRing, captured);
        foreach (ref readonly var bar in seed)
        {
            if (cancellation.IsCancellationRequested) return region;
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
            if (!state.Update(in bar, cancellation, out _)) return region;
        }
        Bar latest = default;
        int tailStart = end - tail.Length;
        for (int i = start; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) break;
            latest = i >= tailStart ? tail[i - tailStart] : source[i];
            if (!AllFieldsFinite(in latest)) { region.InvalidInput = true; break; }
            if (!state.Update(in latest, cancellation, out var value)) break;
            output[0][i] = value;
            if (owned is not null) owned[i] = latest;
            if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
            {
                region.InvalidOutputIndex = i;
                region.InvalidOutputValue = value;
            }
        }
        region.Last = latest;
        return region;
    }

    // Private to this batch route: retain the original state and its IL for all
    // other consumers, and use it permanently after the first unsupported bar.
    private ref struct ThreeInsideWordState
    {
        private readonly ThreeInsideCandle _indicator;
        private readonly Span<ulong> _longRing, _shortRing;
        private readonly Span<Bar> _captured;
        private TripleBodyCandleState? _original;
        private int _baseEncoded, _longCount, _shortCount, _longNext, _shortNext;
        private int _historyCount, _historyNext, _seen;
        private ulong _longSum, _shortSum, _lagLong, _lagLong2, _lagShort;
        private ulong _firstBody, _secondBody;
        private double _firstOpen, _firstClose, _secondOpen, _secondClose;

        internal ThreeInsideWordState(ThreeInsideCandle indicator, Span<ulong> longRing,
            Span<ulong> shortRing, Span<Bar> captured)
        {
            this = default;
            _indicator = indicator;
            _longRing = longRing;
            _shortRing = shortRing;
            _captured = captured;
        }

        internal bool Update(scoped in Bar bar, CancellationToken cancellation, out double value)
        {
            if (_original is not null)
            {
                value = _original.Update(in bar);
                return true;
            }
            if (!TryWord(in bar, out value))
            {
                _original = (TripleBodyCandleState)_indicator.CreateState()!;
                int previous = _historyCount == _captured.Length ? _historyNext : 0;
                for (int i = 0; i < _historyCount; i++)
                {
                    if (cancellation.IsCancellationRequested) return false;
                    ref readonly var prior = ref _captured[previous];
                    _original.Update(in prior);
                    if (++previous == _captured.Length) previous = 0;
                }
                if (cancellation.IsCancellationRequested) return false;
                // This is the current already-captured bar, not a source reread.
                value = _original.Update(in bar);
                return true;
            }
            _captured[_historyNext] = bar;
            if (++_historyNext == _captured.Length) _historyNext = 0;
            if (_historyCount < _captured.Length) _historyCount++;
            return true;
        }

        private bool TryWord(scoped in Bar bar, out double value)
        {
            value = 0;
            ulong openBits = BitConverter.DoubleToUInt64Bits(bar.Open);
            ulong closeBits = BitConverter.DoubleToUInt64Bits(bar.Close);
            int openEncoded = (int)((openBits >> 52) & 0x7ffUL);
            int closeEncoded = (int)((closeBits >> 52) & 0x7ffUL);
            // The base comes from the first captured seed/bar only. Normal
            // positive prices in five adjacent binades have integer units <2^57.
            int grid = _baseEncoded == 0 ? Math.Max(1, openEncoded - 2) : _baseEncoded;
            if (((openBits | closeBits) >> 63) != 0
                || openEncoded is < 1 or > 2046 || closeEncoded is < 1 or > 2046
                || openEncoded < grid || openEncoded > grid + 4
                || closeEncoded < grid || closeEncoded > grid + 4) return false;
            const ulong mantissaMask = (1UL << 52) - 1;
            ulong openUnits = ((openBits & mantissaMask) | (1UL << 52)) << (openEncoded - grid);
            ulong closeUnits = ((closeBits & mantissaMask) | (1UL << 52)) << (closeEncoded - grid);
            ulong body = openUnits >= closeUnits ? openUnits - closeUnits : closeUnits - openUnits;
            _baseEncoded = grid;
            // These are exact, unrounded price differences, as in the original
            // accumulator's separate Max/Min additions. Each period <=64 gives
            // nonnegative sums/products <2^63; compare without unsigned margins.
            if (_seen >= _indicator.WarmupBars
                && _firstBody * (ulong)_indicator.LongPeriod > _lagLong2
                && _secondBody * (ulong)_indicator.ShortPeriod <= _lagShort
                && Math.Max(_secondOpen, _secondClose) < Math.Max(_firstOpen, _firstClose)
                && Math.Min(_secondOpen, _secondClose) > Math.Min(_firstOpen, _firstClose)
                && (_firstClose >= _firstOpen
                    ? bar.Close < bar.Open && bar.Close < _firstOpen
                    : bar.Close >= bar.Open && bar.Close > _firstOpen))
                value = _firstClose < _firstOpen ? 100d : -100d;

            _lagLong2 = _lagLong;
            _lagLong = _longSum;
            _lagShort = _shortSum;
            // Remove before adding: no transient period+one sum or uninitialized
            // stack read. A full ring always contains the value being removed.
            if (_longCount == _longRing.Length) _longSum -= _longRing[_longNext];
            else _longCount++;
            _longRing[_longNext] = body;
            if (++_longNext == _longRing.Length) _longNext = 0;
            _longSum += body;
            if (_shortRing.IsEmpty) _shortSum = _longSum;
            else
            {
                if (_shortCount == _shortRing.Length) _shortSum -= _shortRing[_shortNext];
                else _shortCount++;
                _shortRing[_shortNext] = body;
                if (++_shortNext == _shortRing.Length) _shortNext = 0;
                _shortSum += body;
            }
            _firstOpen = _secondOpen;
            _firstClose = _secondClose;
            _secondOpen = bar.Open;
            _secondClose = bar.Close;
            _firstBody = _secondBody;
            _secondBody = body;
            if (_seen < _indicator.WarmupBars) _seen++;
            return true;
        }
    }
}
#endif
