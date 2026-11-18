#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeLadderWords(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, LadderBottomCandle indicator)
    {
        Span<long> ranges = stackalloc long[64];
        Span<Bar> history = stackalloc Bar[68];
        var state = new LadderWordState(indicator, ranges, history[..(window - 1)]);
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        foreach (ref readonly var bar in seed)
        {
            if (cancellation.IsCancellationRequested) return region;
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
            state.Update(in bar, cancellation);
        }
        int tailStart = end - tail.Length;
        for (int row = start; row < end; row++)
        {
            if (cancellation.IsCancellationRequested) return region;
            var bar = row >= tailStart ? tail[row - tailStart] : source[row];
            region.Last = bar;
            if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; return region; }
            output[row] = state.Update(in bar, cancellation);
            if (owned is not null) owned[row] = bar;
        }
        return region;
    }

    // Every accepted price is an integer in one fixed dyadic grid. Ranges may
    // be negative: finite OHLC validation does not require ordered candles.
    private ref struct LadderWordState(LadderBottomCandle indicator, Span<long> ranges, Span<Bar> history)
    {
        private readonly LadderBottomCandle _indicator = indicator;
        private readonly Span<long> _ranges = ranges[..indicator.Period];
        private readonly Span<Bar> _history = history;
        private ExtendedReversalState? _fallback;
        private int _base, _count, _next, _rangeCount, _rangeNext;
        private long _sum, _lag, _shadow;

        internal double Update(in Bar bar, CancellationToken cancellation)
        {
            if (_fallback is not null) return _fallback.Update(in bar);
            if (_count == 0)
            {
                ulong bits = BitConverter.DoubleToUInt64Bits(bar.Open);
                _base = Math.Max(1, (int)((bits >> 52) & 2047) - 2);
            }
            // Check all four coordinates before modifying any word state. The
            // close guard deliberately matches the independently tested model.
            if (!TryPrice(bar.Open, out long open) || !TryPrice(bar.High, out long high)
                || !TryPrice(bar.Low, out long low) || !TryPrice(bar.Close, out _))
            {
                _fallback = (ExtendedReversalState)_indicator.CreateState()!;
                int first = _count == _history.Length ? _next : 0;
                for (int i = 0; i < _count; i++)
                {
                    // Match the generic worker's nonthrowing cancellation
                    // funnel; the caller throws after all workers quiesce.
                    if (cancellation.IsCancellationRequested) return 0;
                    ref readonly var prior = ref _history[(first + i) % _history.Length];
                    _fallback.Update(in prior);
                }
                return _fallback.Update(in bar);
            }

            double value = 0;
            if (_count == _history.Length)
            {
                ref readonly var a = ref Prior(4);
                ref readonly var b = ref Prior(3);
                ref readonly var c = ref Prior(2);
                ref readonly var d = ref Prior(1);
                if (a.Close < a.Open && b.Close < b.Open && c.Close < c.Open && d.Close < d.Open
                    && a.Open > b.Open && b.Open > c.Open && a.Close > b.Close && b.Close > c.Close
                    && (Int128)_shadow * (10 * _indicator.Period) > _lag
                    && bar.Close >= bar.Open && bar.Open > d.Open && bar.Close > d.High)
                    value = 100;
            }

            _lag = _sum;
            // Each price is <2^57 grid units. Removing before adding keeps
            // the signed P<=64 range sum strictly inside Int64, even for
            // unordered high/low. The weighted shadow above needs Int128.
            if (_rangeCount == _ranges.Length) _sum -= _ranges[_rangeNext];
            else _rangeCount++;
            long range = high - low;
            _ranges[_rangeNext] = range;
            if (++_rangeNext == _ranges.Length) _rangeNext = 0;
            _sum += range;
            _shadow = high - open;
            _history[_next] = bar;
            if (++_next == _history.Length) _next = 0;
            if (_count < _history.Length) _count++;
            return value;
        }

        private readonly ref readonly Bar Prior(int offset)
        {
            int index = _next - offset;
            if (index < 0) index += _history.Length;
            return ref _history[index];
        }

        private readonly bool TryPrice(double value, out long units)
        {
            ulong bits = BitConverter.DoubleToUInt64Bits(value);
            int exponent = (int)(bits >> 52);
            int shift = exponent - _base;
            if (shift is < 0 or > 4 || exponent is < 1 or > 2046)
            {
                units = 0;
                return false;
            }
            units = (long)(((bits & 0x000fffffffffffffUL) | 0x0010000000000000UL) << shift);
            return true;
        }
    }

    private static Bar FillLadderWords(Bar[] source, double[][] output, LadderBottomCandle indicator, int period,
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
                var region = ComputeLadderWords(source, output[0], owned, 0, source.Length, period, [], [], cancellation, indicator);
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
                regions[chunk] = ComputeLadderWords(source, output[0], owned, start, end, period,
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

}
#endif
