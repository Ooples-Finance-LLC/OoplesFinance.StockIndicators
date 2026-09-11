using System.Collections.Concurrent;
using System.Reflection;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Builder.Compute;

/// <summary>
/// Computes a typed spec with the batch indicator it stands for.
/// </summary>
/// <remarks>
/// <para>
/// A typed spec's fast arm is served only once <c>BuilderArmTests</c> has shown it computes what its batch
/// indicator computes. Most had not: of the arms a test could compare, over half disagreed - RSI, ATR, ADX and
/// the stochastic among them, reachable from the public catalogue - by a warmup convention, a different formula,
/// or by computing another indicator altogether. Every other typed spec is computed here, by the batch method,
/// with the spec's options passed as the arguments of the same name.
/// </para>
/// <para>
/// The batch method runs on a copy of the bars. A calculation publishes its outputs onto the StockData it is
/// given, and the evaluator hands every indicator the same one.
/// </para>
/// </remarks>
internal static class BuilderArmBinding
{
    private static readonly ConcurrentDictionary<(Type Options, IndicatorName Name), PropertyInfo?[]> ArgumentMaps = new();

    /// <summary>
    /// The batch indicator and output a typed options type stands for, or false for an unbound type.
    /// </summary>
    public static bool TryGetTarget(Type optionsType, out BuilderArmTarget target) =>
        BuilderArmTargets.Targets.TryGetValue(optionsType, out target);

    /// <summary>
    /// The spec computed by its batch indicator into a pooled buffer, or null when its options type is unbound.
    /// </summary>
    public static ComputeBuffer? TryCompute(StockData data, IndicatorSpec spec, ComputeContext context)
    {
        if (!TryGetTarget(spec.Options.GetType(), out var target))
        {
            return null;
        }

        var values = Compute(data, spec, target);
        var buffer = context.Rent(values.Count);
        var span = buffer.WritableSpan;
        for (var i = 0; i < values.Count; i++)
        {
            span[i] = values[i];
        }

        return buffer;
    }

    /// <summary>The spec's values from its batch indicator.</summary>
    public static List<double> Compute(StockData data, IndicatorSpec spec, BuilderArmTarget target)
    {
        var method = IndicatorInvoker.GetMethod(target.Name)
            ?? throw new NotSupportedException($"{target.Name} has no batch calculation to compute {spec.Options.GetType().Name} with.");

        var bars = new StockData(data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.Dates, data.InputName);
        if (data.CustomValuesList.Count > 0)
        {
            bars.SetInputSeries(new List<double>(data.CustomValuesList));
        }

        var parameters = method.GetParameters();
        var map = ArgumentMaps.GetOrAdd((spec.Options.GetType(), target.Name), key => MapArguments(key.Options, parameters));
        var args = new object?[parameters.Length];
        args[0] = bars;
        for (var i = 1; i < parameters.Length; i++)
        {
            args[i] = map[i] is { } property
                ? Convert(property.GetValue(spec.Options), parameters[i].ParameterType)
                : parameters[i].DefaultValue;
        }

        var result = method.Invoke(null, args) as StockData ?? bars;
        var key = spec.Output == IndicatorOutput.Primary
            ? target.OutputKey
            : IndicatorOutputRegistry.GetOutputKey(target.Name, spec.Output);
        if (key is null)
        {
            return result.CustomValuesList;
        }

        return result.OutputValues.TryGetValue(key, out var series)
            ? series
            : throw new NotSupportedException($"{target.Name} publishes no {key} output for {spec.Options.GetType().Name}.");
    }

    /// <summary>
    /// The options' public properties that name no parameter of the batch method, so would be ignored.
    /// </summary>
    public static IReadOnlyList<string> UnmappedProperties(Type optionsType, IndicatorName name)
    {
        var method = IndicatorInvoker.GetMethod(name);
        if (method is null)
        {
            return optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
        }

        var names = new HashSet<string>(method.GetParameters().Skip(1).Select(p => p.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase);
        return optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !names.Contains(p.Name))
            .Select(p => p.Name)
            .ToList();
    }

    private static PropertyInfo?[] MapArguments(Type optionsType, ParameterInfo[] parameters)
    {
        var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var map = new PropertyInfo?[parameters.Length];
        for (var i = 1; i < parameters.Length; i++)
        {
            map[i] = properties.FirstOrDefault(p => string.Equals(p.Name, parameters[i].Name, StringComparison.OrdinalIgnoreCase));
        }

        return map;
    }

    private static object? Convert(object? value, Type parameterType)
    {
        if (value is null || parameterType.IsInstanceOfType(value))
        {
            return value;
        }

        var target = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        return target.IsEnum ? Enum.ToObject(target, value) : System.Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>The batch indicator a typed options type stands for, and the output it reads, primary when null.</summary>
internal readonly struct BuilderArmTarget
{
    public BuilderArmTarget(IndicatorName name, string? outputKey = null)
    {
        Name = name;
        OutputKey = outputKey;
    }

    public IndicatorName Name { get; }

    public string? OutputKey { get; }
}
