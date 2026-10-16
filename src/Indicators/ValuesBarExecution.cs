#if !NETFRAMEWORK
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

// Internal facade plan: no public field masks, array counts or kernel selection.
// Only sealed, independently qualified states enter this route. Other graphs use
// the existing builder automatically. Input bars are consumed once into local values.
internal static partial class ValuesBarExecution
{
    private static readonly object ParallelBarGate = new();
    internal const int AsinParallelMinimum = 512;
    // These sealed states publish their own outputs without graph or built-in
    // evaluator substitution. Variable SMA's documented pure selector runs once
    // per published bar; deferred node failures retain raw-input priority.
    private static bool IsSharedState(IIndicator indicator) => indicator.Source is null && indicator.Components.Count == 0
        && (SharedCpuStates.Contains(indicator) || IsGeneratedState(indicator)
            || indicator is VariablePeriodClassicAverage { Method: ClassicAverageMethod.Sma });

    internal static bool SupportsOwned(IReadOnlyList<IIndicator> indicators) =>
        IsWeightedChain(indicators) || IsCurvatureChain(indicators) || indicators.Count > 0 && indicators.All(IsPointwise)
        || indicators.Count > 0 && indicators.All(IsSharedState);

    internal static bool SupportsGpu(IReadOnlyList<IIndicator> indicators) => indicators.Count == 1
        && indicators[0].Source is null && indicators[0].Components.Count == 0
        && (indicators[0] is Wma or FixedPeriodWma or TriangularWindowAverage or EndpointWeightedAverage or WindowLinearRegression { Output: not WindowRegressionOutput.Angle } or NormalizedConvolution { HasAffineKernel: true } or RollingPriceSum or WindowPathRatio or WindowVortex or WindowUltimateOscillator or WindowMoneyFlowIndex or TrueRange or CandleArithmetic or PriceRoundingTransform or EngulfingPattern or BullishCandle or BearishCandle or WindowRangeMidpoint or WindowPriceRange or HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or WindowExtremeIndex or WindowAroon or DecayingWindowExtreme { Decay: 0 }
            || indicators[0] is SeededOnBalanceVolume or AccumulationDistributionWithAverage or SeededForceIndex or MoneyFlowWithDetails or VolumeWeightedPrice
            || indicators[0] is SeededAverageTrueRange or NormalizedSeededAverageTrueRange or AverageTrueRangeWithDetails
            || indicators[0] is ScaledTrueRange
            || indicators[0] is WindowRegressionStatistics or RegressionWithFinalLine
            || IsGpuLagged(indicators[0]) || IsGpuCandleWindow(indicators[0]) || IsGpuPercentile(indicators[0]))
        || IsGpuIndexPair(indicators);

    private static bool IsGpuIndexPair(IReadOnlyList<IIndicator> indicators) => indicators.Count == 2
        && indicators[0] is WindowExtremeIndex first && indicators[1] is WindowExtremeIndex second
        && first.Source is null && second.Source is null && first.Components.Count == 0 && second.Components.Count == 0
        && first.Period == second.Period && first.Maximum != second.Maximum;

    internal static bool IsGpuPercentile(IIndicator indicator) => indicator is RollingPercentile
        { Percentile: .25 or .5 or .75, Period: > 0 and <= 64 } percentile
        // Integer full-window ranks expose persistent equal-zero insertion order.
        && (percentile.Period == 1 || percentile.HasFractionalFullRank);

    internal static bool IsGpuCandleWindow(IIndicator indicator) => indicator is
        StrictHaramiCandle or BullishHaramiPattern or BearishHaramiPattern or UpsideTasukiGapPattern or DownsideTasukiGapPattern;

    internal static bool IsGpuLagged(IIndicator indicator) => indicator is LaggedPriceChange
        { Kind: PriceChangeKind.Difference or PriceChangeKind.Gain or PriceChangeKind.Loss or PriceChangeKind.Ratio };

    internal static bool Supports(IReadOnlyList<IIndicator> indicators) =>
        SupportsOwned(indicators) || indicators.All(i => IsSharedState(i) ||
        i.Source is null && i.Components.Count == 0 && i is Sma or JurikAdaptive or ScaledTrueRange
            or RollingPivotLevels or RetrospectiveFractals or RickshawManCandle or BullishShortBodyCandle
            or PriceCircularTransform { Operation: PriceCircularOperation.ArcSine });

