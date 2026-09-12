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
    private readonly Dictionary<SeriesHandle, double[]> _cache;
    private readonly ComputeContext? _computeContext;

    // Cached base input to avoid repeated ToArray() calls
    private double[]? _cachedDefaultInput;

    // Reusable HashSet for cycle detection (avoid allocation per Evaluate call)
    private readonly HashSet<SeriesHandle> _visitingSet = new();

    // Statistics for fast path usage
    private int _fastPathHits;
    private int _standardPathHits;

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
        _cache = new Dictionary<SeriesHandle, double[]>();
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
        _cache = new Dictionary<SeriesHandle, double[]>();
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

    public Dictionary<SeriesHandle, double[]> Evaluate(IReadOnlyCollection<SeriesHandle> handles)
    {
        var result = new Dictionary<SeriesHandle, double[]>();
        foreach (var handle in handles)
        {
            _visitingSet.Clear();
            result[handle] = Resolve(handle);
        }

        return result;
    }

    public double[] Evaluate(SeriesHandle handle)
    {
        _visitingSet.Clear();
        return Resolve(handle);
    }

    private double[] Resolve(SeriesHandle handle)
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

        double[] resolved;
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

    private double[] ResolveIndicator(SeriesNode node)
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
                    // Fast path returns ComputeBuffer, need to copy to double[] for cache
                    // This is still more efficient because we avoid intermediate allocations
                    using var buffer = fastResult.Value;
                    return buffer.ToArray();
                }
            }

            // V2 path: use StatefulIndicator for batch computation
            _standardPathHits++;
            return ComputeWithV2(baseData, node.Spec);
        }

        // Chained indicator path: use custom input values with V2 computation
        _standardPathHits++;
        var input = Resolve(node.Input.Value);
        return ComputeWithV2CustomInput(baseData, input, node.Spec);
    }

    /// <summary>
    /// Computes an indicator using V2-native StatefulIndicators.
    /// </summary>
    private static double[] ComputeWithV2(StockData data, IndicatorSpec spec)
    {
        var state = StatefulIndicatorFactory.Create(spec);
        return BatchCompute.ComputeAll(data, state);
    }

    /// <summary>
    /// Computes an indicator with custom input values using V2-native StatefulIndicators.
    /// </summary>
    private static double[] ComputeWithV2CustomInput(StockData data, double[] customInput, IndicatorSpec spec)
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

        var left = Resolve(node.Left.Value);
        var right = Resolve(node.Right.Value);
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
        var marketData = CreateMarketDataFromPrices(stockData, marketPrices);

        // Apply the multi-stock indicator using V2-native implementation
        _standardPathHits++;
        return ApplyMultiStockIndicatorV2(stockData, marketData, node.Spec);
    }

    /// <summary>
    /// Creates a StockData object from market prices, using the stock data structure as a template.
    /// </summary>
    private static StockData CreateMarketDataFromPrices(StockData templateData, double[] marketPrices)
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
    private double[] GetBaseInput(SeriesKey seriesKey)
    {
        var data = GetBaseData(seriesKey);

        // Fast path: cache default data input to avoid repeated ToArray() calls
        if (ReferenceEquals(data, _defaultData))
        {
            if (_cachedDefaultInput is not null)
            {
                return _cachedDefaultInput;
            }

            var defaultInput = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
            _cachedDefaultInput = defaultInput.ToArray();
            return _cachedDefaultInput;
        }

        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        return input.ToArray();
    }

    /// <summary>
    /// Extracts output values from v1 StockData result.
    /// Used temporarily for multi-stock indicators until Phase 2 migration.
    /// </summary>
    private static double[] ExtractOutput(StockData result, IndicatorSpec spec)
    {
        var key = IndicatorOutputRegistry.GetOutputKey(spec.Name, spec.Output);
        if (key is not null && result.ChainedOutputs.TryGetValue(key, out var list))
        {
            return list.ToArray();
        }

        return result.ChainedValues.ToArray();
    }
}

