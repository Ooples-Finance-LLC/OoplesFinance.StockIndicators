using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Evaluates series in a computation graph with multi-symbol support.
/// </summary>
internal sealed class SeriesEvaluator
{
    private readonly Dictionary<SeriesKey, StockData> _dataByKey;
    private readonly StockData _defaultData;
    private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
    private readonly Dictionary<SeriesHandle, ReadOnlyMemory<double>> _cache;
    private readonly ComputeContext? _computeContext;

    // Cached base input to avoid repeated ToArray() calls
    private ReadOnlyMemory<double>? _cachedDefaultInput;

    // Reusable HashSet for cycle detection (avoid allocation per Evaluate call)
    private readonly HashSet<SeriesHandle> _visitingSet = new();

    // Statistics for fast path usage
    private int _fastPathHits;
    private int _standardPathHits;
    private int _fusedChainHits;

    // How many nodes name each handle as an input, counted once from _nodes. A handle named by exactly one
    // other node has a single consumer, which is what makes its series safe to leave unmaterialised.
    private Dictionary<SeriesHandle, int>? _inDegree;

    // The handles this evaluation was asked for. An intermediate that is itself requested has to be published,
    // so it must be computed as a series of its own however few nodes read it.
    private HashSet<SeriesHandle>? _requested;

    /// <summary>
    /// Creates a new series evaluator with single-symbol data (backwards compatible).
    /// </summary>
    public SeriesEvaluator(StockData data, Dictionary<SeriesHandle, SeriesNode> nodes)
        : this(data, nodes, null)
    {
    }

    /// <summary>
    /// Creates a new series evaluator with single-symbol data and compute context.
    /// </summary>
    public SeriesEvaluator(StockData data, Dictionary<SeriesHandle, SeriesNode> nodes, ComputeContext? computeContext)
    {
        _defaultData = data;
        _dataByKey = new Dictionary<SeriesKey, StockData>();
        _nodes = nodes;
        _cache = new Dictionary<SeriesHandle, ReadOnlyMemory<double>>();
        _computeContext = computeContext;
    }

    /// <summary>
    /// Creates a new series evaluator with multi-symbol data support.
    /// </summary>
    public SeriesEvaluator(Dictionary<SeriesKey, StockData> dataByKey, StockData defaultData, Dictionary<SeriesHandle, SeriesNode> nodes)
        : this(dataByKey, defaultData, nodes, null)
    {
    }

    /// <summary>
    /// Creates a new series evaluator with multi-symbol data and compute context.
    /// </summary>
    public SeriesEvaluator(Dictionary<SeriesKey, StockData> dataByKey, StockData defaultData, Dictionary<SeriesHandle, SeriesNode> nodes, ComputeContext? computeContext)
    {
        _dataByKey = dataByKey;
        _defaultData = defaultData;
        _nodes = nodes;
        _cache = new Dictionary<SeriesHandle, ReadOnlyMemory<double>>();
        _computeContext = computeContext;
    }

    /// <summary>
    /// Gets the number of fast path hits (zero-allocation compute).
    /// </summary>
    public int FastPathHits => _fastPathHits;

    /// <summary>
    /// Gets the number of standard path computations.
    /// </summary>
    public int StandardPathHits => _standardPathHits;

    /// <summary>
    /// Gets the number of chains computed in a single pass instead of through an intermediate series.
    /// </summary>
    /// <remarks>
    /// Fusion is an optimisation, so it changes no value and no test can see it in the results - which is
    /// exactly how a test that never fuses would pass. This counter is the signal that the chain really was
    /// fused, and the fusion tests assert on it for that reason. See issue #107.
    /// </remarks>
    public int FusedChainHits => _fusedChainHits;

    public Dictionary<SeriesHandle, ReadOnlyMemory<double>> Evaluate(IReadOnlyCollection<SeriesHandle> handles)
    {
        _requested = new HashSet<SeriesHandle>(handles);
        var result = new Dictionary<SeriesHandle, ReadOnlyMemory<double>>();
        foreach (var handle in handles)
        {
            _visitingSet.Clear();
            result[handle] = Resolve(handle);
        }

        return result;
    }

