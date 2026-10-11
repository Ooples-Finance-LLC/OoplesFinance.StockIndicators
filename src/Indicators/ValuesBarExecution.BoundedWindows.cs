#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsBoundedWindowIndicator(IIndicator indicator) =>
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or Wma or WilliamsR or RollingPriceSum or EngulfingPattern
            or LaggedPriceChange { Period: < int.MaxValue } or RateOfChangeWithValidity { Period: < int.MaxValue } or WindowRangeMidpoint or WindowPriceRange or WindowMode or WindowExtremeIndex or DecayingWindowExtreme { Decay: 0 }
        || BodyShadowPeriod(indicator) is > 0 and < int.MaxValue
        || indicator is RollingPercentile { HasFractionalFullRank: true }
        || AroonWindow(indicator) > 0
        || WeightedWindowPeriod(indicator) > 0
        || PercentileCandlePeriod(indicator) > 0
        || HaramiWindow(indicator) > 0 || TasukiWindow(indicator) > 0
        || TrendReversalWindow(indicator) > 0;

    private static int AroonWindow(IIndicator indicator) => indicator is WindowAroon aroon
        && (!aroon.IncludeExtraBar || aroon.Period < int.MaxValue)
            ? aroon.Period + (aroon.IncludeExtraBar ? 1 : 0) : 0;

    private static int TrendReversalWindow(IIndicator indicator)
    {
        var (trend, body) = indicator switch
        {
            BullishAbandonedBabyPattern c => (c.TrendPeriod, c.LongPeriod),
            BearishAbandonedBabyPattern c => (c.TrendPeriod, c.LongPeriod),
            TrendStarPattern c => (c.TrendPeriod, c.Period),
            _ => (0, 0)
        };
        if (trend == 0) return 0;
        // Long-body qualification at i-2 needs P+1 seed bars. Bearish bodies
        // always use the fixed 20-bar window; trends end on the middle candle.
        int longest = Math.Max(20, Math.Max(trend, body));
        return longest < int.MaxValue - 1 ? longest + 2 : 0;
    }

    // Directional predicates inspect P transitions ending on the previous bar:
    // P+1 seeds, plus the current bar. Guard the addition for extreme periods.
    private static int HaramiWindow(IIndicator indicator) => indicator switch
    {
        StrictHaramiCandle => 2,
        BullishHaramiPattern { Period: < int.MaxValue - 1 } candle => candle.Period + 2,
        BearishHaramiPattern { Period: < int.MaxValue - 1 } candle => candle.Period + 2,
        _ => 0
    };
    private static int TasukiWindow(IIndicator indicator) => indicator switch
    {
        UpsideTasukiGapPattern { Period: < int.MaxValue - 1 } candle => candle.Period + 2,
        DownsideTasukiGapPattern { Period: < int.MaxValue - 1 } candle => candle.Period + 2,
        _ => 0
    };

    private static int PercentileCandlePeriod(IIndicator indicator) => indicator switch
    {
        LongBodyCandle candle => candle.Period,
        ShortBodyCandle candle => candle.Period,
        BullishLongBodyCandle candle => candle.Period,
        BearishLongBodyCandle candle => candle.Period,
        BullishShortBodyCandle candle => candle.Period,
        BearishShortBodyCandle candle => candle.Period,
        UpperShadowAtOrAbovePercentileCandle candle => candle.Period,
        LowerShadowBelowPercentileCandle candle => candle.Period,
        _ => 0
    };

    private static int WeightedWindowPeriod(IIndicator indicator) => indicator switch
    {
        FixedPeriodWma average => average.Period,
        NormalizedConvolution convolution => convolution.Kernel.Count,
        GaussianWeightedAverage average => average.Period,
        SineWeightedAverage average => average.Period,
        _ => 0
    };

    private static int BoundedParallelMinimum(IIndicator indicator)
    {
        // Exact weighted recurrences still perform several accumulator updates
        // per bar. The 1k native-worker comparison justifies scheduling at 512.
        if (indicator is FixedPeriodWma or NormalizedConvolution { HasAffineKernel: true } or WindowRangeMidpoint or WindowPriceRange or WindowAroon or RollingPercentile or WindowMode or RateOfChangeWithValidity) return 512;
        // Fixed convolutions perform one exact product per coefficient per bar.
        // Keep the existing threshold for the other constant-work kernels; for convolutions
        // use an 8192-term work estimate and at least 512 bars for scheduling.
        int terms = indicator is NormalizedConvolution { HasAffineKernel: false } or GaussianWeightedAverage or SineWeightedAverage
            ? WeightedWindowPeriod(indicator) : 1;
        return Math.Max(512, 1 + 8191 / terms);
    }

    private static int BodyShadowPeriod(IIndicator indicator) => indicator switch
    {
        MarubozuCandle c => c.Period,
        ClosingMarubozuCandle c => c.Period,
        BeltHoldCandle c => c.Period,
        SpinningTopCandle c => c.Period,
        HighWaveCandle c => c.Period,
        LongLineCandle c => c.Period,
        ShortLineCandle c => c.Period,
        _ => 0
    };

    private sealed class LaggedValueState(int period, int count, PriceChangeKind kind) : IIndicatorState
    {
        private readonly double[] _window = new double[Math.Min(period, Math.Max(1, count))];
        private int _position, _seen;
        public void Reset() { _position = _seen = 0; }
        public double Update(in Bar bar)
        {
            double value = _seen < period ? 0 : LaggedPriceChange.Calculate(bar.Close, _window[_position], kind);
            if (_seen < period) _seen++;
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            return value;
        }
    }

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

    private readonly record struct BoundedOutput(double[][] Values, int Origin, int WriteStart);

    private interface IBoundedKernel<T> where T : struct, IBoundedKernel<T>
    {
        static abstract T Create(int period, int count, IIndicator indicator, in BoundedOutput target);
        double Update(in Bar bar);
    }
    private readonly struct PrefixHighKernel(PrefixPriceExtremeState state) : IBoundedKernel<PrefixHighKernel>
    {
        public static PrefixHighKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(true));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct PrefixLowKernel(PrefixPriceExtremeState state) : IBoundedKernel<PrefixLowKernel>
    {
        public static PrefixLowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(false));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct HighKernel(PriceExtremeState state) : IBoundedKernel<HighKernel>
    {
        public static HighKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period, count, true));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct LowKernel(PriceExtremeState state) : IBoundedKernel<LowKernel>
    {
        public static LowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period, count, false));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct WmaKernel(WmaValueState state) : IBoundedKernel<WmaKernel>
    {
        public static WmaKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period, count));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct WilliamsKernel(WilliamsValueState state) : IBoundedKernel<WilliamsKernel>
    {
        public static WilliamsKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period, count));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct SumKernel(SumValueState state) : IBoundedKernel<SumKernel>
    {
        public static SumKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period, count));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct EngulfingKernel(EngulfingPattern.State state) : IBoundedKernel<EngulfingKernel>
    {
        public static EngulfingKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new());
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct LaggedKernel(LaggedValueState state) : IBoundedKernel<LaggedKernel>
    {
        public static LaggedKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var change = (LaggedPriceChange)indicator;
            return new(new(change.Period, count, change.Kind));
        }
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct BodyShadowKernel(BodyShadowCandleState state) : IBoundedKernel<BodyShadowKernel>
    {
        public static BodyShadowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((BodyShadowCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct TrendBabyKernel(TrendBabyState state) : IBoundedKernel<TrendBabyKernel>
    {
        public static TrendBabyKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((TrendBabyState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct TrendStarKernel(TrendStarState state) : IBoundedKernel<TrendStarKernel>
    {
        public static TrendStarKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((TrendStarState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct HaramiKernel(StrictHaramiState state) : IBoundedKernel<HaramiKernel>
    {
        public static HaramiKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((StrictHaramiState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private readonly struct TasukiKernel(TrendTasukiState state) : IBoundedKernel<TasukiKernel>
    {
        public static TasukiKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((TrendTasukiState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct PercentileCandleKernel(PercentileCandleState state) : IBoundedKernel<PercentileCandleKernel>
    {
        public static PercentileCandleKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((PercentileCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct PercentileKernel(RollingPercentile.State state) : IBoundedKernel<PercentileKernel>
    {
        public static PercentileKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var percentile = (RollingPercentile)indicator;
            return new(new(period, percentile.Percentile, percentile.AverageDuringWarmup));
        }
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // Exact modal means are independent of dictionary insertion order, including
    // the representative chosen for equal signed zeros. P-1 seeds suffice.
    private readonly struct ModeKernel(WindowMode.State state) : IBoundedKernel<ModeKernel>
    {
        public static ModeKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct PriceRangeKernel(WindowPriceRange.State state) : IBoundedKernel<PriceRangeKernel>
    {
        public static PriceRangeKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct MidpointKernel(WindowRangeMidpoint.State state) : IBoundedKernel<MidpointKernel>
    {
        public static MidpointKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, ((WindowRangeMidpoint)indicator).CandleRanges));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private sealed class CloseExtremeState(int period, bool maximum) : IIndicatorState
    {
        private readonly WindowExtremeDeque _deque = new(period, maximum);
        public void Reset() => _deque.Reset();
        public double Update(in Bar bar)
        {
            _deque.Add(bar.Close);
            return _deque.Value;
        }
    }

    private readonly struct CloseExtremeKernel(CloseExtremeState state) : IBoundedKernel<CloseExtremeKernel>
    {
        public static CloseExtremeKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, ((DecayingWindowExtreme)indicator).Maximum));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private sealed class ExtremeIndexState(int period, bool maximum, int origin = 0) : IIndicatorState
    {
        private readonly WindowExtremeDeque _deque = new(period, maximum);
        public void Reset() => _deque.Reset();
        public double Update(in Bar bar)
        {
            _deque.Add(bar.Close);
            return origin + _deque.Index;
        }
    }

    private readonly struct ExtremeIndexKernel(ExtremeIndexState state) : IBoundedKernel<ExtremeIndexKernel>
    {
        public static ExtremeIndexKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, ((WindowExtremeIndex)indicator).Maximum, target.Origin));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // Small windows can retain the winner and rescan only when it expires.
    // Cap this route so monotone inputs cannot turn large windows into O(NP) work.
    private sealed class SmallExtremeIndexState(int period, bool maximum, int origin = 0) : IIndicatorState
    {
        private readonly double[] _window = new double[period];
        private int _position, _next, _bestIndex;
        private double _best;
        public void Reset() { _position = _next = _bestIndex = 0; _best = 0; }
        public double Update(in Bar bar)
        {
            double value = bar.Close;
            int current = _next++;
            _window[_position] = value;
            if (++_position == period) _position = 0;
            if (current == 0 || (maximum ? value >= _best : value <= _best))
            {
                _best = value;
                _bestIndex = current;
            }
            else if (_bestIndex <= current - period)
            {
                _best = value;
                _bestIndex = current;
                int slot = _position == 0 ? period - 1 : _position - 1;
                // Reverse traversal plus strict comparison preserves newest ties.
                for (int age = 1; age < period; age++)
                {
                    if (--slot < 0) slot = period - 1;
                    value = _window[slot];
                    if (maximum ? value > _best : value < _best)
                    {
                        _best = value;
                        _bestIndex = current - age;
                    }
                }
            }
            return origin + _bestIndex;
        }
    }

    private readonly struct SmallExtremeIndexKernel(SmallExtremeIndexState state) : IBoundedKernel<SmallExtremeIndexKernel>
    {
        public static SmallExtremeIndexKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, ((WindowExtremeIndex)indicator).Maximum, target.Origin));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private struct AroonKernel(WindowAroon.State state, BoundedOutput target) : IBoundedKernel<AroonKernel>
    {
        private readonly double[] _scratch = new double[6];
        private int _index = target.Origin;
        public static AroonKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var aroon = (WindowAroon)indicator;
            return new(new(aroon.Period, aroon.IncludeExtraBar, aroon.OldestTie), target);
        }
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            // Captured seed inputs initialize state but belong to another worker.
            if (index >= target.WriteStart)
                for (int slot = 1; slot < _scratch.Length; slot++) target.Values[slot][index] = _scratch[slot];
            return _scratch[0];
        }
    }

    private struct ValidReturnKernel(RateOfChangeWithValidity.State state, BoundedOutput target) : IBoundedKernel<ValidReturnKernel>
    {
        private int _index = target.Origin;
        public static ValidReturnKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(((RateOfChangeWithValidity)indicator).Period), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            // Seed outputs belong to the preceding worker; only publish owned rows.
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private readonly struct StateWindowKernel(IIndicatorState state) : IBoundedKernel<StateWindowKernel>
    {
        public static StateWindowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((IIndicatorState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private static Bar FillBoundedWindow(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator switch
    {
        HighestHigh high when high.Length >= source.Length => FillBoundedWindow<PrefixHighKernel>(source, output, indicator, high.Length, cancellation, owned),
        LowestLow low when low.Length >= source.Length => FillBoundedWindow<PrefixLowKernel>(source, output, indicator, low.Length, cancellation, owned),
        HighestHigh high => FillBoundedWindow<HighKernel>(source, output, indicator, high.Length, cancellation, owned),
        LowestLow low => FillBoundedWindow<LowKernel>(source, output, indicator, low.Length, cancellation, owned),
        Wma average => FillBoundedWindow<WmaKernel>(source, output, indicator, Math.Max(1, average.Length), cancellation, owned),
        WilliamsR range => FillBoundedWindow<WilliamsKernel>(source, output, indicator, Math.Max(1, range.Length), cancellation, owned),
        RollingPriceSum sum => FillBoundedWindow<SumKernel>(source, output, indicator, sum.Period, cancellation, owned),
        WindowAroon when AroonWindow(indicator) > 0 => FillBoundedWindow<AroonKernel>(source, output, indicator, AroonWindow(indicator), cancellation, owned),
        RollingPercentile { HasFractionalFullRank: true } percentile => FillBoundedWindow<PercentileKernel>(source, output, indicator, percentile.Period, cancellation, owned),
        RateOfChangeWithValidity { Period: < int.MaxValue } change => FillBoundedWindow<ValidReturnKernel>(source, output, indicator, change.Period + 1, cancellation, owned),
        WindowMode mode => FillBoundedWindow<ModeKernel>(source, output, indicator, mode.Period, cancellation, owned),
        WindowPriceRange range => FillBoundedWindow<PriceRangeKernel>(source, output, indicator, range.Period, cancellation, owned),
        WindowRangeMidpoint midpoint => FillBoundedWindow<MidpointKernel>(source, output, indicator, midpoint.Period, cancellation, owned),
        WindowExtremeIndex { Period: <= 32 } index => FillBoundedWindow<SmallExtremeIndexKernel>(source, output, indicator, index.Period, cancellation, owned),
        WindowExtremeIndex index => FillBoundedWindow<ExtremeIndexKernel>(source, output, indicator, index.Period, cancellation, owned),
        DecayingWindowExtreme { Decay: 0 } extreme => FillBoundedWindow<CloseExtremeKernel>(source, output, indicator, extreme.Period, cancellation, owned),
        LaggedPriceChange change => FillBoundedWindow<LaggedKernel>(source, output, indicator, change.Period + 1, cancellation, owned),
        // Two seed bars preserve the pattern's two-bar startup in every worker.
        EngulfingPattern => FillBoundedWindow<EngulfingKernel>(source, output, indicator, 3, cancellation, owned),
        // Each predicate reads the preceding P candles, excluding the current bar.
        _ when BodyShadowPeriod(indicator) is > 0 and < int.MaxValue =>
            FillBoundedWindow<BodyShadowKernel>(source, output, indicator, BodyShadowPeriod(indicator) + 1, cancellation, owned),
        // Fixed coefficients and exact eviction make P-1 preceding closes sufficient.
        _ when WeightedWindowPeriod(indicator) > 0 =>
            FillBoundedWindow<StateWindowKernel>(source, output, indicator, WeightedWindowPeriod(indicator), cancellation, owned),
        // Percentile windows include the current candle, requiring only P-1 seeds.
        _ when PercentileCandlePeriod(indicator) > 0 =>
            FillBoundedWindow<PercentileCandleKernel>(source, output, indicator, PercentileCandlePeriod(indicator), cancellation, owned),
        TrendStarPattern when TrendReversalWindow(indicator) > 0 =>
            FillBoundedWindow<TrendStarKernel>(source, output, indicator, TrendReversalWindow(indicator), cancellation, owned),
        _ when TrendReversalWindow(indicator) > 0 =>
            FillBoundedWindow<TrendBabyKernel>(source, output, indicator, TrendReversalWindow(indicator), cancellation, owned),
        _ when HaramiWindow(indicator) > 0 =>
            FillBoundedWindow<HaramiKernel>(source, output, indicator, HaramiWindow(indicator), cancellation, owned),
        _ when TasukiWindow(indicator) > 0 =>
            FillBoundedWindow<TasukiKernel>(source, output, indicator, TasukiWindow(indicator), cancellation, owned),
        _ => throw new InvalidOperationException("Unqualified bounded-window kernel.")
    };

    private static Bar FillBoundedWindow<T>(Bar[] source, double[][] output, IIndicator indicator, int period,
        CancellationToken cancellation, OwnedBarBuffer? owned) where T : struct, IBoundedKernel<T>
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
                var region = ComputeBoundedRegion<T>(source, output, owned, 0, source.Length, period, [], [], cancellation, indicator);
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
                regions[chunk] = ComputeBoundedRegion<T>(source, output, owned, start, end, period,
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

    private static PointwiseRegion ComputeBoundedRegion<T>(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, int period, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, IIndicator indicator) where T : struct, IBoundedKernel<T>
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        var target = new BoundedOutput(output, start - seed.Length, start);
        var kernel = T.Create(period, end - start + seed.Length, indicator, in target);
        var primary = output[0];
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
            primary[i] = value;
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