    internal static IIndicatorRun Execute(Bar[] source, IReadOnlyList<IIndicator> indicators, CancellationToken cancellation,
        OwnedBarHistory? history = null, TensorsGpuExecution? gpu = null)
    {
        if (gpu is not null && (!SupportsGpu(indicators) || source.Length == 0))
            throw new NotSupportedException("Required GPU pointwise execution needs a nonempty supported indicator configuration.");
        if (IsWeightedChain(indicators))
            return ExecuteWeightedChain(source, (FixedPeriodWma)indicators[0], cancellation, history);
        if (IsCurvatureChain(indicators))
            return ExecuteCurvatureChain(source, (WindowRegressionStatistics)indicators[0], cancellation, history);
        var nodes = new List<Node>(indicators.Count);
        try
        {
            if (history is not null && !SupportsOwned(indicators))
                throw new InvalidOperationException("Unqualified owned values execution plan.");
            var owned = history is null ? null : new OwnedBarBuffer(source.Length);
            bool boundedBatch = indicators.Count > 1 && indicators.All(i => i.Outputs.Count == 1 && IsSharedState(i) && IsBoundedWindowIndicator(i));
            bool pointwiseBatch = indicators.Count > 1 && indicators.All(IsPointwise);
            bool volumeFlowCustomRange = indicators.Any(i => i is VolumeFlowIndicator)
                && UsesVolumeFlowCustomRange(source, cancellation);
            if (indicators.Count == 1)
                nodes.Add(new Node(indicators[0], source.Length, true, false, false, volumeFlowCustomRange));
            else
                foreach (var indicator in indicators.Distinct(IndicatorIdentity.Comparer))
                    nodes.Add(new Node(indicator, source.Length, false, boundedBatch, pointwiseBatch, volumeFlowCustomRange));
            // The guarded batch SMA contract needs replayable closes, not OHLCV
            // history. Multiple SMA periods share this one temporary input column.
            bool singleSma = nodes.Count == 1 && nodes[0].Indicator is Sma;
            double[]? close = singleSma ? nodes[0].Values[0]
                : nodes.Any(n => n.Indicator is Sma) ? new double[source.Length] : null;
            var summary = new Core.SmaCpuKernel.GridSummary();
            var positiveRange = new Core.SmaCpuKernel.PositiveRangeSummary();
            Bar latest = default;
            bool fusedSma = false, fusedFinite = false, obvFinite = false;
            if (singleSma)
            {
                int period = Math.Max(1, ((Sma)nodes[0].Indicator).Length);
                fusedSma = TryExecuteSmaParallel(source, close!, period, cancellation,
                    out latest, out fusedFinite, out _);
                if (!fusedSma)
                    latest = FillSmaColumn(source, close!, period, cancellation, out summary, out positiveRange);
            }
            else if (nodes.Count == 1 && (IsPointwise(nodes[0].Indicator) || gpu is not null))
                latest = gpu is null ? FillPointwise(source, nodes[0].Values, nodes[0].Indicator, cancellation, owned)
                    : gpu.ExecutePointwise(source, nodes[0].Values, nodes[0].Indicator, cancellation, owned);
            else if (gpu is not null && IsGpuIndexPair(indicators))
            {
                var minimum = nodes.First(n => !((WindowExtremeIndex)n.Indicator).Maximum);
                var maximum = nodes.First(n => ((WindowExtremeIndex)n.Indicator).Maximum);
                latest = gpu.ExecutePointwise(source, [minimum.Values[0], maximum.Values[0]],
                    minimum.Indicator, cancellation, owned, maximum.Indicator);
            }
            else if (pointwiseBatch)
                latest = FillPointwiseBatch(source, nodes, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is TrueRange)
                latest = FillTrueRange(source, nodes[0].Values[0], nodes[0].Indicator, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is ScaledTrueRange)
                latest = FillBoundedWindow<ScaledRangeKernel>(source, nodes[0].Values, nodes[0].Indicator, 2, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is BalanceOfPower power)
                latest = FillBalanceOfPower(source, nodes[0].Values, power, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is ClassicMacd macd && ClassicMacdWindow(macd) > 0)
                latest = FillClassicMacd(source, nodes[0].Values, macd, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is ClassicStochastic stochastic && ClassicStochasticWindow(stochastic) > 0)
                latest = FillClassicStochastic(source, nodes[0].Values, stochastic, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is RateOfChangeWithAverage roc && RateOfChangeWindow(roc) > 0)
                latest = FillRateOfChange(source, nodes[0].Values, roc, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is BalanceOfPowerWithValidity { Period: <= 4096 } bop)
                latest = FillNullableBalanceOfPower(source, nodes[0].Values, bop, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is PriceCandlePattern priceCandle)
                latest = FillPriceCandle(source, nodes[0].Values, priceCandle, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is RangeAccelerationBands acceleration)
                latest = FillBoundedWindow<AccelerationKernel>(source, nodes[0].Values, acceleration, acceleration.Period, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is PriceRelativeStrength relative && PriceRelativeWindow(relative) > 0)
                latest = FillPriceRelative(source, nodes[0].Values, relative, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is ClassicMovingAverage or ClassicPriceOscillator)
                latest = FillClassicAverage(source, nodes[0].Values, nodes[0].Indicator, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is WindowAverageEnvelope envelope && AverageEnvelopeWindow(envelope) > 0)
                latest = FillAverageEnvelope(source, nodes[0].Values, envelope, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is SeededOnBalanceVolume obv)
                latest = FillOnBalanceVolume(source, nodes[0].Values, obv, cancellation, owned, out obvFinite);
            else if (nodes.Count == 1 && nodes[0].Indicator is VolumeWeightedPrice weightedPrice)
                latest = FillVolumeWeightedPrice(source, nodes[0].Values, weightedPrice, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is WindowRegressionStatistics regressionStatistics)
                latest = FillRegressionStatistics(source, nodes[0].Values, regressionStatistics, cancellation, owned);
            else if (indicators.Count == 1 && nodes[0].Indicator is RegressionWithFinalLine historicalRegression)
                latest = FillRegressionStatistics(source, nodes[0].Values, historicalRegression, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is SeededAverageTrueRange or NormalizedSeededAverageTrueRange or AverageTrueRangeWithDetails)
                latest = FillSeededAtr(source, nodes[0].Values, nodes[0].Indicator, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].Indicator is AccumulationDistributionWithAverage adl)
                latest = FillAccumulationDistribution(source, nodes[0].Values, adl, cancellation, owned);
            else if (nodes.Count == 1 && IsBoundedWindowIndicator(nodes[0].Indicator))
                latest = FillBoundedWindow(source, nodes[0].Values, nodes[0].Indicator, cancellation, owned);
            else if (boundedBatch)
                latest = FillBoundedBatch(source, nodes, cancellation, owned);
            else if (nodes.Count == 1 && nodes[0].HasScalarState)
                latest = nodes[0].FillScalar(source, cancellation, owned);
            else
            {
                // Only the row-wise mixed/state route needs this filtered list.
                var active = nodes.Where(n => n.Indicator is not Sma).ToArray();
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var bar = source[i];
                    latest = bar;
                    if (owned is not null) owned[i] = bar;
                    if (!AllFieldsFinite(in bar))
                        IndicatorInputDomain.Finite.Validate(in bar);
                    if (close is not null) { close[i] = bar.Close; summary.Include(bar.Close); }
                    foreach (var node in active) node.Append(in bar, i, cancellation);
                }
            }
            int outputCount = 0;
            foreach (var node in nodes) outputCount += node.Values.Length;
            bool scalarLatest = history is null && source.Length != 0 && nodes.Count == 1 && outputCount == 1;
            var published = scalarLatest ? null : new Dictionary<IIndicatorOutput, double[]>(outputCount);
            foreach (var node in nodes)
            {
                cancellation.ThrowIfCancellationRequested();
                node.Failure?.Throw();
                node.Complete(cancellation);
                // Standalone BOP validates raw ratios before smoothing; its default
                // EMA is an exactly rounded convex mean of finite values. Neither
                // output needs the generic scan (including shared EMA(1) arrays).
                bool finiteByConstruction = boundedBatch || pointwiseBatch || nodes.Count == 1 && (IsPointwise(node.Indicator)
                    || node.Indicator is TrueRange or BalanceOfPower || obvFinite || IsBoundedWindowIndicator(node.Indicator));
                if (node.Indicator is Sma sma)
                    finiteByConstruction = fusedSma ? fusedFinite
                        : ComputeSma(close!, node.Values[0], Math.Max(1, sma.Length), summary, cancellation, singleSma, positiveRange.Certifies(Math.Max(1, sma.Length)));
                for (int slot = 0; slot < node.Values.Length; slot++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // Asin and proven SMA ranges produce finite outputs by construction.
                    // Unproven guarded SMA and other states still validate every output.
                    // Deviation bands can overflow secondary slots while their mean stays finite.
                    // Keep slot-major validation after every input has been checked.
                    // A pivot alias has already been checked at its earlier owner
                    // slot. Rechecking the same array cannot discover a new error.
                    bool distinctColumn = node.Indicator is not RollingPivotLevels pivot
                        || PivotColumnOwner(pivot.Style, slot) == slot;
                    if (distinctColumn && (!finiteByConstruction || node.Indicator is RollingPivotLevels or WindowDeviationBands or ClassicDeviationBands or PriorPriceChannel or WindowPairStatistics or WindowVortex or MoneyFlowWithDetails or MovingAverageDiagnostics or WindowBetaStatistics or AwesomeWithDetails or StochasticSmaKdj or WindowStochasticKdj
                        || (gpu is not null && node.Indicator is WindowMoneyFlowIndex)))
                    {
                        for (int i = 0; i < source.Length; i++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (!double.IsFinite(node.Values[slot][i]))
                                IndicatorOutputPolicy.Validate(node.Indicator, slot, i, node.Values[slot][i]);
                        }
                    }
                    published?.Add(node.Indicator.Outputs[slot], node.Values[slot]);
                }
            }
            int warmup = 0;
            foreach (var node in nodes) warmup = Math.Max(warmup, node.Indicator.WarmupBars);
            if (scalarLatest)
                return new ScalarLatestOnlyIndicatorRun(nodes[0].Indicator.Outputs[0], nodes[0].Values[0], latest, warmup);
            if (history is not null)
            {
                owned!.TransferTo(history);
                return new IndicatorRun(null, published!, history, 0, warmup);
            }
            bool ready = source.Length >= warmup;
            if (source.Length != 0 && ready)
                foreach (var values in published!.Values)
                    if (double.IsNaN(values[source.Length - 1])) { ready = false; break; }
            IBarSnapshot? snapshot = source.Length == 0 ? null
                : new BarSnapshot(latest, source.Length - 1, published!, ready);
            return new LatestOnlyIndicatorRun(published!, source.Length, snapshot);
        }
        finally { foreach (var node in nodes) node.Dispose(); }
    }

    private struct FusedSmaRegion
    {
        internal Bar Last;
        internal bool Invalid, Certified, Proven;
    }

    internal static bool TryExecuteSmaParallel(Bar[] source, double[] output, int period,
        CancellationToken cancellation, out Bar latest, out bool finite, out bool certified, OwnedBarBuffer? owned = null)
        => TryExecuteSmaParallelCore(source, output, period, cancellation, out latest, out finite, out certified, owned, null);

    internal static bool TryExecuteSmaParallelOwned(Bar[] source, double[] output, int period,
        CancellationToken cancellation, out Bar latest, out bool finite, out bool certified, OwnedBarHistory history)
        => TryExecuteSmaParallelCore(source, output, period, cancellation, out latest, out finite, out certified, null, history);

    private static bool TryExecuteSmaParallelCore(Bar[] source, double[] output, int period,
        CancellationToken cancellation, out Bar latest, out bool finite, out bool certified,
        OwnedBarBuffer? owned, OwnedBarHistory? history)
    {
        latest = default;
        finite = certified = false;
        if (period is < 2 or > 4096 || !CanParallelize(source.Length, 8192)) return false;
        int chunks = Math.Min(WorkerCount(owned is null && history is null ? 8 : 4), source.Length / (16 * period));
        if (chunks < 2 || !Monitor.TryEnter(ParallelBarGate)) return false;
        double[]? closes = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            // Only an accepted Full dispatch reserves snapshots. Rejected work
            // keeps the original serial path without a second history allocation.
            if (history is not null) owned = new OwnedBarBuffer(source.Length);
            closes = System.Buffers.ArrayPool<double>.Shared.Rent(source.Length);
            var captured = closes;
            int intervals = (source.Length - period) / period;
            int Begin(int chunk) => chunk == 0 ? 0 : period + (int)((long)intervals * chunk / chunks) * period;
            var regions = new FusedSmaRegion[chunks];
            // Capture each overlap once before workers start, directly into its
            // final owned storage. Defer errors until each earlier body is checked.
            for (int chunk = 1; chunk < chunks; chunk++)
            {
                cancellation.ThrowIfCancellationRequested();
                int start = Begin(chunk) - period;
                var boundary = new FusedSmaRegion();
                for (int i = start; i < start + period; i++)
                {
                    if (cancellation.IsCancellationRequested) cancellation.ThrowIfCancellationRequested();
                    var bar = source[i];
                    captured[i] = bar.Close;
                    if (owned is not null) owned[i] = bar;
                    if (!boundary.Invalid)
                    {
                        boundary.Last = bar;
                        boundary.Invalid = !AllFieldsFinite(in bar);
                    }
                }
                regions[chunk - 1] = boundary;
            }
            AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = Begin(chunk);
                int end = chunk == chunks - 1 ? source.Length : Begin(chunk + 1);
                int bodyEnd = chunk == chunks - 1 ? end : end - period;
                var region = new FusedSmaRegion();
                if (!CaptureSmaRegion(source.AsSpan(start, bodyEnd - start),
                    captured.AsSpan(start, bodyEnd - start), owned, start, ref region, cancellation))
                {
                    regions[chunk] = region;
                    return;
                }
                if (bodyEnd == end) regions[chunk] = region;
                if (regions[chunk].Invalid) return;
                int arithmeticStart = Math.Max(period, start);
                var input = captured.AsSpan(arithmeticStart - period, end - arithmeticStart + period);
                try
                {
                    Core.SmaCpuKernel.Summarize(input, out var grid, out var positive, cancellation);
                    bool gridProof = grid.Certifies(period);
                    if (!gridProof && grid.CanRefine)
                    {
                        var proof = new Core.SmaCpuKernel.Certificate(period);
                        gridProof = true;
                        foreach (double value in input)
                        {
                            if (cancellation.IsCancellationRequested) return;
                            if (!proof.Include(value)) { gridProof = false; break; }
                        }
                    }
                    regions[chunk].Certified = gridProof;
                    regions[chunk].Proven = gridProof || positive.Certifies(period);
                    if (regions[chunk].Proven)
                        Core.SmaCpuKernel.ProcessRebasedRegion(input,
                            output.AsSpan(arithmeticStart, end - arithmeticStart), period, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // Quiesce the pool before reporting cancellation on the caller.
                }
            });
            cancellation.ThrowIfCancellationRequested();
            finite = certified = true;
            foreach (var region in regions)
            {
                if (region.Invalid) IndicatorInputDomain.Finite.Validate(in region.Last);
                finite &= region.Proven;
                certified &= region.Certified;
            }
            latest = regions[chunks - 1].Last;
            if (finite)
            {
                output.AsSpan(0, period - 1).Clear();
                double sum = 0;
                for (int i = 0; i < period; i++) sum += captured[i];
                output[period - 1] = sum / period;
            }
            else
            {
                // A rejected region may depend on earlier guarded state. Replay
                // the entire owned close column, not just that region.
                var reader = new Core.SmaCpuKernel.DoubleReader();
                var consumer = new Core.SmaCpuKernel.Identity();
                Core.SmaCpuKernel.ProcessGuarded<double, Core.SmaCpuKernel.DoubleReader, Core.SmaCpuKernel.Identity>(
                    captured.AsSpan(0, source.Length), output, period, ref reader, ref consumer, cancellation);
            }
            cancellation.ThrowIfCancellationRequested();
            if (history is not null) owned!.TransferTo(history);
            return true;
        }
        finally
        {
            if (closes is not null) System.Buffers.ArrayPool<double>.Shared.Return(closes);
            Monitor.Exit(ParallelBarGate);
        }
    }

    private static bool CaptureSmaRegion(ReadOnlySpan<Bar> source, Span<double> closes, OwnedBarBuffer? history, int offset,
        ref FusedSmaRegion region, CancellationToken cancellation)
    {
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            if (cancellation.IsCancellationRequested) return false;
            latest = source[i];
            if (!AllFieldsFinite(in latest))
            {
                region.Last = latest;
                region.Invalid = true;
                return false;
            }
            closes[i] = latest.Close;
            if (history is not null) history[offset + i] = latest;
        }
        region.Last = latest;
        return true;
    }

    internal static Bar FillSmaColumn(Bar[] source, double[] close, int period, CancellationToken cancellation,
        out Core.SmaCpuKernel.GridSummary grid, out Core.SmaCpuKernel.PositiveRangeSummary positive, OwnedBarBuffer? owned = null)
    {
        if (CanParallelize(source.Length) && Monitor.TryEnter(ParallelBarGate))
        {
            try { return FillSmaParallel(source, close, period, cancellation, out grid, out positive, owned); }
            finally { Monitor.Exit(ParallelBarGate); }
        }
        var latest = FillSma(source, close, cancellation, owned);
        grid = new Core.SmaCpuKernel.GridSummary();
        positive = new Core.SmaCpuKernel.PositiveRangeSummary();
        if (period > 1 && period <= close.Length)
            Core.SmaCpuKernel.Summarize(close, out grid, out positive, cancellation);
        return latest;
    }

    private static Bar FillSma(Bar[] source, double[] close, CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            // Use the same owned local for validation and the final snapshot. A
            // separate bar local forces a second 48-byte copy in the Tier1 loop.
            latest = source[i];
            if (!AllFieldsFinite(in latest))
                IndicatorInputDomain.Finite.Validate(in latest);
            close[i] = latest.Close;
            if (owned is not null) owned[i] = latest;
        }
        return latest;
    }

    internal static Bar FillAsin(Bar[] source, double[][] output, CancellationToken cancellation, OwnedBarBuffer? owned = null)
    {
        // The published tensor pool is shared and serializes dispatches. Let one
        // mid-sized build use a bounded fan-out; competing builders continue inline.
        if (CanParallelize(source.Length, AsinParallelMinimum) && Monitor.TryEnter(ParallelBarGate))
        {
            try { return FillAsinParallel(source, output, cancellation, owned); }
            finally { Monitor.Exit(ParallelBarGate); }
        }
        var values = output[0];
        var flags = output[1];
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            latest = source[i];
            if (!AllFieldsFinite(in latest))
                IndicatorInputDomain.Finite.Validate(in latest);
            bool defined = latest.Close is >= -1 and <= 1;
            values[i] = defined ? Math.Asin(latest.Close) : 0;
            flags[i] = defined ? 1 : 0;
            if (owned is not null) owned[i] = latest;
        }
        return latest;
    }

    private static bool CanParallelize(int count, int minimum = 65_536) => count >= minimum && Environment.ProcessorCount > 1
        && AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism > 1;

    private static int WorkerCount(int maximum = 8) => Math.Max(1, Math.Min(maximum, Math.Min(Environment.ProcessorCount,
        AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism)));

    private struct SmaRegion
    {
        internal Bar Last;
        internal bool Invalid;
        internal Core.SmaCpuKernel.GridSummary Grid;
        internal Core.SmaCpuKernel.PositiveRangeSummary Positive;
    }

    private static Bar FillSmaParallel(Bar[] source, double[] close, int period, CancellationToken cancellation,
        out Core.SmaCpuKernel.GridSummary grid, out Core.SmaCpuKernel.PositiveRangeSummary positive, OwnedBarBuffer? owned)
    {
        // Full history also writes the wide bar column; its measured crossover
        // favors four workers, while the close-only path benefits from eight.
        int chunks = WorkerCount(owned is null ? 8 : 4);
        var regions = new SmaRegion[chunks];
        AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = (int)((long)source.Length * chunk / chunks);
            int end = (int)((long)source.Length * (chunk + 1) / chunks);
            var input = source.AsSpan(start, end - start);
            var output = close.AsSpan(start, end - start);
            Bar latest = default;
            for (int i = 0; i < input.Length; i++)
            {
                if (cancellation.IsCancellationRequested) return;
                latest = input[i];
                if (!AllFieldsFinite(in latest))
                {
                    regions[chunk].Invalid = true;
                    regions[chunk].Last = latest;
                    return;
                }
                output[i] = latest.Close;
                if (owned is not null) owned[start + i] = latest;
            }
            regions[chunk].Last = latest;
            try
            {
                if (period > 1 && period <= source.Length)
                    Core.SmaCpuKernel.Summarize(close.AsSpan(start, end - start),
                        out regions[chunk].Grid, out regions[chunk].Positive, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Report cancellation on the caller after every worker quiesces.
            }
        });
        cancellation.ThrowIfCancellationRequested();
        grid = new Core.SmaCpuKernel.GridSummary();
        positive = new Core.SmaCpuKernel.PositiveRangeSummary();
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            if (regions[chunk].Invalid) IndicatorInputDomain.Finite.Validate(in regions[chunk].Last);
            if (period > 1 && period <= source.Length)
            {
                grid.Merge(regions[chunk].Grid);
                positive.Merge(regions[chunk].Positive);
            }
        }
        return regions[chunks - 1].Last;
    }

    private static Bar FillAsinParallel(Bar[] source, double[][] output, CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var values = output[0];
        var flags = output[1];
        int chunks = WorkerCount();
        var lastBars = new Bar[chunks];
        var invalid = new bool[chunks];
        AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = (int)((long)values.Length * chunk / chunks);
            int end = (int)((long)values.Length * (chunk + 1) / chunks);
            var input = source.AsSpan(start, end - start);
            var result = values.AsSpan(start, end - start);
            var presence = flags.AsSpan(start, end - start);
            Bar latest = default;
            for (int i = 0; i < input.Length; i++)
            {
                // The pool can wrap worker exceptions under its ThreadPool mode.
                // Quiesce all workers, then throw cancellation on the caller.
                if (cancellation.IsCancellationRequested) return;
                latest = input[i];
                if (!AllFieldsFinite(in latest))
                {
                    invalid[chunk] = true;
                    break;
                }
                double value = latest.Close;
                bool defined = value is >= -1 and <= 1;
                result[i] = defined ? Math.Asin(value) : 0;
                presence[i] = defined ? 1 : 0;
                if (owned is not null) owned[start + i] = latest;
            }
            lastBars[chunk] = latest;
        });
        cancellation.ThrowIfCancellationRequested();
        // Each region saves its first invalid owned bar. Visiting regions in
        // source order preserves the serial validator's first exception, without
        // a second input traversal or any reread of mutable caller storage.
        for (int chunk = 0; chunk < chunks; chunk++)
            if (invalid[chunk]) IndicatorInputDomain.Finite.Validate(in lastBars[chunk]);
        return lastBars[chunks - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AllFieldsFinite(in Bar bar)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var prices = Vector256.Create(bar.Open, bar.High, bar.Low, bar.Close).AsInt64();
            var exponentMask = Vector256.Create(0x7ff0000000000000L);
            return !Vector256.EqualsAny(prices & exponentMask, exponentMask) && double.IsFinite(bar.Volume);
        }
        return double.IsFinite(bar.Open) && double.IsFinite(bar.High) && double.IsFinite(bar.Low)
            && double.IsFinite(bar.Close) && double.IsFinite(bar.Volume);
    }

    internal static bool ComputeSma(double[] close, double[] output, int period,
        Core.SmaCpuKernel.GridSummary summary, CancellationToken cancellation, bool inPlace, bool boundedPositive,
        int maxWorkers = 8)
    {
        bool certified = CertifiesSma(close, period, summary, cancellation);
        if (inPlace)
        {
            int participants = WorkerCount(maxWorkers);
            // Reduce fan-out for long periods instead of losing an existing
            // parallel route when the normal worker cap increases.
            if (period is >= 2 and <= 4096)
                participants = Math.Min(participants, Math.Max(1, output.Length / (16 * period)));
            // Bound edge storage to at most one sixteenth of the output.
            if ((boundedPositive || certified) && period is >= 2 and <= 4096 && CanParallelize(output.Length)
                && participants > 1
                && Monitor.TryEnter(ParallelBarGate))
            {
                try { Core.SmaCpuKernel.ProcessRebasedParallel(output, period, participants, cancellation); }
                finally { Monitor.Exit(ParallelBarGate); }
                return true;
            }
            var identity = new Core.SmaCpuKernel.MeanIdentity();
            if (certified && Core.SmaCpuKernel.TryProcessPrefixInPlace(output, period, summary, ref identity, cancellation))
                return true;
            Core.SmaCpuKernel.ProcessInPlace(output, period, certified, cancellation, boundedPositive);
            return certified || boundedPositive;
        }
        var reader = new Core.SmaCpuKernel.DoubleReader();
        var consumer = new Core.SmaCpuKernel.Identity();
        if (certified) Core.SmaCpuKernel.ProcessCertified<double, Core.SmaCpuKernel.DoubleReader, Core.SmaCpuKernel.Identity>(
            close, output, period, ref reader, ref consumer, cancellation);
        else Core.SmaCpuKernel.ProcessGuarded<double, Core.SmaCpuKernel.DoubleReader, Core.SmaCpuKernel.Identity>(
            close, output, period, ref reader, ref consumer, cancellation);
        return certified;
    }

    internal static bool CertifiesSma(double[] close, int period,
        Core.SmaCpuKernel.GridSummary summary, CancellationToken cancellation)
    {
        var proof = new Core.SmaCpuKernel.Certificate(period);
        bool certified = period == 1 || period > close.Length || summary.Certifies(period);
        if (!certified && summary.CanRefine)
        {
            certified = true;
            foreach (var value in close)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!proof.Include(value)) { certified = false; break; }
            }
        }
        return certified;
    }

    private sealed class Node : IDisposable
    {
        internal IIndicator Indicator { get; }
        internal double[][] Values { get; }
        internal ExceptionDispatchInfo? Failure { get; private set; }
        private readonly object? _state;
        private readonly double[] _scratch;
        internal bool HasScalarState => _state is IIndicatorState;
        internal Node(IIndicator indicator, int count, bool singleRoot, bool boundedBatch, bool pointwiseBatch, bool volumeFlowCustomRange)
        {
            Indicator = indicator;
            bool pointwise = (singleRoot || pointwiseBatch) && IsPointwise(indicator);
            bool inactiveObvAverage = indicator is SeededOnBalanceVolume obv
                && (obv.AveragePeriod is null || obv.AveragePeriod >= count);
            bool smoothed = indicator is SmoothedAccumulationOscillator;
            bool smoothedDetails = indicator is SmoothedAccumulationOscillator { IncludeDetails: true };
            bool macd = indicator is ClassicMacd or AlignedEmaMacd or EmaDifferenceSignal;
            var pivot = indicator as RollingPivotLevels;
            Values = new double[indicator.Outputs.Count][];
            for (int slot = 0; slot < Values.Length; slot++)
                Values[slot] = indicator is RegressionWithChannels && slot is 5 or 6
                    || indicator is WindowRegressionStatistics && slot is 7 or 9
                    || indicator is RegressionWithFinalLine && slot is 6 or 7
                    || macd && MacdColumnOwner(indicator, slot) < slot
                    || pivot is not null && PivotColumnOwner(pivot.Style, slot) < slot
                    || indicator is SumSeededDirectionalIndex && slot is 6 or 7
                    || indicator is RateOfChangeWithAverage { AveragePeriod: null } && slot == 4
                    || indicator is SeededTrueStrength { SignalPeriod: <= 1 } && slot == 3
                    || indicator is TripleExponentialRate { SignalPeriod: null } && slot == 5
                    || indicator is WindowChandelierExit { Selection: not ChandelierExitSelection.Short } && slot == 3
                    || indicator is WindowChandelierExit { Selection: ChandelierExitSelection.Short } && slot == 2
                    || indicator is WindowAroon && slot >= 4
                    || indicator is ClassicStochastic or ClassicStochasticRsi && slot == 3
                    || indicator is WindowStochasticKdj && slot == 5
                    || indicator is StandardDeviationWithDetails && slot == 5
                    || indicator is WindowDeviationBands && slot is 7 or 8
                    || indicator is ClassicDeviationBands && slot is 4 or 5
                    || indicator is WindowAtrBands && (slot == 6 || slot == 7 && !((WindowAtrBands)indicator).IncludeWidth)
                    || indicator is PriorPriceChannel && slot is 5 or 6
                    || inactiveObvAverage && slot == 2
                    || smoothed && (slot >= 6 || !smoothedDetails && slot is 2 or 3 or 5)
                    || slot == 1 && (singleRoot && indicator is BalanceOfPower { Length: <= 1 }
                    || pointwise && indicator is not PriceCircularTransform { Operation: PriceCircularOperation.ArcSine })
                    ? Array.Empty<double>()
                    : indicator is Sma || pointwise ? GC.AllocateUninitializedArray<double>(count) : new double[count];
            if (macd)
                for (int slot = 0; slot < Values.Length; slot++)
                {
                    int column = MacdColumnOwner(indicator, slot);
                    if (column < slot) Values[slot] = Values[column];
                }
            // These three presence outputs are identical by contract. Completed
            // series expose read-only spans, so one run-owned column serves all three.
            if (pivot is not null)
                for (int slot = 0; slot < Values.Length; slot++)
                {
                    int column = PivotColumnOwner(pivot.Style, slot);
                    if (column < slot) Values[slot] = Values[column];
                }
            if (indicator is RegressionWithChannels) Values[5] = Values[6] = Values[4];
            if (indicator is WindowRegressionStatistics) Values[7] = Values[9] = Values[6];
            if (indicator is RegressionWithFinalLine) Values[6] = Values[7] = Values[5];
            if (indicator is WindowAroon) Values[4] = Values[5] = Values[3];
            if (indicator is SumSeededDirectionalIndex) Values[6] = Values[7] = Values[5];
            if (indicator is RateOfChangeWithAverage { AveragePeriod: null }) Values[4] = Values[3];
            if (indicator is SeededTrueStrength { SignalPeriod: <= 1 }) Values[3] = Values[1];
            if (indicator is TripleExponentialRate { SignalPeriod: null }) Values[5] = Values[2];
            if (indicator is WindowChandelierExit chandelier)
            {
                // Both selected levels mature together; an unselected level and
                // its presence flag are identically zero, even on overflow.
                if (chandelier.Selection == ChandelierExitSelection.Both) Values[3] = Values[2];
                else if (chandelier.Selection == ChandelierExitSelection.Long) Values[3] = Values[1];
                else Values[2] = Values[0];
            }
            if (indicator is WindowStochasticKdj) Values[5] = Values[4];
            if (indicator is ClassicStochastic or ClassicStochasticRsi) Values[3] = Values[2];
            if (indicator is StandardDeviationWithDetails) Values[5] = Values[4];
            if (indicator is WindowDeviationBands) Values[7] = Values[8] = Values[6];
            if (indicator is ClassicDeviationBands) Values[4] = Values[5] = Values[3];
            if (indicator is PriorPriceChannel) Values[5] = Values[6] = Values[4];
            // Both bands publish together; disabled width/value flags remain zero.
            if (indicator is WindowAtrBands atrBands)
            {
                Values[6] = Values[5];
                if (!atrBands.IncludeWidth) Values[7] = Values[3];
            }
            // Disabled/all-warmup OBV average and presence are both constant zero.
            // Keep both public columns while sharing their run-owned storage.
            if (inactiveObvAverage) Values[2] = Values[1];
            // Chaikin detail presence is identical across all three columns.
            // With details disabled, all six detail value/presence columns are zero.
            if (smoothed)
            {
                if (smoothedDetails) Values[6] = Values[7] = Values[5];
                else Values[2] = Values[3] = Values[5] = Values[6] = Values[7] = Values[1];
            }
            _state = indicator switch
            {
                RegressionWithFinalLine when singleRoot => null,
                RegressionWithFinalLine r => new RegressionWithFinalLine.Writer(r.Period, count),
                RegressionWithChannels r => new RegressionWithChannels.Writer(r.Period, r.Deviations, count),
                Sma => null,
                _ when pointwise => null,
                ScaledTrueRange when singleRoot => null,
                WindowRegressionStatistics when singleRoot => null,
                BalanceOfPower when singleRoot => null,
                ClassicMovingAverage or ClassicPriceOscillator when singleRoot => null,
                ClassicMacd macdOwner when singleRoot && ClassicMacdWindow(macdOwner) > 0 => null,
                ClassicStochastic stochastic when singleRoot && ClassicStochasticWindow(stochastic) > 0 => null,
                RateOfChangeWithAverage roc when singleRoot && RateOfChangeWindow(roc) > 0 => null,
                BalanceOfPowerWithValidity { Period: <= 4096 } when singleRoot => null,
                PriceCandlePattern when singleRoot => null,
                WindowAverageEnvelope envelope when singleRoot && AverageEnvelopeWindow(envelope) > 0 => null,
                SeededOnBalanceVolume or AccumulationDistributionWithAverage or VolumeWeightedPrice when singleRoot => null,
                SeededAverageTrueRange or NormalizedSeededAverageTrueRange or AverageTrueRangeWithDetails when singleRoot => null,
                _ when (singleRoot || boundedBatch) && IsBoundedWindowIndicator(indicator) => null,
                _ when IsGeneratedState(indicator) => CreateGeneratedState(indicator, count, volumeFlowCustomRange),
                NormalizedWindowEntropy { Period: <= 4096 } entropy => new EntropyValueState(entropy.Period, count),
                RangeFractalAverage fractal => new FractalValueState(fractal.Period, count),
                FilteredDeviationAverage deviation => new DeviationAverageValueState(deviation.Period, deviation.ScaleFactor, count),
                QuarticDynamicAverage quartic => new QuarticValueState(quartic.Period, quartic.Factor, quartic.Startup),
                TillsonAverage tillson => new TillsonValueState(tillson.Period, tillson.VolumeFactor, tillson.Seed, tillson.Suppression),
                SeededAdaptiveAverage adaptive => new SeededAdaptiveValueState(adaptive, count),
                VariablePeriodClassicAverage { Method: ClassicAverageMethod.Sma } variable => new VariableSmaState(variable, count),
                DeviationRatioAdaptiveAverage ratio => new DeviationRatioValueState(ratio, count),
                OldestFirstHilbertTrendline => new OldestHilbertValueState(),
                TripleExponentialRate triple => new TripleRateState(triple.Period, triple.Seed, triple.SignalPeriod, triple.Suppression),
                WindowChandelierExit chandelierOwner => new ChandelierState(chandelierOwner, count),
                AlligatorWithDetails alligator => new AlligatorState(alligator, count),
                GatorWithDetails gator => new GatorWithDetails.State(new AlligatorState(gator.Alligator, count)),
                JurikAdaptive jurik => new JurikValueState(jurik, count),
                RollingPivotLevels pivots => new PivotValueState(pivots, count),
                RetrospectiveFractals f => (long)f.LeftSpan + f.RightSpan + 1 > count ? null
                    : IndicatorKernels.Fractal(f.LeftSpan, f.RightSpan, f.UseClose),
                IndicatorBase single => single.CreateState(),
                MultiOutputIndicatorBase multi => multi.CreateState(),
                _ => throw new InvalidOperationException("Unqualified values plan.")
            };
            // Scalar and fused kernels never use a per-node output scratch buffer.
            _scratch = _state is null or IIndicatorState ? Array.Empty<double>()
                : new double[indicator is RegressionWithChannels ? 10 : indicator.Outputs.Count];
        }
        internal void Append(in Bar bar, int index, CancellationToken cancellation)
        {
            if (Failure is not null || Indicator is Sma) return;
            try
            {
                if (_state is RegressionWithFinalLine.Writer regression) regression.Append(in bar, index, Values, _scratch);
                else if (_state is RegressionWithChannels.Writer channels) channels.Append(in bar, index, Values, _scratch, cancellation);
                else if (Indicator is PriceCircularTransform)
                {
                    bool defined = bar.Close is >= -1 and <= 1;
                    Values[0][index] = defined ? Math.Asin(bar.Close) : 0;
                    Values[1][index] = defined ? 1 : 0;
                }
                else if (Indicator is RetrospectiveFractals f)
                {
                    if (_state is not IndicatorKernel kernel) return;
                    kernel.Update(in bar, _scratch);
                    int center = index - f.RightSpan;
                    if (center < 0) return;
                    for (int slot = 0; slot < 2; slot++)
                    {
                        bool present = !double.IsNaN(_scratch[slot]);
                        Values[slot][center] = present ? _scratch[slot] : 0;
                        Values[slot + 2][center] = present ? 1 : 0;
                    }
                }
                else if (_state is IIndicatorState single) Values[0][index] = single.Update(in bar);
                else if (_state is MovingAverageDifference.State difference)
                {
                    difference.Update(in bar, _scratch);
                    Values[0][index] = _scratch[0];
                    Values[1][index] = _scratch[1];
                }
                else if (_state is WindowDirectionalMeasure.State directional)
                {
                    // This state clears both outputs itself, including absence.
                    directional.Update(in bar, _scratch);
                    Values[0][index] = _scratch[0];
                    Values[1][index] = _scratch[1];
                }
                else if (_state is VariableSmaState variable)
                {
                    variable.Update(in bar, _scratch);
                    Values[0][index] = _scratch[0];
                    Values[1][index] = _scratch[1];
                }
                else if (_state is PriceRelativeStrength.State relative)
                {
                    relative.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is RangeAccelerationBands.State acceleration)
                {
                    acceleration.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is PriceCandlePattern.State priceCandle)
                {
                    priceCandle.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is HeikinAshiCandles.State heikinAshi)
                {
                    heikinAshi.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is AlligatorState alligator)
                {
                    alligator.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is GatorWithDetails.State gator)
                {
                    gator.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is BalanceOfPowerWithValidity.State bop)
                {
                    bop.Update(in bar, _scratch);
                    Values[0][index] = _scratch[0]; Values[1][index] = _scratch[1];
                }
                else if (_state is ElderRayWithDetails.State elderRay)
                {
                    elderRay.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is PriceAdjustedAccumulationDistribution.State adjustedAdl)
                {
                    adjustedAdl.Update(in bar, _scratch);
                    Values[0][index] = _scratch[0]; Values[1][index] = _scratch[1];
                }
                else if (_state is ChandelierState chandelier)
                {
                    chandelier.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is SeededAtrTrailingStop.State atrStop)
                {
                    atrStop.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is TripleRateState tripleRate)
                {
                    tripleRate.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is SeededPriceMomentum.State priceMomentum)
                {
                    priceMomentum.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is SeededTrueStrength.State trueStrength)
                {
                    trueStrength.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is RateOfChangeWithAverage.State roc)
                {
                    roc.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is RateOfChangeRmsBands.State rocBands)
                {
                    rocBands.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is VolumeConditionedIndex.State volumeIndex)
                {
                    // The recurrence assigns both columns, including absent values.
                    volumeIndex.Update(in bar, _scratch);
                    Values[0][index] = _scratch[0];
                    Values[1][index] = _scratch[1];
                }
                else if (_state is WilderStrengthOscillator.State wilderStrength)
                {
                    wilderStrength.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is NullableStrengthOscillator.State nullableStrength)
                {
                    nullableStrength.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is SumSeededDirectionalIndex.State sumDirectional)
                {
                    sumDirectional.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is ClassicMacd.State classicMacd)
                {
                    classicMacd.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is AlignedEmaMacd.State alignedMacd)
                {
                    alignedMacd.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is EmaDifferenceSignal.State emaDifference)
                {
                    emaDifference.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is ClassicStochastic.State classicStochastic)
                {
                    classicStochastic.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is ClassicStochasticRsi.State classicRsi)
                {
                    classicRsi.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is NullableStochasticRsi.State nullableRsi)
                {
                    nullableRsi.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is SmoothedStochasticRsi.State smoothedRsi)
                {
                    smoothedRsi.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is WindowStochasticKdj.State kdj)
                {
                    kdj.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is DoubleSmoothedStochasticMomentum.State momentum)
                {
                    momentum.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is WindowAtrBands.State atrBands)
                {
                    atrBands.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is WindowAverageEnvelope.State envelope)
                {
                    envelope.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else if (_state is PivotValueState pivot)
                    pivot.Append(in bar, _scratch, Values, index);
                else if (_state is IMultiOutputState multi)
                {
                    Array.Clear(_scratch);
                    multi.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else throw new InvalidOperationException("Values plan has no state.");
            }
            catch (Exception error)
            {
                // Validate remaining raw bars before surfacing arithmetic failures,
                // matching the finite builder's input-before-output validation order.
                Failure = ExceptionDispatchInfo.Capture(error);
            }
        }
        internal Bar FillScalar(Bar[] source, CancellationToken cancellation, OwnedBarBuffer? owned)
        {
            // Keep the existing Rickshaw batch's concrete Update call available to
            // the JIT; an interface call here loses its hot-loop specialization.
            if (_state is RickshawGridState grid) return FillScalar(source, new GridUpdate(grid), cancellation, owned);
            if (_state is PriceExtremeState extreme) return FillScalar(source, new ExtremeUpdate(extreme), cancellation, owned);
            if (_state is EmaValueState ema) return FillScalar(source, new EmaUpdate(ema), cancellation, owned);
            if (_state is TrueRangeValueState range) return FillScalar(source, new TrueRangeUpdate(range), cancellation, owned);
            if (_state is EntropyValueState entropy) return FillScalar(source, new EntropyUpdate(entropy), cancellation, owned);
            if (_state is FractalValueState fractal) return FillScalar(source, new FractalUpdate(fractal), cancellation, owned);
            if (_state is DeviationAverageValueState deviation) return FillScalar(source, new DeviationAverageUpdate(deviation), cancellation, owned);
            if (_state is FirstValueEma.State firstEma) return FillScalar(source, new FirstEmaUpdate(firstEma), cancellation, owned);
            if (_state is TrendMethodsState methods) return FillScalar(source, new TrendMethodsUpdate(methods), cancellation, owned);
            return FillScalar(source, new StateUpdate((IIndicatorState)_state!), cancellation, owned);
        }
        // Preserve recurrence state while exposing concrete updates to the JIT.
        private readonly struct FirstEmaUpdate(FirstValueEma.State state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct TrendMethodsUpdate(TrendMethodsState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private interface IScalarUpdate { double Update(in Bar bar); }
        private readonly struct GridUpdate(RickshawGridState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct ExtremeUpdate(PriceExtremeState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct EmaUpdate(EmaValueState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct TrueRangeUpdate(TrueRangeValueState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct StateUpdate(IIndicatorState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct EntropyUpdate(EntropyValueState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct FractalUpdate(FractalValueState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct DeviationAverageUpdate(DeviationAverageValueState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private Bar FillScalar<T>(Bar[] source, T state, CancellationToken cancellation, OwnedBarBuffer? owned) where T : struct, IScalarUpdate
        {
            var output = Values[0];
            Bar latest = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var bar = source[i];
                latest = bar;
                if (owned is not null) owned[i] = bar;
                if (!AllFieldsFinite(in bar))
                    IndicatorInputDomain.Finite.Validate(in bar);
                if (Failure is not null) continue;
                try { output[i] = state.Update(in bar); }
                catch (Exception error) { Failure = ExceptionDispatchInfo.Capture(error); }
            }
            return latest;
        }
        internal void Complete(CancellationToken cancellation)
        {
            if (_state is RegressionWithFinalLine.Writer regression) regression.Complete(Values, cancellation);
            if (_state is RegressionWithChannels.Writer channels) channels.Complete();
        }
        public void Dispose() => (_state as IDisposable)?.Dispose();
    }
}
#endif
