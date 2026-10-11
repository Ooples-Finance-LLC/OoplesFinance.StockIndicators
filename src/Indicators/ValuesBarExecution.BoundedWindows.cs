#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsBoundedWindowIndicator(IIndicator indicator) =>
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or Wma or WilliamsR or RollingPriceSum;

    private sealed class SumValueState(int period, int count) : IIndicatorState
    {
        private readonly double[] _window = new double[Math.Min(period, Math.Max(1, count))];
        private ExactMeanAccumulator _sum;
        private int _position, _seen;
        public void Reset() { _position = _seen = 0; _sum = default; }
        public double Update(in Bar bar)
        {
            if (_seen == period) _sum.Add(_window[_position], -1);
            else _seen++;
            _sum.Add(bar.Close);
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            return _seen < period ? 0 : _sum.Mean(1);
        }
    }

    private sealed class WmaValueState(int period, int count) : IIndicatorState
    {
        private readonly double[] _window = new double[Math.Min(period, Math.Max(1, count))];
        private readonly long _denominator = (long)period * (period + 1L) / 2;
        private ExactMeanAccumulator _sum, _weighted;
        private int _position, _seen;
        public void Reset() { _position = _seen = 0; _sum = _weighted = default; }
        public double Update(in Bar bar)
        {
            // Match MovingAverageCore's fixed denominator, including zero-padded
            // startup. Capacity is bounded by the observations this execution owns.
            _weighted.Subtract(_sum);
            _weighted.Add(bar.Close, period);
            _sum.Add(bar.Close);
            if (_seen >= period) _sum.Add(_window[_position], -1);
            else _seen++;
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            return _weighted.Mean(_denominator);
        }
    }

    private sealed class WilliamsValueState(int period, int count) : IIndicatorState
    {
        private readonly ExtremeDeque _high = new(Math.Min(period, Math.Max(1, count)), true);
        private readonly ExtremeDeque _low = new(Math.Min(period, Math.Max(1, count)), false);
        private long _index;
        public void Reset() { _high.Reset(); _low.Reset(); _index = 0; }
        public double Update(in Bar bar)
        {
            _high.Add(_index, bar.High, _index - period + 1);
            _low.Add(_index, bar.Low, _index - period + 1);
            _index++;
            return WilliamsRangePosition.Percent(bar.Close, _low.Value, _high.Value);
        }
    }

    private interface IBoundedKernel<T> where T : struct, IBoundedKernel<T>
    {
        static abstract T Create(int period, int count);
        double Update(in Bar bar);
    }
    private readonly struct HighKernel(PriceExtremeState state) : IBoundedKernel<HighKernel>
    {
        public static HighKernel Create(int period, int count) => new(new(period, count, true));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct LowKernel(PriceExtremeState state) : IBoundedKernel<LowKernel>
    {
        public static LowKernel Create(int period, int count) => new(new(period, count, false));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct WmaKernel(WmaValueState state) : IBoundedKernel<WmaKernel>
    {
        public static WmaKernel Create(int period, int count) => new(new(period, count));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct WilliamsKernel(WilliamsValueState state) : IBoundedKernel<WilliamsKernel>
    {
        public static WilliamsKernel Create(int period, int count) => new(new(period, count));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct SumKernel(SumValueState state) : IBoundedKernel<SumKernel>
    {
        public static SumKernel Create(int period, int count) => new(new(period, count));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private static Bar FillBoundedWindow(Bar[] source, double[] output, IIndicator indicator,
        CancellationToken cancellation, Bar[]? owned) => indicator switch
    {
        HighestHigh high => FillBoundedWindow<HighKernel>(source, output, indicator, high.Length, cancellation, owned),
        LowestLow low => FillBoundedWindow<LowKernel>(source, output, indicator, low.Length, cancellation, owned),
        Wma average => FillBoundedWindow<WmaKernel>(source, output, indicator, Math.Max(1, average.Length), cancellation, owned),
        WilliamsR range => FillBoundedWindow<WilliamsKernel>(source, output, indicator, Math.Max(1, range.Length), cancellation, owned),
        RollingPriceSum sum => FillBoundedWindow<SumKernel>(source, output, indicator, sum.Period, cancellation, owned),
        _ => throw new InvalidOperationException("Unqualified bounded-window kernel.")
    };

    private static Bar FillBoundedWindow<T>(Bar[] source, double[] output, IIndicator indicator, int period,
        CancellationToken cancellation, Bar[]? owned) where T : struct, IBoundedKernel<T>
    {
        cancellation.ThrowIfCancellationRequested();
        int chunks = period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;
        if (chunks < 2 || !CanParallelize(source.Length, 8192) || !Monitor.TryEnter(ParallelBarGate))
        {
            var region = ComputeBoundedRegion<T>(source, output, owned, 0, source.Length, period, [], [], cancellation);
            cancellation.ThrowIfCancellationRequested();
            ValidatePointwiseInput(in region);
            ValidatePointwiseOutput(indicator, in region);
            return region.Last;
        }
        Bar[][]? tails = null;
        try
        {
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
                regions[chunk] = ComputeBoundedRegion<T>(source, output, owned, start, end, period,
                    chunk == 0 ? [] : tails[chunk - 1].AsSpan(0, period - 1),
                    chunk == chunks - 1 ? [] : tails[chunk].AsSpan(0, period - 1), cancellation);
            });
            cancellation.ThrowIfCancellationRequested();
            foreach (var region in regions) ValidatePointwiseInput(in region);
            foreach (var region in regions) ValidatePointwiseOutput(indicator, in region);
            return regions[^1].Last;
        }
        finally
        {
            if (tails is not null)
                foreach (var tail in tails)
                    if (tail is { Length: > 0 }) System.Buffers.ArrayPool<Bar>.Shared.Return(tail);
            Monitor.Exit(ParallelBarGate);
        }
    }

    private static PointwiseRegion ComputeBoundedRegion<T>(Bar[] source, double[] output, Bar[]? owned,
        int start, int end, int period, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation) where T : struct, IBoundedKernel<T>
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        var kernel = T.Create(period, end - start + seed.Length);
        foreach (ref readonly var bar in seed)
        {
            if (cancellation.IsCancellationRequested) return region;
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
            kernel.Update(in bar);
        }
        Bar latest = default;
        int tailStart = end - tail.Length;
        for (int i = start; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) break;
            latest = i >= tailStart ? tail[i - tailStart] : source[i];
            if (!AllFieldsFinite(in latest)) { region.InvalidInput = true; break; }
            var value = kernel.Update(in latest);
            output[i] = value;
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
}
#endif
