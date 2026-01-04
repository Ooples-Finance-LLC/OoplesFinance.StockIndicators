using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class IndicatorDefinition
{
    public IndicatorDefinition(IndicatorName name, IndicatorType type, Func<StockData, StockData> calculator)
    {
        Name = name;
        Type = type;
        Calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public IndicatorName Name { get; }
    public IndicatorType Type { get; }
    public Func<StockData, StockData> Calculator { get; }
}

public sealed class IndicatorFilter
{
    public IndicatorType? Category { get; set; }
    public IReadOnlyCollection<IndicatorName>? IncludeNames { get; set; }
    public IReadOnlyCollection<IndicatorName>? ExcludeNames { get; set; }
    public IndicatorCost? MaxCost { get; set; }
    public Func<IndicatorName, IndicatorCost>? CostProvider { get; set; }
    public Func<IndicatorDefinition, bool>? Predicate { get; set; }

    public bool Matches(IndicatorDefinition definition)
    {
        if (Category.HasValue && definition.Type != Category.Value)
        {
            return false;
        }

        if (IncludeNames != null && IncludeNames.Count > 0 && !Contains(IncludeNames, definition.Name))
        {
            return false;
        }

        if (ExcludeNames != null && Contains(ExcludeNames, definition.Name))
        {
            return false;
        }

        if (MaxCost.HasValue)
        {
            var cost = CostProvider != null
                ? CostProvider(definition.Name)
                : IndicatorCostMap.GetCost(definition.Name);
            if (cost > MaxCost.Value)
            {
                return false;
            }
        }

        if (Predicate != null && !Predicate(definition))
        {
            return false;
        }

        return true;
    }

    private static bool Contains(IReadOnlyCollection<IndicatorName> collection, IndicatorName value)
    {
        foreach (var item in collection)
        {
            if (item == value)
            {
                return true;
            }
        }

        return false;
    }
}

public static class IndicatorRegistry
{
    private static readonly Lazy<IReadOnlyList<IndicatorDefinition>> Definitions = new(BuildDefinitions, true);
    private static readonly Dictionary<string, IndicatorName> IndicatorNameMap = BuildIndicatorNameMap();

    public static IReadOnlyList<IndicatorDefinition> GetDefinitions() => Definitions.Value;

    private static IReadOnlyList<IndicatorDefinition> BuildDefinitions()
    {
        var definitions = new List<IndicatorDefinition>();
        var methods = typeof(Calculations).GetMethods(BindingFlags.Public | BindingFlags.Static);

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];
            if (!method.Name.StartsWith("Calculate", StringComparison.Ordinal))
            {
                continue;
            }

            if (method.ReturnType != typeof(StockData))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(StockData))
            {
                continue;
            }

            var suffix = method.Name.Substring("Calculate".Length);
            var normalized = Normalize(suffix);
            if (!IndicatorNameMap.TryGetValue(normalized, out var indicatorName))
            {
                continue;
            }

            if (!TryCreateCalculator(method, parameters, out var calculator))
            {
                continue;
            }

            var indicatorType = indicatorName.GetIndicatorType();
            definitions.Add(new IndicatorDefinition(indicatorName, indicatorType, calculator));
        }

        var unique = new Dictionary<IndicatorName, IndicatorDefinition>();
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (!unique.ContainsKey(definition.Name))
            {
                unique.Add(definition.Name, definition);
            }
        }

        var list = new List<IndicatorDefinition>(unique.Count);
        foreach (var definition in unique.Values)
        {
            list.Add(definition);
        }

        list.Sort((left, right) => left.Name.CompareTo(right.Name));
        return list;
    }

    private static bool TryCreateCalculator(MethodInfo method, ParameterInfo[] parameters,
        out Func<StockData, StockData> calculator)
    {
        var stockParam = Expression.Parameter(typeof(StockData), "stockData");
        var args = new Expression[parameters.Length];
        args[0] = stockParam;

        for (var i = 1; i < parameters.Length; i++)
        {
            if (!TryGetDefaultValue(parameters[i], out var value))
            {
                calculator = null!;
                return false;
            }

            args[i] = Expression.Constant(value, parameters[i].ParameterType);
        }

        var call = Expression.Call(method, args);
        var lambda = Expression.Lambda<Func<StockData, StockData>>(call, stockParam);
        calculator = lambda.Compile();
        return true;
    }

    private static bool TryGetDefaultValue(ParameterInfo parameter, out object? value)
    {
        if (parameter.HasDefaultValue)
        {
            value = parameter.DefaultValue;
            if (value == DBNull.Value || value == Missing.Value)
            {
                value = GetDefaultValue(parameter.ParameterType);
            }

            return true;
        }

        if (parameter.IsOptional)
        {
            value = GetDefaultValue(parameter.ParameterType);
            return true;
        }

        value = null;
        return false;
    }

    private static object? GetDefaultValue(Type type)
    {
        if (!type.IsValueType)
        {
            return null;
        }

        return Activator.CreateInstance(type);
    }

    private static Dictionary<string, IndicatorName> BuildIndicatorNameMap()
    {
        var map = new Dictionary<string, IndicatorName>(StringComparer.Ordinal);
        var values = (IndicatorName[])Enum.GetValues(typeof(IndicatorName));

        for (var i = 0; i < values.Length; i++)
        {
            var name = values[i];
            var normalized = Normalize(name.ToString());
            if (!map.ContainsKey(normalized))
            {
                map.Add(normalized, name);
            }
        }

        return map;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '_')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
