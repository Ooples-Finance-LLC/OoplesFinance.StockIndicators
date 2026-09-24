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
    private static readonly ConcurrentDictionary<(Type Options, IndicatorName Name), ArgumentSource?[]> ArgumentMaps = new();

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

        // The caller's own series, which is the chained one: the published list is empty or rounded when the
        // publication options say so, and the indicator must compute on what the caller chained.
        var bars = new StockData(data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.Dates);
        if (data.ChainedValues.Count > 0)
        {
            bars.SetInputSeries(new List<double>(data.ChainedValues));
        }
        else
        {
            bars.InputValues = new List<double>(data.InputValues);
        }

        var parameters = method.GetParameters();
        var map = ArgumentMaps.GetOrAdd((spec.Options.GetType(), target.Name), key => MapArguments(key.Options, target, parameters));
        var args = new object?[parameters.Length];
        args[0] = bars;
        for (var i = 1; i < parameters.Length; i++)
        {
            if (map[i] is { } source)
            {
                args[i] = Convert(source.Read(spec.Options), parameters[i].ParameterType);
                continue;
            }

            // A parameter the options do not map and the method does not default cannot be supplied from one
            // series. DefaultValue answers DBNull for it, which reflection rejects with a message about DBNull
            // and the parameter's type - an account of how the call was assembled rather than of what the
            // caller asked for. A comparison indicator takes its second series that way, so it reaches this.
            if (!parameters[i].HasDefaultValue)
            {
                throw new CalculationException(
                    $"{target.Name} cannot be computed from one series: its '{parameters[i].Name}' argument is "
                    + $"not supplied by {spec.Options.GetType().Name} and has no default.");
            }

            args[i] = parameters[i].DefaultValue;
        }

        var result = method.Invoke(null, args) as StockData ?? bars;

        // The key the caller named, else the one this arm stands for. A spec naming neither wants the
        // indicator's own series, which is where a single-output indicator publishes it.
        var key = spec.OutputKey ?? target.OutputKey
            ?? OoplesFinance.StockIndicators.Indicators.GeneratedExpandedPrimary.KeyFor(spec.Options.GetType());
        // These legacy calculations intentionally publish only named outputs. A
        // typed streaming spec still has a default value; preserve that selection
        // without turning the generated multi-output facade into a scalar alias.
        if (key is null && result.CustomValuesList.Count == 0)
            key = target.Name switch
            {
                IndicatorName.EhlersDominantCycleTunedBypassFilter => "V2",
                IndicatorName.EhlersFourierSeriesAnalysis => "Wave",
                IndicatorName.VervoortModifiedBollingerBandIndicator => "PercentB",
                _ => null
            };
        if (key is null)
        {
            return result.CustomValuesList;
        }

        if (!result.ChainedOutputs.TryGetValue(key, out var series))
        {
            // A key naming a series the indicator does not publish. Answering with the series it does publish
            // is exactly how "SignalFastK" passed for a D line: two slots over one series, silently.
            //
            // A caller who named the key is told which key, not which slot: saying "does not publish a Primary
            // output" of a request that asked for "Signal" would describe the wrong thing, since a key-addressed
            // spec carries Primary by construction. The wording matches SeriesEvaluator's for the same failure.
            throw DoesNotPublishKey(target.Name, key);
        }

        return series;
    }

    /// <summary>
    /// The refusal for a key the indicator does not publish, worded as <c>SeriesEvaluator</c> words it.
    /// </summary>
    /// <remarks>
    /// #186 replaced this substitution with a raise in the other resolver, and recorded why there: an output the
    /// indicator does not produce is an error, not a series. The same message is used here so a caller cannot
    /// tell which of the resolvers refused, and so all of them name what the indicator does publish. Internal
    /// rather than private for that reason: the streaming registration in <c>IndicatorRuntime</c> refuses the
    /// same mistake, and a second wording there would be a second account of one failure. See PR #230.
    /// </remarks>
    internal static CalculationException DoesNotPublishKey(IndicatorName name, string outputKey)
    {
        return new CalculationException(
            $"{name} does not publish an output named '{outputKey}'. Available outputs: {Available(name)}.");
    }

    private static string Available(IndicatorName name)
    {
        var available = GeneratedIndicatorOutputs.KeysFor(name);
        return available.Count == 0 ? "none" : string.Join(", ", available);
    }

    /// <summary>
    /// The options' public properties that reach no parameter of the batch method, so would be ignored: neither
    /// named like one nor declared as one's argument, and not marked obsolete as having no effect.
    /// </summary>
    public static IReadOnlyList<string> UnmappedProperties(Type optionsType, BuilderArmTarget target)
    {
        var parameters = IndicatorInvoker.GetMethod(target.Name)?.GetParameters().Skip(1).Select(p => p.Name ?? string.Empty)
            ?? Enumerable.Empty<string>();
        var names = new HashSet<string>(parameters, StringComparer.OrdinalIgnoreCase);
        var declared = new HashSet<string>(target.Arguments.Select(a => a.Property), StringComparer.Ordinal);
        return optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !names.Contains(p.Name) && !declared.Contains(p.Name) && p.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(p => p.Name)
            .ToList();
    }

    /// <summary>The declared arguments that name no property of the options, or no parameter of the batch method.</summary>
    public static IReadOnlyList<string> InvalidArguments(Type optionsType, BuilderArmTarget target)
    {
        var parameters = new HashSet<string>(
            IndicatorInvoker.GetMethod(target.Name)?.GetParameters().Skip(1).Select(p => p.Name ?? string.Empty)
                ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);
        var invalid = new List<string>();
        foreach (var argument in target.Arguments)
        {
            if (optionsType.GetProperty(argument.Property, BindingFlags.Public | BindingFlags.Instance) is null)
            {
                invalid.Add($"{argument.Property} is not a property of {optionsType.Name}");
            }

            if (!parameters.Contains(argument.Parameter))
            {
                invalid.Add($"{argument.Parameter} is not a parameter of {target.Name}");
            }
        }

        return invalid;
    }

    // Each batch parameter reads the property declared for it, else the property of the same name. An option
    // marked obsolete has no effect and is never passed.
    private static ArgumentSource?[] MapArguments(Type optionsType, BuilderArmTarget target, ParameterInfo[] parameters)
    {
        var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var map = new ArgumentSource?[parameters.Length];
        for (var i = 1; i < parameters.Length; i++)
        {
            var parameter = parameters[i].Name ?? string.Empty;
            var declared = target.Arguments.FirstOrDefault(a => string.Equals(a.Parameter, parameter, StringComparison.Ordinal));
            var property = declared.Property is { } declaredName
                ? properties.FirstOrDefault(p => p.Name == declaredName)
                : properties.FirstOrDefault(p => string.Equals(p.Name, parameter, StringComparison.OrdinalIgnoreCase)
                    && p.GetCustomAttribute<ObsoleteAttribute>() is null);
            map[i] = property is null ? null : new ArgumentSource(property, declared.Property is null ? null : declared.Convert);
        }

        return map;
    }

    private sealed class ArgumentSource
    {
        private readonly PropertyInfo _property;
        private readonly Func<object?, object?>? _convert;

        public ArgumentSource(PropertyInfo property, Func<object?, object?>? convert)
        {
            _property = property;
            _convert = convert;
        }

        public object? Read(object options)
        {
            var value = _property.GetValue(options);
            return _convert is null ? value : _convert(value);
        }
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
    private readonly BuilderArgument[]? _arguments;

    public BuilderArmTarget(IndicatorName name, string? outputKey = null, params BuilderArgument[] arguments)
    {
        Name = name;
        OutputKey = outputKey;
        _arguments = arguments;
    }

    public IndicatorName Name { get; }

    public string? OutputKey { get; }

    /// <summary>Options passed to batch parameters of another name; every other option goes by its own name.</summary>
    public IReadOnlyList<BuilderArgument> Arguments => _arguments ?? Array.Empty<BuilderArgument>();
}

/// <summary>An options property passed to a batch parameter of another name, converted first when given.</summary>
internal readonly struct BuilderArgument
{
    public BuilderArgument(string property, string parameter, Func<object?, object?>? convert = null)
    {
        Property = property;
        Parameter = parameter;
        Convert = convert;
    }

    public string Property { get; }

    public string Parameter { get; }

    public Func<object?, object?>? Convert { get; }
}