    public ReadOnlyMemory<double> Evaluate(SeriesHandle handle)
    {
        _requested = new HashSet<SeriesHandle> { handle };
        _visitingSet.Clear();
        return Resolve(handle);
    }

    private ReadOnlyMemory<double> Resolve(SeriesHandle handle)
    {
        if (_cache.TryGetValue(handle, out var cached))
        {
            return cached;
        }

        if (!_visitingSet.Add(handle))
        {
            throw new InvalidOperationException("Cycle detected in series graph.");
        }

        if (!_nodes.TryGetValue(handle, out var node))
        {
            throw new InvalidOperationException("Unknown series handle.");
        }

        ReadOnlyMemory<double> resolved;
        switch (node.Kind)
        {
            case SeriesNodeKind.Base:
                resolved = GetBaseInput(node.SeriesKey);
                break;
            case SeriesNodeKind.Indicator:
                resolved = ResolveIndicator(node);
                break;
            case SeriesNodeKind.Formula:
                resolved = ResolveFormula(node);
                break;
            case SeriesNodeKind.MultiStockIndicator:
                resolved = ResolveMultiStockIndicator(node);
                break;
            default:
                throw new InvalidOperationException("Unknown series node kind.");
        }

        _visitingSet.Remove(handle);
        _cache[handle] = resolved;
        return resolved;
    }

    private ReadOnlyMemory<double> ResolveIndicator(SeriesNode node)
    {
        if (!node.Input.HasValue || node.Spec == null)
        {
            throw new InvalidOperationException("Indicator node missing input or spec.");
        }

        var baseData = GetBaseData(node.SeriesKey);

        // Fast path: if input is the base series (close prices), use original data directly
        // This avoids cloning StockData and copying arrays
        if (IsBaseSeriesInput(node.Input.Value, node.SeriesKey))
        {
            // Try zero-allocation fast path if compute context available
            if (_computeContext is not null)
            {
                var fastResult = IndicatorCompute.TryComputeFast(baseData, node.Spec, _computeContext);
                if (fastResult.HasValue)
                {
                    _fastPathHits++;

                    // Deliberately not disposed, and deliberately not copied. The rented array becomes the
                    // cached series: copying it out cost a full pass and another array the size of the whole
                    // history - 80,024 bytes at 10,000 bars, a third of everything a run allocated. The
                    // ComputeContext still holds it and returns it to the pool when the runtime that owns the
                    // context is disposed, which is exactly when the series stops being readable anyway.
                    return fastResult.Value.Memory;
                }
            }

            // V2 path: use StatefulIndicator for batch computation
            _standardPathHits++;
            return ComputeWithV2(baseData, node.Spec);
        }

        // Chained indicator path. When nothing else needs the series between the links, the chain runs as one
        // pass over the bars and the intermediate array is never built.
        _standardPathHits++;
        if (TryBuildFusedChain(node, out var chain))
        {
            _fusedChainHits++;
            return BatchCompute.ComputeAllChained(baseData, chain);
        }

        var input = Resolve(node.Input.Value);
        return ComputeWithV2CustomInput(baseData, input.Span, node.Spec);
    }

    /// <summary>
    /// The states of a chain that can run in one pass, or nothing when this chain has to be materialised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Walks back from <paramref name="node"/> through its inputs to the bars, and refuses as soon as anything
    /// would make the intermediate series observable. An intermediate survives only when it is read by exactly
    /// one node, is not itself requested, is not already computed, and publishes its own value rather than a
    /// named output - the last because <see cref="BatchCompute.ComputeAllChained"/> carries each state's value
    /// forward, which is what the unfused chained path carries too, while a named output is answered by
    /// <c>ComputeWithV2</c> instead.
    /// </para>
    /// <para>
    /// The head carries two conditions of its own, because the head is the only link whose implementation
    /// fusion changes: unfused it can be served a fast arm, fused it is always its streaming state. It must
    /// read the same series both ways, and it must be a spec those two engines agree on exactly - see
    /// <see cref="FusableChainHeads"/>.
    /// </para>
    /// </remarks>
    private bool TryBuildFusedChain(SeriesNode node, out List<IStreamingIndicatorState> chain)
    {
        chain = new List<IStreamingIndicatorState>();

        // Nothing is asked here about which series the head will read. ComputeAllChained feeds it the same
        // expression a fast arm reads, so the two agree by construction; a check instead would have to hold
        // against a caller mutating the list InputValues returns, which passes through no setter at all.
        if (!TryWalkToBars(node, out var specs) || specs.Count < 2)
        {
            // One indicator reading the bars is not a chain; it has no intermediate to save.
            return false;
        }

        specs.Reverse();
        return TryCreateStates(specs, chain);
    }

