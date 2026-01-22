#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Evaluates series in a computation graph.
/// </summary>
internal sealed class SeriesEvaluator
{
    private readonly StockData _data;
    private readonly Dictionary<SeriesHandle, SeriesNode> _nodes;
    private readonly Dictionary<SeriesHandle, double[]> _cache;

    public SeriesEvaluator(StockData data, Dictionary<SeriesHandle, SeriesNode> nodes)
    {
        _data = data;
        _nodes = nodes;
        _cache = new Dictionary<SeriesHandle, double[]>();
    }

    public Dictionary<SeriesHandle, double[]> Evaluate(IReadOnlyCollection<SeriesHandle> handles)
    {
        var result = new Dictionary<SeriesHandle, double[]>();
        foreach (var handle in handles)
        {
            result[handle] = Resolve(handle, new HashSet<SeriesHandle>());
        }

        return result;
    }

    public double[] Evaluate(SeriesHandle handle)
    {
        return Resolve(handle, new HashSet<SeriesHandle>());
    }

    private double[] Resolve(SeriesHandle handle, HashSet<SeriesHandle> visiting)
    {
        if (_cache.TryGetValue(handle, out var cached))
        {
            return cached;
        }

        if (!visiting.Add(handle))
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
                resolved = GetBaseInput();
                break;
            case SeriesNodeKind.Indicator:
                resolved = ResolveIndicator(node, visiting);
                break;
            case SeriesNodeKind.Formula:
                resolved = ResolveFormula(node, visiting);
                break;
            default:
                throw new InvalidOperationException("Unknown series node kind.");
        }

        visiting.Remove(handle);
        _cache[handle] = resolved;
        return resolved;
    }

    private double[] ResolveIndicator(SeriesNode node, HashSet<SeriesHandle> visiting)
    {
        if (!node.Input.HasValue || node.Spec == null)
        {
            throw new InvalidOperationException("Indicator node missing input or spec.");
        }

        var input = Resolve(node.Input.Value, visiting);
        var working = CloneWithCustomValues(_data, input);
        var result = ApplyIndicator(working, node.Spec);
        return ExtractOutput(result, node.Spec);
    }

    private double[] ResolveFormula(SeriesNode node, HashSet<SeriesHandle> visiting)
    {
        if (!node.Left.HasValue || !node.Right.HasValue || node.Formula == null)
        {
            throw new InvalidOperationException("Formula node missing operands or function.");
        }

        var left = Resolve(node.Left.Value, visiting);
        var right = Resolve(node.Right.Value, visiting);
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

    private double[] GetBaseInput()
    {
        var input = _data.CustomValuesList.Count > 0 ? _data.CustomValuesList : _data.InputValues;
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
        var key = GetOutputKey(spec.Name, spec.Output);
        if (key is not null && result.OutputValues.TryGetValue(key, out var list))
        {
            return list.ToArray();
        }

        return result.CustomValuesList.ToArray();
    }

    private static string? GetOutputKey(IndicatorName name, IndicatorOutput output)
    {
        if (name == IndicatorName.MovingAverageConvergenceDivergence)
        {
            return output switch
            {
                IndicatorOutput.Signal => "Signal",
                IndicatorOutput.Histogram => "Histogram",
                _ => null
            };
        }

        if (name == IndicatorName.BollingerBands)
        {
            return output switch
            {
                IndicatorOutput.UpperBand => "UpperBand",
                IndicatorOutput.LowerBand => "LowerBand",
                _ => "MiddleBand"
            };
        }

        return null;
    }
}
