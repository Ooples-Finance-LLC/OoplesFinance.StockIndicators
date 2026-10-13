#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsBoundedWindowIndicator(IIndicator indicator) =>
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or Wma or WilliamsR or RollingPriceSum or EngulfingPattern
            or WindowPathRatio { Period: < int.MaxValue } or WindowLinearRegression { Period: <= 32767 } or LaggedPriceChange { Period: < int.MaxValue } or RateOfChangeWithValidity { Period: < int.MaxValue } or WindowRangeMidpoint or WindowPriceRange or WindowMode or WindowDispersion or WindowSampleShape or WindowUlcerIndex or PriorPriceChannel { Period: < int.MaxValue } or ClassicDeviationBands { Method: ClassicAverageMethod.Sma } or WindowDeviationBands or WindowExtremeIndex or DecayingWindowExtreme { Decay: 0 }
        || indicator is WindowReturnBeta { Period: < int.MaxValue }
        || indicator is WindowMoneyFlowIndex { Period: < int.MaxValue } or WindowVortex { Period: < int.MaxValue } or WindowUltimateOscillator { LongPeriod: < int.MaxValue } or WindowChoppinessIndex { Period: < int.MaxValue }
        || indicator is WindowCorrelation or WindowPairStatistics or MoneyFlowWithDetails
        || CommodityReplayWindow(indicator) > 0
        || BodyShadowPeriod(indicator) is > 0 and < int.MaxValue
        || indicator is RollingPercentile { HasFractionalFullRank: true }
        || RelativeVolatilityWindow(indicator) > 0
        || HullReplayWindow(indicator) > 0
        || DetailedDeviationWindow(indicator) > 0
        || AroonWindow(indicator) > 0
        || WeightedWindowPeriod(indicator) > 0
        || PercentileCandlePeriod(indicator) > 0
        || HaramiWindow(indicator) > 0 || TasukiWindow(indicator) > 0
        || TrendReversalWindow(indicator) > 0 || ContainmentWindow(indicator) > 0 || NeckWindow(indicator) > 0
        || KickingWindow(indicator) > 0 || MatchedLinesWindow(indicator) > 0 || SequenceWindow(indicator) > 0;

    // Replay the original startup length to recover up to three anchor candles
    // and their lagged range windows. The maximum-period fallback stays scalar.
    private static int SequenceWindow(IIndicator indicator) => indicator is
        MatchingLowCandle or StickSandwichCandle or ThreeLineStrikeCandle or ThreeOutsideCandle
        && indicator.WarmupBars < int.MaxValue ? indicator.WarmupBars + 1 : 0;

    private readonly struct SequenceKernel(SequenceCandleState state) : IBoundedKernel<SequenceKernel>
    {
        public static SequenceKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((SequenceCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // Three Outside uses only open/close comparisons from the preceding two
    // candles. Keep its three-bar startup without shifting unused range arrays.
    private struct ThreeOutsideKernel : IBoundedKernel<ThreeOutsideKernel>
    {
        private double _lastOpen, _lastClose, _middleOpen, _middleClose;
        private int _seen;
        public static ThreeOutsideKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => default;
        public double Update(in Bar bar)
        {
            double value = 0;
            if (_seen == 3)
            {
                bool lastWhite = _lastClose >= _lastOpen;
                bool middleWhite = _middleClose >= _middleOpen;
                bool up = lastWhite && !middleWhite && _lastClose > _middleOpen
                    && _lastOpen < _middleClose && bar.Close > _lastClose;
                bool down = !lastWhite && middleWhite && _lastOpen > _middleClose
                    && _lastClose < _middleOpen && bar.Close < _lastClose;
                value = up ? 100 : down ? -100 : 0;
            }
            else _seen++;
            _middleOpen = _lastOpen; _middleClose = _lastClose;
            _lastOpen = bar.Open; _lastClose = bar.Close;
            return value;
        }
    }

    // Warmup includes the longest threshold window plus one anchor, or two
    // for the side-by-side gap pattern. Replay it before publishing a region.
    private static int MatchedLinesWindow(IIndicator indicator) => indicator is
        CounterattackCandle or SeparatingLinesCandle or GapSideBySideWhiteLinesCandle
        && indicator.WarmupBars < int.MaxValue ? indicator.WarmupBars + 1 : 0;

    private readonly struct MatchedLinesKernel(MatchedLinesState state) : IBoundedKernel<MatchedLinesKernel>
    {
        public static MatchedLinesKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((MatchedLinesState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // The previous candle's thresholds need the longest preceding window,
    // followed by that candle; the current candle then has both exact windows.
    private static int KickingWindow(IIndicator indicator)
    {
        int period = indicator switch
        {
            KickingCandle c => Math.Max(c.BodyPeriod, c.ShadowPeriod),
            KickingByLengthCandle c => Math.Max(c.BodyPeriod, c.ShadowPeriod),
            _ => 0
        };
        return period is > 0 and < int.MaxValue - 1 ? period + 2 : 0;
    }

    private readonly struct KickingKernel(KickingCandleState state) : IBoundedKernel<KickingKernel>
    {
        public static KickingKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((KickingCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // Both averages exclude the anchor candle. Replay the longest window plus
    // that anchor before publishing; retain the original maximum-period route.
    private static int NeckWindow(IIndicator indicator)
    {
        int period = indicator switch
        {
            OnNeckCandle c => Math.Max(c.BodyPeriod, c.EqualPeriod),
            InNeckCandle c => Math.Max(c.BodyPeriod, c.EqualPeriod),
            ThrustingCandle c => Math.Max(c.BodyPeriod, c.EqualPeriod),
            _ => 0
        };
        return period is > 0 and < int.MaxValue - 1 ? period + 2 : 0;
    }

    private readonly struct NeckKernel(NeckCandleState state) : IBoundedKernel<NeckKernel>
    {
        public static NeckKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((NeckCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // The long-body window ends before the previous candle; P+1 seeds and
    // the current candle reconstruct both exact thresholds without older data.
    private static int ContainmentWindow(IIndicator indicator) => indicator switch
    {
        HaramiCandle { Period: < int.MaxValue - 1 } c => c.Period + 2,
        HaramiCrossCandle { Period: < int.MaxValue - 1 } c => c.Period + 2,
        HomingPigeonCandle { Period: < int.MaxValue - 1 } c => c.Period + 2,
        DojiStarCandle { Period: < int.MaxValue - 1 } c => c.Period + 2,
        _ => 0
    };

    private readonly struct ContainmentKernel(ContainmentCandleState state) : IBoundedKernel<ContainmentKernel>
    {
        public static ContainmentKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((ContainmentCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private static int CommodityReplayWindow(IIndicator indicator)
    {
        if (indicator is not WindowCommodityChannelIndex commodity) return 0;
        // P distances each use their own P-price mean: 2P-1 prices total.
        long size = commodity.RollingDeviations ? 2L * commodity.Period - 1 : commodity.Period;
        return size <= int.MaxValue ? (int)size : 0;
    }

    // P smoothed deviations each require P changes and their preceding price:
    // 2P prices in total, including the assigned output bar.
    private static int RelativeVolatilityWindow(IIndicator indicator) =>
        indicator is RelativeVolatilityStrength { Period: <= int.MaxValue / 2 } relative
            ? 2 * relative.Period : 0;

    private static int HullReplayWindow(IIndicator indicator)
    {
        if (indicator is not HullWindow hull) return 0;
        long size = (long)hull.Period + hull.RootPeriod - 1;
        return size <= int.MaxValue ? (int)size : 0;
    }

    private static int DetailedDeviationWindow(IIndicator indicator)
    {
        if (indicator is not StandardDeviationWithDetails deviation) return 0;
        long size = (long)deviation.Period + (deviation.SmoothingPeriod ?? 1) - 1;
        return size <= int.MaxValue ? (int)size : 0;
    }

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
        EndpointWeightedAverage { Period: <= 1073741824 } endpoint => endpoint.Period,
        FixedPeriodWma average => average.Period,
        FibonacciWeightedAverage average => average.Period,
        TriangularWindowAverage average => average.Period,
        ArnaudLegouxWindow average => average.Period,
        NormalizedConvolution convolution => convolution.Kernel.Count,
        GaussianWeightedAverage average => average.Period,
        SineWeightedAverage average => average.Period,
        WindowedSinc sinc => sinc.Taps,
        _ => 0
    };

    private static int BoundedParallelMinimum(IIndicator indicator)
    {
        if (ContainmentWindow(indicator) > 0 || NeckWindow(indicator) > 0 || KickingWindow(indicator) > 0 || MatchedLinesWindow(indicator) > 0) return 512;
        if (SequenceWindow(indicator) > 0) return 512;
        if (indicator is TriangularWindowAverage or WindowReturnBeta or WindowCorrelation or WindowPairStatistics or WindowCommodityChannelIndex or WindowMoneyFlowIndex or MoneyFlowWithDetails or VolumeWeightedPrice or WindowVortex or WindowUltimateOscillator or WindowChoppinessIndex) return 512;
        // Exact weighted recurrences still perform several accumulator updates
        // per bar. The 1k native-worker comparison justifies scheduling at 512.
        if (indicator is Wma or WilliamsR or EndpointWeightedAverage or FixedPeriodWma or FibonacciWeightedAverage or NormalizedConvolution { HasAffineKernel: true } or WindowRangeMidpoint or WindowPriceRange or WindowAroon or RollingPercentile or WindowMode or WindowDispersion or WindowSampleShape or StandardDeviationWithDetails or PriorPriceChannel or ClassicDeviationBands { Method: ClassicAverageMethod.Sma } or WindowDeviationBands or HullWindow or RateOfChangeWithValidity) return 512;
        // Fixed convolutions perform one exact product per coefficient per bar.
        // Keep the existing threshold for the other constant-work kernels; for convolutions
        // use an 8192-term work estimate and at least 512 bars for scheduling.
        int terms = indicator is NormalizedConvolution { HasAffineKernel: false } or GaussianWeightedAverage or SineWeightedAverage or WindowedSinc or ArnaudLegouxWindow
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
        private readonly double[] _window = period == 1 || period > count ? [] : new double[period];
        private ExactMeanAccumulator _sum;
        private Core.SmaCpuKernel.Certificate _proof = new(period);
        private double _running;
        private bool _certified = true;
        private int _position, _seen;
        public void Reset()
        {
            _position = _seen = 0; _sum = default; _running = 0;
            _proof = new(period); _certified = true;
        }
        public double Update(in Bar bar)
        {
            if (period > count) return 0;
            if (period == 1) return bar.Close == 0 ? 0 : bar.Close;
            if (_certified && !_proof.Include(bar.Close))
            {
                // Reconstruct from retained input, never from a rounded running
                // result. The new value has not been inserted or evicted yet.
                for (int i = 0; i < _seen; i++) _sum.Add(_window[i]);
                _certified = false;
            }
            if (_seen == period)
            {
                if (_certified) _running -= _window[_position];
                else _sum.Add(_window[_position], -1);
            }
            else _seen++;
            // Evict first so the certificate bounds at most P inputs.
            if (_certified) _running += bar.Close;
            else _sum.Add(bar.Close);
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            return _seen < period ? 0 : _certified ? _running == 0 ? 0 : _running : _sum.Mean(1);
        }
    }

    private sealed class WmaValueState(int period, int count, bool normalizeAvailable = false) : IIndicatorState
    {
        private readonly double[] _window = new double[Math.Min(period, Math.Max(1, count))];
        private readonly long _denominator = (long)period * (period + 1L) / 2;
        private ExactMeanAccumulator _sum, _weighted;
        private Core.SmaCpuKernel.Certificate _certificate = new(period <= 65535 ? (int)((long)period * (period + 1) / 2) : int.MaxValue);
        private bool _certified = period <= 65535;
        private double _fastSum, _fastWeighted;
        private int _position, _seen;
        public void Reset()
        {
            _position = _seen = 0; _sum = _weighted = default;
            _fastSum = _fastWeighted = 0;
            _certified = period <= 65535;
            _certificate = new(_certified ? (int)_denominator : int.MaxValue);
        }
        public double Update(in Bar bar)
        {
            if (period == 1) return bar.Close == 0 ? 0 : bar.Close;
            if (_certified && !_certificate.Include(bar.Close))
            {
                // Previous totals were exact doubles. Promote them once before
                // including the first value outside the common-grid certificate.
                _sum.Add(_fastSum);
                _weighted.Add(_fastWeighted);
                _certified = false;
            }
            // Match MovingAverageCore's fixed denominator, including zero-padded
            // startup. Capacity is bounded by the observations this execution owns.
            if (_certified)
            {
                // Subtraction first lowers the total weight to T-P; adding the
                // newest P-weighted price restores T. No intermediate rounds.
                _fastWeighted -= _fastSum;
                _fastWeighted += bar.Close * period;
                _fastSum += bar.Close;
                if (_seen >= period) _fastSum -= _window[_position];
            }
            else
            {
                _weighted.Subtract(_sum);
                _weighted.Add(bar.Close, period);
                _sum.Add(bar.Close);
                if (_seen >= period) _sum.Add(_window[_position], -1);
            }
            if (_seen < period) _seen++;
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            long denominator = normalizeAvailable ? (long)_seen * (2L * period - _seen + 1) / 2 : _denominator;
            return _certified ? _fastWeighted == 0 ? 0 : _fastWeighted / denominator : _weighted.Mean(denominator);
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
        public static WmaKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, count, indicator is FixedPeriodWma));
        public double Update(in Bar bar) => state.Update(in bar);
    }
    private struct TriangularKernel(TriangularValueState state) : IBoundedKernel<TriangularKernel>
    {
        public static TriangularKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period, count));
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

    // Exact moments remain equivalent on any binary grid, so worker state
    // need not retain a finer grid from observations before its seed window.
    private readonly struct DispersionKernel(WindowDispersion.State state) : IBoundedKernel<DispersionKernel>
    {
        public static DispersionKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var dispersion = (WindowDispersion)indicator;
            return new(new(period, dispersion.Output, dispersion.Sample, dispersion.Multiplier));
        }
        public double Update(in Bar bar) => state.Update(in bar);
    }

    // Exact modal means ignore dictionary ordering, including signed-zero keys.
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

    private readonly struct RelativeVolatilityKernel(RelativeVolatilityStrength.State state) : IBoundedKernel<RelativeVolatilityKernel>
    {
        public static RelativeVolatilityKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(((RelativeVolatilityStrength)indicator).Period));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private struct UlcerKernel(WindowUlcerIndex.State state, BoundedOutput target) : IBoundedKernel<UlcerKernel>
    {
        private int _index = target.Origin;
        public static UlcerKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period), target);
        public double Update(in Bar bar)
        {
            Span<double> scratch = stackalloc double[2];
            state.Update(in bar, scratch);
            int index = _index++;
            // Seed bars initialize the cached exact window but belong to another worker.
            if (index >= target.WriteStart) target.Values[1][index] = scratch[1];
            return scratch[0];
        }
    }

    private struct SampleShapeKernel(WindowSampleShape.State state, BoundedOutput target) : IBoundedKernel<SampleShapeKernel>
    {
        private readonly double[] _scratch = new double[2];
        private int _index = target.Origin;
        public static SampleShapeKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, ((WindowSampleShape)indicator).Kurtosis), target);
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = _scratch[1];
            return _scratch[0];
        }
    }

    private struct HullKernel(HullWindow.State state, BoundedOutput target) : IBoundedKernel<HullKernel>
    {
        private readonly double[] _scratch = new double[2];
        private int _index = target.Origin;
        public static HullKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var hull = (HullWindow)indicator;
            return new(new(hull.Period, hull.HalfPeriod, hull.RootPeriod,
                hull.Convention == HullWindowConvention.ExpandingFloor), target);
        }
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            // Only the primary value can overflow; the secondary is a presence bit.
            // Seeded synthetic prefixes have expired before the first published bar.
            if (index >= target.WriteStart) target.Values[1][index] = _scratch[1];
            return _scratch[0];
        }
    }

    private struct PriorChannelKernel(PriorPriceChannel.State state, BoundedOutput target) : IBoundedKernel<PriorChannelKernel>
    {
        private readonly double[] _scratch = new double[8];
        private int _index = target.Origin;
        public static PriorChannelKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(((PriorPriceChannel)indicator).Period), target);
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < _scratch.Length; slot++) target.Values[slot][index] = _scratch[slot];
            return _scratch[0];
        }
    }

    private struct ClassicBandsKernel(ClassicDeviationBands.SmaState state, BoundedOutput target) : IBoundedKernel<ClassicBandsKernel>
    {
        private readonly double[] _scratch = new double[6];
        private int _index = target.Origin;
        public static ClassicBandsKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new((ClassicDeviationBands)indicator), target);
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < _scratch.Length; slot++) target.Values[slot][index] = _scratch[slot];
            return _scratch[0];
        }
    }

    private struct DeviationBandsKernel(WindowDeviationBands.State state, BoundedOutput target) : IBoundedKernel<DeviationBandsKernel>
    {
        private readonly double[] _scratch = new double[12];
        private int _index = target.Origin;
        public static DeviationBandsKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new((WindowDeviationBands)indicator), target);
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < _scratch.Length; slot++) target.Values[slot][index] = _scratch[slot];
            return _scratch[0];
        }
    }

    private struct DetailedDeviationKernel(StandardDeviationWithDetails.State state, BoundedOutput target) : IBoundedKernel<DetailedDeviationKernel>
    {
        private readonly double[] _scratch = new double[8];
        private int _index = target.Origin;
        public static DetailedDeviationKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var deviation = (StandardDeviationWithDetails)indicator;
            return new(new(deviation.Period, deviation.SmoothingPeriod), target);
        }
        public double Update(in Bar bar)
        {
            state.Update(in bar, _scratch);
            int index = _index++;
            // Population deviation is bounded by max(abs(price)); mean and its
            // smoothing are convex means, and abs(Z) <= sqrt(P-1). All secondary
            // outputs stay finite. Seeds initialize both windows without publishing.
            if (index >= target.WriteStart)
                for (int slot = 1; slot < _scratch.Length; slot++) target.Values[slot][index] = _scratch[slot];
            return _scratch[0];
        }
    }

    private sealed class PathRatioValueState(int period, int count, PathRatioConvention convention)
    {
        private readonly double[] _prices = period < count && period <= int.MaxValue / 2 ? new double[period] : [];
        private Core.SmaCpuKernel.Certificate _proof = new((int)Math.Min(int.MaxValue,
            Math.Max(2L * period, convention == PathRatioConvention.SignedPercent ? 200 : 2)));
        private WindowPathRatio.Calculator? _exact;
        private int _position, _seen;
        private double _path, _previous;

        internal double? Next(double value)
        {
            if (period >= count) return null;
            if (_exact is not null) return _exact.Next(value);
            if (period > int.MaxValue / 2 || !_proof.Include(value))
            {
                // Replay retained raw prices in order. They reconstruct P-1
                // changes exactly, including after a certified flat window.
                _exact = new(period, convention);
                int first = _seen == period ? _position : 0;
                for (int i = 0; i < _seen; i++)
                    _exact.Next(_prices[(int)(((long)first + i) % period)]);
                return _exact.Next(value);
            }
            if (_seen != 0) _path += Math.Abs(value - _previous);
            bool mature = _seen == period;
            double? result = null;
            if (mature)
            {
                double oldest = _prices[_position];
                if (_path != 0)
                {
                    double net = value - oldest;
                    double numerator = convention == PathRatioConvention.SignedPercent ? net * 100 : Math.Abs(net);
                    result = numerator == 0 ? 0 : numerator / _path;
                }
                // Publish P changes, then evict to keep every intermediate
                // bounded by 2P price terms (200 for the signed numerator).
                double next = period == 1 ? value : _prices[_position + 1 == period ? 0 : _position + 1];
                _path -= Math.Abs(next - oldest);
            }
            else _seen++;
            _prices[_position] = value;
            if (++_position == period) _position = 0;
            _previous = value;
            return result;
        }
    }

    private struct PathRatioKernel(PathRatioValueState state, BoundedOutput target) : IBoundedKernel<PathRatioKernel>
    {
        private int _index = target.Origin;
        public static PathRatioKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var ratio = (WindowPathRatio)indicator;
            return new(new(ratio.Period, count, ratio.Convention), target);
        }
        public double Update(in Bar bar)
        {
            var value = state.Next(bar.Close);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = value.HasValue ? 1 : 0;
            return value ?? 0;
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

    private readonly struct EndpointKernel(AffineConvolutionState state) : IBoundedKernel<EndpointKernel>
    {
        public static EndpointKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var endpoint = (EndpointWeightedAverage)indicator;
            // Fixed endpoint weights are 2P-1, 2P-4, ...; the bounded eligibility
            // guarantees that the newest weight fits Int32 without truncation.
            return new(new(period, (int)(2L * period - 1), -3, endpoint.AverageDuringWarmup));
        }
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private readonly struct StateWindowKernel(IIndicatorState state) : IBoundedKernel<StateWindowKernel>
    {
        public static StateWindowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((IIndicatorState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private struct ChoppinessKernel(WindowChoppinessIndex.CompactState state, BoundedOutput target) : IBoundedKernel<ChoppinessKernel>
    {
        private int _index = target.Origin;
        public static ChoppinessKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(((WindowChoppinessIndex)indicator).Period, count), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct UltimateKernel(WindowUltimateOscillator.CompactState state, BoundedOutput target) : IBoundedKernel<UltimateKernel>
    {
        private int _index = target.Origin;
        public static UltimateKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var ultimate = (WindowUltimateOscillator)indicator;
            return new(new([ultimate.ShortPeriod, ultimate.MiddlePeriod, ultimate.LongPeriod], ultimate.ZeroRangeContribution, count), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct VortexKernel(WindowVortex.CompactState state, BoundedOutput target) : IBoundedKernel<VortexKernel>
    {
        private int _index = target.Origin;
        public static VortexKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(((WindowVortex)indicator).Period, count), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[4];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }

    private struct VolumePriceKernel(VolumeWeightedPrice.CompactState state, BoundedOutput target) : IBoundedKernel<VolumePriceKernel>
    {
        private int _index = target.Origin;
        public static VolumePriceKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period, ((VolumeWeightedPrice)indicator).TypicalPrice, null), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct CmfKernel(MoneyFlowWithDetails.CompactState state, BoundedOutput target) : IBoundedKernel<CmfKernel>
    {
        private int _index = target.Origin;
        public static CmfKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(period), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[4];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }

    private struct MoneyFlowKernel(WindowMoneyFlowIndex.CompactState state, BoundedOutput target) : IBoundedKernel<MoneyFlowKernel>
    {
        private int _index = target.Origin;
        public static MoneyFlowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var flow = (WindowMoneyFlowIndex)indicator;
            // Local change counts begin at Origin. Suppression is global, so
            // only the still-unelapsed portion applies to this worker's seed.
            return new(new(flow.Period, flow.MinimumTotalOne, Math.Max(0, flow.SuppressedBars - target.Origin), count), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct CommodityKernel(WindowCommodityChannelIndex.CompactState state, BoundedOutput target) : IBoundedKernel<CommodityKernel>
    {
        private int _index = target.Origin;
        public static CommodityKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var commodity = (WindowCommodityChannelIndex)indicator;
            return new(new(commodity.Period, commodity.RollingDeviations, commodity.FlatZero, count), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct CorrelationKernel(WindowCorrelation.State state, BoundedOutput target) : IBoundedKernel<CorrelationKernel>
    {
        private int _index = target.Origin;
        public static CorrelationKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var correlation = (WindowCorrelation)indicator;
            return new(new(correlation.Period, correlation.Left, correlation.Right, correlation.FlatZero), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct PairStatisticsKernel(WindowPairStatistics.State state, BoundedOutput target) : IBoundedKernel<PairStatisticsKernel>
    {
        private int _index = target.Origin;
        public static PairStatisticsKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var pair = (WindowPairStatistics)indicator;
            return new(new(pair.Period, pair.Left, pair.Right), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[10];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }

    private struct ReturnBetaKernel(WindowReturnBeta.State state, BoundedOutput target) : IBoundedKernel<ReturnBetaKernel>
    {
        private int _index = target.Origin;
        public static ReturnBetaKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var beta = (WindowReturnBeta)indicator;
            return new(new(beta.Period, beta.Market, beta.Evaluation, Math.Min(beta.Period, Math.Max(0, count - 1))), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct AlmaKernel(ArnaudLegouxWindow.State state, BoundedOutput target) : IBoundedKernel<AlmaKernel>
    {
        private int _index = target.Origin;
        public static AlmaKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var alma = (ArnaudLegouxWindow)indicator;
            // Later workers never publish incomplete seed windows. Suppressing
            // those values also avoids rebuilding every expanding weight prefix.
            return new(new(alma.Period, alma.Offset, alma.Sigma,
                alma.FullWindowOnly || target.WriteStart > 0), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            // P-1 raw prices seed each worker. Their presence outputs belong
            // to its predecessor; the first owned row has a complete window.
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private static Bar FillBoundedWindow(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator switch
    {
        HighestHigh high when high.Length >= source.Length => FillBoundedWindow<PrefixHighKernel>(source, output, indicator, high.Length, cancellation, owned),
        LowestLow low when low.Length >= source.Length => FillBoundedWindow<PrefixLowKernel>(source, output, indicator, low.Length, cancellation, owned),
        HighestHigh high => FillBoundedWindow<HighKernel>(source, output, indicator, high.Length, cancellation, owned),
        LowestLow low => FillBoundedWindow<LowKernel>(source, output, indicator, low.Length, cancellation, owned),
        Wma average => FillBoundedWindow<WmaKernel>(source, output, indicator, Math.Max(1, average.Length), cancellation, owned),
        FixedPeriodWma average => FillBoundedWindow<WmaKernel>(source, output, indicator, average.Period, cancellation, owned),
        TriangularWindowAverage average => FillBoundedWindow<TriangularKernel>(source, output, indicator, average.Period, cancellation, owned),
        ArnaudLegouxWindow average => FillBoundedWindow<AlmaKernel>(source, output, indicator, average.Period, cancellation, owned),
        // P returns need P preceding prices and the current output price.
        WindowReturnBeta { Period: < int.MaxValue } beta =>
            FillBoundedWindow<ReturnBetaKernel>(source, output, indicator, beta.Period + 1, cancellation, owned),
        WindowChoppinessIndex { Period: < int.MaxValue } chop =>
            FillBoundedWindow<ChoppinessKernel>(source, output, indicator, chop.Period + 1, cancellation, owned),
        WindowUltimateOscillator { LongPeriod: < int.MaxValue } ultimate =>
            FillBoundedWindow<UltimateKernel>(source, output, indicator, ultimate.LongPeriod + 1, cancellation, owned),
        WindowVortex { Period: < int.MaxValue } vortex =>
            FillBoundedWindow<VortexKernel>(source, output, indicator, vortex.Period + 1, cancellation, owned),
        WindowMoneyFlowIndex { Period: < int.MaxValue } flow =>
            FillBoundedWindow<MoneyFlowKernel>(source, output, indicator, flow.Period + 1, cancellation, owned),
        MoneyFlowWithDetails cmf => FillBoundedWindow<CmfKernel>(source, output, indicator, cmf.Period, cancellation, owned),
        WindowCorrelation correlation => FillBoundedWindow<CorrelationKernel>(source, output, indicator, correlation.Period, cancellation, owned),
        WindowPairStatistics pair => FillBoundedWindow<PairStatisticsKernel>(source, output, indicator, pair.Period, cancellation, owned),
        WindowCommodityChannelIndex commodity when CommodityReplayWindow(commodity) > 0 =>
            FillBoundedWindow<CommodityKernel>(source, output, indicator, CommodityReplayWindow(commodity), cancellation, owned),
        WilliamsR range => FillBoundedWindow<WilliamsKernel>(source, output, indicator, Math.Max(1, range.Length), cancellation, owned),
        WindowLinearRegression regression => FillBoundedWindow<RegressionKernel>(source, output, indicator, regression.Period, cancellation, owned),
        // P changes require P preceding prices plus the assigned output bar.
        WindowPathRatio { Period: < int.MaxValue } ratio =>
            FillBoundedWindow<PathRatioKernel>(source, output, indicator, ratio.Period + 1, cancellation, owned),
        RollingPriceSum sum => FillBoundedWindow<SumKernel>(source, output, indicator, sum.Period, cancellation, owned),
        WindowAroon when AroonWindow(indicator) > 0 => FillBoundedWindow<AroonKernel>(source, output, indicator, AroonWindow(indicator), cancellation, owned),
        RollingPercentile { HasFractionalFullRank: true } percentile => FillBoundedWindow<PercentileKernel>(source, output, indicator, percentile.Period, cancellation, owned),
        RateOfChangeWithValidity { Period: < int.MaxValue } change => FillBoundedWindow<ValidReturnKernel>(source, output, indicator, change.Period + 1, cancellation, owned),
        HullWindow when HullReplayWindow(indicator) > 0 =>
            FillBoundedWindow<HullKernel>(source, output, indicator, HullReplayWindow(indicator), cancellation, owned),
        PriorPriceChannel { Period: < int.MaxValue } channel =>
            FillBoundedWindow<PriorChannelKernel>(source, output, indicator, channel.Period + 1, cancellation, owned),
        ClassicDeviationBands { Method: ClassicAverageMethod.Sma } bands =>
            FillBoundedWindow<ClassicBandsKernel>(source, output, indicator, bands.Period, cancellation, owned),
        WindowDeviationBands bands =>
            FillBoundedWindow<DeviationBandsKernel>(source, output, indicator, bands.Period, cancellation, owned),
        StandardDeviationWithDetails when DetailedDeviationWindow(indicator) > 0 =>
            FillBoundedWindow<DetailedDeviationKernel>(source, output, indicator, DetailedDeviationWindow(indicator), cancellation, owned),
        WindowSampleShape shape => FillBoundedWindow<SampleShapeKernel>(source, output, indicator, shape.Period, cancellation, owned),
        RelativeVolatilityStrength when RelativeVolatilityWindow(indicator) > 0 =>
            FillBoundedWindow<RelativeVolatilityKernel>(source, output, indicator, RelativeVolatilityWindow(indicator), cancellation, owned),
        WindowUlcerIndex ulcer => FillBoundedWindow<UlcerKernel>(source, output, indicator, ulcer.Period, cancellation, owned),
        WindowDispersion dispersion => FillBoundedWindow<DispersionKernel>(source, output, indicator, dispersion.Period, cancellation, owned),
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
        EndpointWeightedAverage { Period: <= 1073741824 } endpoint =>
            FillBoundedWindow<EndpointKernel>(source, output, indicator, endpoint.Period, cancellation, owned),
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
        _ when ContainmentWindow(indicator) > 0 =>
            FillBoundedWindow<ContainmentKernel>(source, output, indicator, ContainmentWindow(indicator), cancellation, owned),
        _ when NeckWindow(indicator) > 0 =>
            FillBoundedWindow<NeckKernel>(source, output, indicator, NeckWindow(indicator), cancellation, owned),
        _ when KickingWindow(indicator) > 0 =>
            FillBoundedWindow<KickingKernel>(source, output, indicator, KickingWindow(indicator), cancellation, owned),
        _ when MatchedLinesWindow(indicator) > 0 =>
            FillBoundedWindow<MatchedLinesKernel>(source, output, indicator, MatchedLinesWindow(indicator), cancellation, owned),
        ThreeOutsideCandle =>
            FillBoundedWindow<ThreeOutsideKernel>(source, output, indicator, 4, cancellation, owned),
        _ when SequenceWindow(indicator) > 0 =>
            FillBoundedWindow<SequenceKernel>(source, output, indicator, SequenceWindow(indicator), cancellation, owned),
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
        if (typeof(T) == typeof(MoneyFlowKernel))
            return ComputeMoneyFlowColumn(source, output, owned, start, end, (WindowMoneyFlowIndex)indicator, seed, tail, cancellation);
        if (typeof(T) == typeof(UltimateKernel))
            return ComputeUltimateColumn(source, output, owned, start, end, (WindowUltimateOscillator)indicator, seed, tail, cancellation);
        if (typeof(T) == typeof(ChoppinessKernel))
            return ComputeChoppinessColumn(source, output, owned, start, end, period - 1, seed, tail, cancellation);
        if (typeof(T) == typeof(VortexKernel))
            return ComputeVortexColumn(source, output, owned, start, end, period - 1, seed, tail, cancellation);
        if (typeof(T) == typeof(TriangularKernel) && period is >= 2 and <= 4096 && end - start + seed.Length >= 512)
            return ComputeTriangularColumn(source, output[0], owned, start, end, period, seed, tail, cancellation);
        if ((typeof(T) == typeof(CorrelationKernel) || typeof(T) == typeof(PairStatisticsKernel))
            && period <= 4096 && end - start + seed.Length is >= 512 and <= int.MaxValue / 2)
            return ComputePairColumn(source, output, owned, start, end, period, seed, tail, cancellation, indicator);
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