    /// <summary>
    /// The specs from <paramref name="node"/> back to the bars, nearest first, or nothing when one of the
    /// series between them has to exist.
    /// </summary>
    private bool TryWalkToBars(SeriesNode node, out List<IndicatorSpec> specs)
    {
        specs = new List<IndicatorSpec>();
        var walked = new HashSet<SeriesHandle>();
        var current = node;
        while (true)
        {
            if (current.Spec is null || !current.Input.HasValue || !current.SeriesKey.Equals(node.SeriesKey))
            {
                return false;
            }

            specs.Add(current.Spec);
            var inputHandle = current.Input.Value;
            if (IsBaseSeriesInput(inputHandle, node.SeriesKey))
            {
                return true;
            }

            if (!CanFoldAway(inputHandle, walked)
                || !_nodes.TryGetValue(inputHandle, out var upstream)
                || upstream.Kind != SeriesNodeKind.Indicator)
            {
                return false;
            }

            current = upstream;
        }
    }

    /// <summary>Whether the series at this handle can be left unbuilt.</summary>
    private bool CanFoldAway(SeriesHandle handle, HashSet<SeriesHandle> walked)
    {
        // A handle seen twice on one walk is a cycle. Resolve catches those with _visitingSet, and this path
        // does not go through Resolve, so it catches its own.
        if (!walked.Add(handle))
        {
            return false;
        }

        if (_cache.ContainsKey(handle) || _requested is null || _requested.Contains(handle))
        {
            return false;
        }

        return InDegree().TryGetValue(handle, out var consumers) && consumers == 1;
    }

