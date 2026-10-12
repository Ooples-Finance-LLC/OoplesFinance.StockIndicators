#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int BoundedPeriod(IIndicator indicator) => indicator switch
    {
        HighestHigh high => high.Length,
        LowestLow low => low.Length,
        Wma average => Math.Max(1, average.Length),
        WilliamsR range => Math.Max(1, range.Length),
        RollingPriceSum sum => sum.Period,
        RelativeVolatilityStrength => RelativeVolatilityWindow(indicator),
        RollingPercentile { HasFractionalFullRank: true } percentile => percentile.Period,
        WindowDispersion dispersion => dispersion.Period,
        WindowMode mode => mode.Period,
        WindowPriceRange range => range.Period,
        WindowRangeMidpoint midpoint => midpoint.Period,
        WindowExtremeIndex index => index.Period,
        DecayingWindowExtreme { Decay: 0 } extreme => extreme.Period,
        LaggedPriceChange change => change.Period + 1,
        EngulfingPattern => 3,
        _ when BodyShadowPeriod(indicator) is > 0 and < int.MaxValue => BodyShadowPeriod(indicator) + 1,
        _ when WeightedWindowPeriod(indicator) > 0 => WeightedWindowPeriod(indicator),
        _ when PercentileCandlePeriod(indicator) > 0 => PercentileCandlePeriod(indicator),
        _ when TrendReversalWindow(indicator) > 0 => TrendReversalWindow(indicator),
        _ when HaramiWindow(indicator) > 0 => HaramiWindow(indicator),
        _ when TasukiWindow(indicator) > 0 => TasukiWindow(indicator),
        _ => throw new InvalidOperationException("Unqualified bounded-window batch.")
    };

    private static IIndicatorState CreateBoundedState(IIndicator indicator, int count, int origin) => indicator switch
    {
        HighestHigh high when high.Length >= count => new PrefixPriceExtremeState(true),
        LowestLow low when low.Length >= count => new PrefixPriceExtremeState(false),
        HighestHigh high => new PriceExtremeState(high.Length, count, true),
        LowestLow low => new PriceExtremeState(low.Length, count, false),
        Wma average => new WmaValueState(Math.Max(1, average.Length), count),
        FixedPeriodWma average => new WmaValueState(average.Period, count, true),
        WilliamsR range => new WilliamsValueState(Math.Max(1, range.Length), count),
        RollingPriceSum sum => new SumValueState(sum.Period, count),
        RelativeVolatilityStrength relative => new RelativeVolatilityStrength.State(relative.Period),
        RollingPercentile { HasFractionalFullRank: true } percentile => new RollingPercentile.State(percentile.Period, percentile.Percentile, percentile.AverageDuringWarmup),
        WindowDispersion dispersion => new WindowDispersion.State(dispersion.Period, dispersion.Output, dispersion.Sample, dispersion.Multiplier),
        WindowMode mode => new WindowMode.State(mode.Period),
        WindowPriceRange range => new WindowPriceRange.State(range.Period),
        WindowRangeMidpoint midpoint => (IIndicatorState)midpoint.CreateState(),
        WindowExtremeIndex { Period: <= 32 } index => new SmallExtremeIndexState(index.Period, index.Maximum, origin),
        WindowExtremeIndex index => new ExtremeIndexState(index.Period, index.Maximum, origin),
        DecayingWindowExtreme { Decay: 0 } extreme => new CloseExtremeState(extreme.Period, extreme.Maximum),
        LaggedPriceChange change => new LaggedValueState(change.Period, count, change.Kind),
        EngulfingPattern => new EngulfingPattern.State(),
        _ when BodyShadowPeriod(indicator) is > 0 and < int.MaxValue =>
            (BodyShadowCandleState)((IndicatorBase)indicator).CreateState()!,
        TrendStarPattern when TrendReversalWindow(indicator) > 0 =>
            (TrendStarState)((IndicatorBase)indicator).CreateState()!,
        _ when TrendReversalWindow(indicator) > 0 =>
            (TrendBabyState)((IndicatorBase)indicator).CreateState()!,
        _ when HaramiWindow(indicator) > 0 =>
            (StrictHaramiState)((IndicatorBase)indicator).CreateState()!,
        _ when TasukiWindow(indicator) > 0 =>
            (TrendTasukiState)((IndicatorBase)indicator).CreateState()!,
        _ when PercentileCandlePeriod(indicator) > 0 =>
            (PercentileCandleState)((IndicatorBase)indicator).CreateState()!,
        _ when WeightedWindowPeriod(indicator) > 0 =>
            (IIndicatorState)((IndicatorBase)indicator).CreateState()!,
        _ => throw new InvalidOperationException("Unqualified bounded-window batch.")
    };

    private readonly record struct BoundedFailure(int Index, double Value);
    private sealed class BoundedBatchRegion(int count)
    {
        internal PointwiseRegion Input;
        internal readonly BoundedFailure[] Outputs = new BoundedFailure[count];
    }

    private static Bar FillBoundedBatch(Bar[] source, List<Node> nodes,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        cancellation.ThrowIfCancellationRequested();
        int period = nodes.Max(n => BoundedPeriod(n.Indicator));
        int chunks = period <= 4096 ? Math.Min(WorkerCount(), source.Length / period) : 1;
        BoundedBatchRegion[] regions;
        int parallelMinimum = nodes.Min(n => BoundedParallelMinimum(n.Indicator));
        if (chunks < 2 || !CanParallelize(source.Length, parallelMinimum) || !Monitor.TryEnter(ParallelBarGate))
            regions = [ComputeBoundedBatch(source, nodes, owned, 0, source.Length, [], [], cancellation)];
        else
        {
            Bar[][]? tails = null;
            try
            {
                tails = new Bar[chunks - 1][];
                regions = new BoundedBatchRegion[chunks];
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
                    regions[chunk] = ComputeBoundedBatch(source, nodes, owned, start, end,
                        chunk == 0 ? [] : tails[chunk - 1].AsSpan(0, period - 1),
                        chunk == chunks - 1 ? [] : tails[chunk].AsSpan(0, period - 1), cancellation);
                });
            }
            finally
            {
                if (tails is not null)
                    foreach (var tail in tails)
                        if (tail is { Length: > 0 }) System.Buffers.ArrayPool<Bar>.Shared.Return(tail);
                Monitor.Exit(ParallelBarGate);
            }
        }
        cancellation.ThrowIfCancellationRequested();
        foreach (var region in regions) ValidatePointwiseInput(in region.Input);
        // Match the ordinary publication order, not the earliest failure across
        // all indicators. Every input error still precedes every output error.
        for (int node = 0; node < nodes.Count; node++)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var region in regions)
            {
                var failure = region.Outputs[node];
                if (!double.IsFinite(failure.Value))
                    Validation.IndicatorOutputPolicy.Validate(nodes[node].Indicator, 0, failure.Index, failure.Value);
            }
        }
        return regions[^1].Input.Last;
    }

    private static BoundedBatchRegion ComputeBoundedBatch(Bar[] source, List<Node> nodes, OwnedBarBuffer? owned,
        int start, int end, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail, CancellationToken cancellation)
    {
        var region = new BoundedBatchRegion(nodes.Count);
        int count = end - start + seed.Length;
        int origin = start - seed.Length;
        var states = nodes.Select(n => CreateBoundedState(n.Indicator, count, origin)).ToArray();
        // All states have bounded memory and exact eviction. Feeding the shared
        // longest tail leaves shorter windows with the same final state.
        foreach (ref readonly var bar in seed)
        {
            if (cancellation.IsCancellationRequested) return region;
            if (!AllFieldsFinite(in bar))
            {
                region.Input = new PointwiseRegion { InvalidInput = true, Last = bar };
                return region;
            }
            foreach (var state in states) state.Update(in bar);
        }
        int tailStart = end - tail.Length;
        for (int i = start; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) return region;
            var bar = i >= tailStart ? tail[i - tailStart] : source[i];
            region.Input.Last = bar;
            if (!AllFieldsFinite(in bar)) { region.Input.InvalidInput = true; break; }
            if (owned is not null) owned[i] = bar;
            for (int node = 0; node < states.Length; node++)
            {
                double value = states[node].Update(in bar);
                nodes[node].Values[0][i] = value;
                if (!double.IsFinite(value) && double.IsFinite(region.Outputs[node].Value))
                    region.Outputs[node] = new BoundedFailure(i, value);
            }
        }
        return region;
    }
}
#endif