/// <summary>
/// Extensible registry for indicator output key mappings.
/// </summary>
public static class IndicatorOutputRegistry
{
    private static readonly Dictionary<(IndicatorName, IndicatorOutput), string> OutputKeyMap = new()
    {
        // MACD outputs
        { (IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Signal), "Signal" },
        { (IndicatorName.MovingAverageConvergenceDivergence, IndicatorOutput.Histogram), "Histogram" },

        // Bollinger Bands outputs
        { (IndicatorName.BollingerBands, IndicatorOutput.UpperBand), "UpperBand" },
        { (IndicatorName.BollingerBands, IndicatorOutput.MiddleBand), "MiddleBand" },
        { (IndicatorName.BollingerBands, IndicatorOutput.LowerBand), "LowerBand" },

        // Stochastic outputs (K/D lines): the batch indicator publishes %D as FastD.
        { (IndicatorName.StochasticOscillator, IndicatorOutput.Signal), "FastD" },

        // Chandelier Exit outputs
        { (IndicatorName.ChandelierExit, IndicatorOutput.UpperBand), "ExitLong" },
        { (IndicatorName.ChandelierExit, IndicatorOutput.LowerBand), "ExitShort" },

        // ADX outputs (DI+, DI-, ADX)
        { (IndicatorName.AverageDirectionalIndex, IndicatorOutput.Signal), "Adx" },

        // Aroon outputs
        { (IndicatorName.AroonOscillator, IndicatorOutput.UpperBand), "AroonUp" },
        { (IndicatorName.AroonOscillator, IndicatorOutput.LowerBand), "AroonDown" },

        // CCI outputs
        { (IndicatorName.CommodityChannelIndex, IndicatorOutput.Primary), "Cci" },

        // Williams %R outputs
        { (IndicatorName.WilliamsR, IndicatorOutput.Primary), "WilliamsR" },

        // Donchian Channels outputs
        { (IndicatorName.DonchianChannels, IndicatorOutput.UpperBand), "UpperChannel" },
        { (IndicatorName.DonchianChannels, IndicatorOutput.MiddleBand), "MiddleChannel" },
        { (IndicatorName.DonchianChannels, IndicatorOutput.LowerBand), "LowerChannel" },

        // Keltner Channels outputs
        { (IndicatorName.KeltnerChannels, IndicatorOutput.UpperBand), "UpperBand" },
        { (IndicatorName.KeltnerChannels, IndicatorOutput.MiddleBand), "MiddleBand" },
        { (IndicatorName.KeltnerChannels, IndicatorOutput.LowerBand), "LowerBand" },

        // Ichimoku Cloud outputs
        { (IndicatorName.IchimokuCloud, IndicatorOutput.Primary), "TenkanSen" },
        { (IndicatorName.IchimokuCloud, IndicatorOutput.Signal), "KijunSen" },
        { (IndicatorName.IchimokuCloud, IndicatorOutput.UpperBand), "SenkouSpanA" },
        { (IndicatorName.IchimokuCloud, IndicatorOutput.LowerBand), "SenkouSpanB" },
    };

    private static readonly object RegistryLock = new();

    /// <summary>
    /// Gets the output key for an indicator and output type.
    /// </summary>
    /// <param name="name">The indicator name.</param>
    /// <param name="output">The output type.</param>
    /// <returns>The output key string, or null if using default output.</returns>
    public static string? GetOutputKey(IndicatorName name, IndicatorOutput output)
    {
        // Primary output typically uses CustomValuesList, not OutputValues
        if (output == IndicatorOutput.Primary)
        {
            // Check if there's a specific mapping for this indicator's primary output
            lock (RegistryLock)
            {
                if (OutputKeyMap.TryGetValue((name, output), out var key))
                {
                    return key;
                }
            }
            return null;
        }

        lock (RegistryLock)
        {
            if (OutputKeyMap.TryGetValue((name, output), out var key))
            {
                return key;
            }
        }

        // Fallback mappings for common output types
        return output switch
        {
            IndicatorOutput.UpperBand => "UpperBand",
            IndicatorOutput.MiddleBand => "MiddleBand",
            IndicatorOutput.LowerBand => "LowerBand",
            IndicatorOutput.Signal => "Signal",
            IndicatorOutput.Histogram => "Histogram",
            _ => null
        };
    }

    /// <summary>
    /// Registers a custom output key mapping.
    /// </summary>
    /// <param name="name">The indicator name.</param>
    /// <param name="output">The output type.</param>
    /// <param name="key">The output key string.</param>
    public static void Register(IndicatorName name, IndicatorOutput output, string key)
    {
        lock (RegistryLock)
        {
            OutputKeyMap[(name, output)] = key;
        }
    }

    /// <summary>
    /// Checks if an output key mapping exists.
    /// </summary>
    /// <param name="name">The indicator name.</param>
    /// <param name="output">The output type.</param>
    /// <returns>True if a mapping exists.</returns>
    public static bool HasMapping(IndicatorName name, IndicatorOutput output)
    {
        lock (RegistryLock)
        {
            return OutputKeyMap.ContainsKey((name, output));
        }
    }
}