    /// <summary>The states for a chain given head first, or nothing when one of its links cannot be fused.</summary>
    private static bool TryCreateStates(List<IndicatorSpec> specs, List<IStreamingIndicatorState> chain)
    {
        if (!FusableChainHeads.Types.Contains(specs[0].Options.GetType()))
        {
            return false;
        }

        for (var i = 0; i < specs.Count; i++)
        {
            // Every link but the last is carried forward by value, so one addressed by a named output would be
            // a different series from the one the unfused path resolves.
            if (i < specs.Count - 1 && specs[i].OutputKey is not null)
            {
                return false;
            }

            try
            {
                chain.Add(StatefulIndicatorFactory.Create(specs[i]));
            }
            catch (NotSupportedException)
            {
                // No streaming state for this spec, so there is no chain to run.
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// How many nodes name each handle as an input, computed once for this evaluator.
    /// </summary>
    /// <remarks>
    /// Counted over every node rather than the reachable ones, which can only overstate a handle's consumers
    /// and so can only refuse a fusion that would have been safe.
    /// </remarks>
    private Dictionary<SeriesHandle, int> InDegree()
    {
        if (_inDegree is not null)
        {
            return _inDegree;
        }

        var counts = new Dictionary<SeriesHandle, int>();
        foreach (var node in _nodes.Values)
        {
            CountInput(counts, node.Input);
            CountInput(counts, node.Left);
            CountInput(counts, node.Right);
        }

        _inDegree = counts;
        return counts;
    }

    private static void CountInput(Dictionary<SeriesHandle, int> counts, SeriesHandle? handle)
    {
        if (handle is { } value)
        {
            counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
        }
    }

    /// <summary>
    /// Computes an indicator using V2-native StatefulIndicators.
    /// </summary>
    private static double[] ComputeWithV2(StockData data, IndicatorSpec spec)
    {
        var state = StatefulIndicatorFactory.Create(spec);

        // Ask for the slot the caller actually requested. Without this every output of a multi-output
        // indicator returned the primary series - three identical bands from one Alligator.
        return BatchCompute.ComputeAll(data, state, ResolveOutputKey(spec));
    }

    /// <summary>
    /// The output key this spec is asking for, or null when it wants the primary value.
    /// </summary>
    private static string? ResolveOutputKey(IndicatorSpec spec)
    {
        // The key the caller named, or null for the indicator's own series. There is nothing to resolve any
        // more: a spec either names a published output or it wants the single series, and the indicator's own
        // published outputs are the authority on which keys exist. The six-slot map that used to sit here
        // could not name every output and answered some of them wrongly - UpperBand returning a lower
        // channel - which is what issue #219 removed.
        return spec.OutputKey;
    }

    /// <summary>
    /// Computes an indicator with custom input values using V2-native StatefulIndicators.
    /// </summary>
    private static double[] ComputeWithV2CustomInput(StockData data, ReadOnlySpan<double> customInput, IndicatorSpec spec)
    {
        var state = StatefulIndicatorFactory.Create(spec);
        return BatchCompute.ComputeAllWithCustomInput(data, customInput, state);
    }

    /// <summary>
    /// Checks if the input handle points to the base price series for the given series key.
    /// </summary>
    private bool IsBaseSeriesInput(SeriesHandle inputHandle, SeriesKey seriesKey)
    {
        if (!_nodes.TryGetValue(inputHandle, out var inputNode))
        {
            return false;
        }

        // Input is a base series node with matching series key
        return inputNode.Kind == SeriesNodeKind.Base && inputNode.SeriesKey.Equals(seriesKey);
    }

    private double[] ResolveFormula(SeriesNode node)
    {
        if (!node.Left.HasValue || !node.Right.HasValue || node.Formula == null)
        {
            throw new InvalidOperationException("Formula node missing operands or function.");
        }

        var left = Resolve(node.Left.Value).Span;
        var right = Resolve(node.Right.Value).Span;
        var count = Math.Max(left.Length, right.Length);
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            var l = i < left.Length ? left[i] : double.NaN;
            var r = i < right.Length ? right[i] : double.NaN;
            values[i] = node.Formula(l, r);
        }

        return values;
    }

    /// <summary>
    /// Resolves a multi-stock indicator node (compares stock vs market/benchmark).
    /// </summary>
    private double[] ResolveMultiStockIndicator(SeriesNode node)
    {
        if (!node.Left.HasValue || !node.Right.HasValue || node.Spec == null)
        {
            throw new InvalidOperationException("Multi-stock indicator node missing stock input, market input, or spec.");
        }

        var stockData = GetBaseData(node.SeriesKey);
        var marketPrices = Resolve(node.Right.Value);

        // Create market StockData from the resolved market prices
        // For multi-stock indicators, we need to create a StockData with the market prices as close prices
        var marketData = CreateMarketDataFromPrices(stockData, marketPrices.Span);

        // Apply the multi-stock indicator using V2-native implementation
        _standardPathHits++;
        return ApplyMultiStockIndicatorV2(stockData, marketData, node.Spec);
    }

    /// <summary>
    /// Creates a StockData object from market prices, using the stock data structure as a template.
    /// </summary>
    private static StockData CreateMarketDataFromPrices(StockData templateData, ReadOnlySpan<double> marketPrices)
    {
        // Create ticker data with the market prices as close prices
        var tickerList = new List<TickerData>();
        var count = Math.Min(templateData.ClosePrices.Count, marketPrices.Length);

        for (var i = 0; i < count; i++)
        {
            var date = i < templateData.Dates.Count ? templateData.Dates[i] : DateTime.MinValue.AddDays(i);
            var price = marketPrices[i];
            tickerList.Add(new TickerData
            {
                Date = date,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = 0
            });
        }

        return new StockData(tickerList);
    }

    /// <summary>
    /// Applies a multi-stock indicator using V2-native implementations.
    /// </summary>
    private static double[] ApplyMultiStockIndicatorV2(StockData stockData, StockData marketData, IndicatorSpec spec)
    {
        if (spec.Options is not MultiStockIndicatorOptions options)
        {
            throw new InvalidOperationException($"Multi-stock indicator '{spec.Name}' requires MultiStockIndicatorOptions.");
        }

        // Create series keys for primary and market data (use Streaming.SeriesKey, not Builder.SeriesKey)
        var primaryKey = new Streaming.SeriesKey("PRIMARY", BarTimeframe.Days(1));
        var marketKey = new Streaming.SeriesKey("MARKET", BarTimeframe.Days(1));

        var state = CreateMultiSeriesIndicatorState(spec.Name, options, primaryKey, marketKey);
        return BatchCompute.ComputeAllMultiSeries(stockData, marketData, state, primaryKey, marketKey);
    }

    /// <summary>
    /// Creates a V2-native multi-series indicator state from spec.
    /// </summary>
    private static IMultiSeriesIndicatorState CreateMultiSeriesIndicatorState(
        IndicatorName name,
        MultiStockIndicatorOptions options,
        Streaming.SeriesKey primaryKey,
        Streaming.SeriesKey marketKey)
    {
        return name switch
        {
            IndicatorName.RSMKIndicator => new RSMKIndicatorState(
                primaryKey, marketKey, options.MaType, options.Length1, options.Length2),
            IndicatorName.ComparePriceMomentumOscillator => new ComparePriceMomentumOscillatorState(
                primaryKey, marketKey, options.Length1, options.Length2),
            IndicatorName.KaufmanStressIndicator => new KaufmanStressIndicatorState(
                primaryKey, marketKey, options.Length1),
            IndicatorName.RelativeNormalizedVolatility => new RelativeNormalizedVolatilityState(
                primaryKey, marketKey, options.MaType, options.Length1),
            IndicatorName.RelativeStrength3DIndicator => new RelativeStrength3DIndicatorState(
                primaryKey, marketKey, options.MaType, options.Length1, options.Length2, options.Length3, options.Length4, options.Length5),
            IndicatorName.SectorRotationModel => new SectorRotationModelState(
                primaryKey, marketKey, options.MaType, options.Length1, options.Length2),
            _ => throw new NotSupportedException($"Multi-stock indicator '{name}' is not supported in V2.")
        };
    }

    /// <summary>
    /// Gets the base data for a series key, supporting multi-symbol scenarios.
    /// </summary>
    private StockData GetBaseData(SeriesKey seriesKey)
    {
        if (_dataByKey.TryGetValue(seriesKey, out var data))
        {
            return data;
        }

        return _defaultData;
    }

    /// <summary>
    /// Gets the base input values for a series key.
    /// </summary>
    private ReadOnlyMemory<double> GetBaseInput(SeriesKey seriesKey)
    {
        var data = GetBaseData(seriesKey);

        // Fast path: cache default data input to avoid repeated copies
        if (ReferenceEquals(data, _defaultData))
        {
            if (_cachedDefaultInput.HasValue)
            {
                return _cachedDefaultInput.Value;
            }

            // InputMemory rather than InputValues.ToArray(): the latter builds the column as a list and then
            // copies it out, two arrays the size of the history for a series the caller already holds.
            _cachedDefaultInput = data.CustomValuesList.Count > 0
                ? data.CustomValuesList.ToArray()
                : data.InputMemory;
            return _cachedDefaultInput.Value;
        }

        return data.CustomValuesList.Count > 0 ? data.CustomValuesList.ToArray() : data.InputMemory;
    }

    /// <summary>
    /// Extracts output values from v1 StockData result.
    /// Used temporarily for multi-stock indicators until Phase 2 migration.
    /// </summary>
    private static double[] ExtractOutput(StockData result, IndicatorSpec spec)
    {
        // A named key is answered out of what the indicator actually published, and judged against the same
        // thing. The six-slot map cannot serve as the authority here: it holds at most six keys per
        // indicator, so every key this feature exists to reach - CamarillaPivotPoints has eleven - would be
        // rejected as unpublished. See issue #201.
        if (spec.OutputKey is not null)
        {
            if (result.OutputValues is not null && result.OutputValues.TryGetValue(spec.OutputKey, out var named))
            {
                return named.ToArray();
            }

            var published = result.OutputValues is null || result.OutputValues.Count == 0
                ? "none"
                : string.Join(", ", result.OutputValues.Keys);

            throw new CalculationException(
                $"{spec.Name} does not publish an output named '{spec.OutputKey}'. Available outputs: {published}.");
        }

        // A spec that names no key wants the indicator's own series, which is where a single-output
        // indicator publishes it.
        return result.CustomValuesList.ToArray();
    }
}
