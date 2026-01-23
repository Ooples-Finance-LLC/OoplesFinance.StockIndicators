#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

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

    // Cached base input to avoid repeated ToArray() calls
    private double[]? _cachedDefaultInput;

    // Reusable HashSet for cycle detection (avoid allocation per Evaluate call)
    private readonly HashSet<SeriesHandle> _visitingSet = new();

    /// <summary>
    /// Creates a new series evaluator with single-symbol data (backwards compatible).
    /// </summary>
    public SeriesEvaluator(StockData data, Dictionary<SeriesHandle, SeriesNode> nodes)
    {
        _defaultData = data;
        _dataByKey = new Dictionary<SeriesKey, StockData>();
        _nodes = nodes;
        _cache = new Dictionary<SeriesHandle, double[]>();
    }

    /// <summary>
    /// Creates a new series evaluator with multi-symbol data support.
    /// </summary>
    public SeriesEvaluator(Dictionary<SeriesKey, StockData> dataByKey, StockData defaultData, Dictionary<SeriesHandle, SeriesNode> nodes)
    {
        _dataByKey = dataByKey;
        _defaultData = defaultData;
        _nodes = nodes;
        _cache = new Dictionary<SeriesHandle, double[]>();
    }

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
            var result = ApplyIndicator(baseData, node.Spec);
            return ExtractOutput(result, node.Spec);
        }

        // Chained indicator path: need to clone with custom input values
        var input = Resolve(node.Input.Value);
        var working = CloneWithCustomValues(baseData, input);
        var result2 = ApplyIndicator(working, node.Spec);
        return ExtractOutput(result2, node.Spec);
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

            var defaultInput = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
            _cachedDefaultInput = defaultInput.ToArray();
            return _cachedDefaultInput;
        }

        var input = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        return input.ToArray();
    }

    private static StockData CloneWithCustomValues(StockData baseData, double[] customValues)
    {
        var clone = new StockData(baseData.TickerDataList, baseData.InputName)
        {
            Options = baseData.Options,
            CustomValuesList = new List<double>(customValues)
        };
        return clone;
    }

    private static StockData ApplyIndicator(StockData data, IndicatorSpec spec)
    {
        // Handle known typed options first for performance
        switch (spec.Options)
        {
            case SmaSpecOptions sma:
                return data.CalculateSimpleMovingAverage(sma.Length);
            case EmaSpecOptions ema:
                return data.CalculateExponentialMovingAverage(length: ema.Length);
            case RsiSpecOptions rsi:
                return data.CalculateRelativeStrengthIndex(length: rsi.Length);
            case MacdSpecOptions macd:
                return data.CalculateMovingAverageConvergenceDivergence(
                    fastLength: macd.FastLength,
                    slowLength: macd.SlowLength,
                    signalLength: macd.SignalLength);
            case BollingerBandsSpecOptions bb:
                return data.CalculateBollingerBands(
                    length: bb.Length,
                    stdDevMult: bb.StdDevMult);
            case AtrSpecOptions atr:
                return data.CalculateAverageTrueRange(length: atr.Length);
            case AdxSpecOptions adx:
                return data.CalculateAverageDirectionalIndex(length: adx.Length);
            case StochasticSpecOptions stoch:
                return data.CalculateStochasticOscillator(
                    length: stoch.KLength,
                    smoothLength1: stoch.DLength);
            case GenericIndicatorOptions generic:
                // Use reflection-based dispatch for all other indicators
                return IndicatorInvoker.Invoke(data, spec.Name, generic.Parameters);
            default:
                throw new NotSupportedException($"Indicator '{spec.Name}' with options type '{spec.Options?.GetType().Name}' not supported.");
        }
    }

    private static double[] ExtractOutput(StockData result, IndicatorSpec spec)
    {
        var key = IndicatorOutputRegistry.GetOutputKey(spec.Name, spec.Output);
        if (key is not null && result.OutputValues.TryGetValue(key, out var list))
        {
            return list.ToArray();
        }

        return result.CustomValuesList.ToArray();
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

        // Stochastic outputs (K/D lines)
        { (IndicatorName.StochasticOscillator, IndicatorOutput.Signal), "SignalFastK" },

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
