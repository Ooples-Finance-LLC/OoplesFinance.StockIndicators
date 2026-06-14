using System.Reflection;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Invokes indicator calculations dynamically by <see cref="IndicatorName"/> and returns the FULL per-indicator
/// breadth — every named intermediate series (<see cref="StockData.OutputValues"/>) plus the primary
/// (<see cref="StockData.CustomValuesList"/>). This is the SUPPORTED breadth-harvest surface for ML feature
/// engineering (thousands of candidate series across an Input × MovingAvgType × length grid).
/// </summary>
/// <remarks>
/// NOT deprecated. For typed, single-output use of one indicator, prefer the source-generated V2
/// <c>StatefulIndicatorFactory</c> (compile-time safety, faster). This reflection-based invoker is the
/// intentional path when you need the full multi-series breadth that the single-output V2 factory collapses
/// away — e.g. the feature-universe harvester. It is the one supported reason the full-output Calculate
/// surface is retained, so callers never need to touch the deprecated direct <c>Calculate*</c> extension methods.
/// </remarks>
public static class IndicatorInvoker
{
    private static readonly Dictionary<IndicatorName, MethodInfo> MethodCache = new();
    private static readonly object CacheLock = new();
    private static bool _initialized;

    /// <summary>
    /// Invokes an indicator calculation by name.
    /// </summary>
    /// <param name="data">The stock data to calculate on.</param>
    /// <param name="name">The indicator name.</param>
    /// <param name="parameters">Optional parameters for the indicator.</param>
    /// <returns>The calculated stock data with indicator values.</returns>
    public static StockData Invoke(StockData data, IndicatorName name, params object[] parameters)
    {
        EnsureInitialized();

        if (!MethodCache.TryGetValue(name, out var method))
        {
            throw new NotSupportedException($"Indicator '{name}' is not supported or has no corresponding Calculate method.");
        }

        try
        {
            // Build parameter array: first parameter is always stockData, then optional parameters
            var methodParams = method.GetParameters();

            // Detect extra parameters that the method doesn't accept
            if (parameters.Length > methodParams.Length - 1)
            {
                throw new ArgumentException($"Too many parameters for indicator '{name}'. Expected at most {methodParams.Length - 1}, got {parameters.Length}.");
            }

            var args = new object[methodParams.Length];
            args[0] = data; // StockData is always first parameter

            // Fill in provided parameters or use defaults
            var paramIndex = 0;
            for (var i = 1; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                if (paramIndex < parameters.Length)
                {
                    args[i] = ConvertParameter(parameters[paramIndex], param.ParameterType);
                    paramIndex++;
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue ?? GetDefaultValue(param.ParameterType);
                }
                else
                {
                    throw new ArgumentException($"Missing required parameter '{param.Name}' for indicator '{name}'.");
                }
            }

            var result = method.Invoke(null, args);
            return result as StockData ?? data;
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException($"Error calculating indicator '{name}': {ex.InnerException?.Message}", ex.InnerException);
        }
    }

    /// <summary>
    /// Gets the method info for an indicator by name.
    /// </summary>
    public static MethodInfo? GetMethod(IndicatorName name)
    {
        EnsureInitialized();
        return MethodCache.TryGetValue(name, out var method) ? method : null;
    }

    /// <summary>
    /// Gets the parameter info for an indicator.
    /// </summary>
    public static ParameterInfo[]? GetParameters(IndicatorName name)
    {
        var method = GetMethod(name);
        return method?.GetParameters().Skip(1).ToArray(); // Skip StockData parameter
    }

    /// <summary>
    /// Checks if an indicator is supported.
    /// </summary>
    public static bool IsSupported(IndicatorName name)
    {
        EnsureInitialized();
        return MethodCache.ContainsKey(name);
    }

    /// <summary>
    /// Gets all supported indicator names.
    /// </summary>
    public static IReadOnlyCollection<IndicatorName> GetSupportedIndicators()
    {
        EnsureInitialized();
        return MethodCache.Keys.ToList().AsReadOnly();
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (CacheLock)
        {
            if (_initialized) return;

            // Get all Calculate* methods from the Calculations class
            var calculationsType = typeof(Calculations);
            var methods = calculationsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("Calculate", StringComparison.Ordinal))
                .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(StockData))
                .Where(m => m.ReturnType == typeof(StockData));

            foreach (var method in methods)
            {
                // Extract indicator name from method name (e.g., "CalculateSimpleMovingAverage" -> "SimpleMovingAverage")
                var indicatorName = method.Name.Substring("Calculate".Length);

                // Try to match with IndicatorName enum
                if (Enum.TryParse<IndicatorName>(indicatorName, out var name) && name != IndicatorName.None)
                {
                    MethodCache[name] = method;
                }
            }

            _initialized = true;
        }
    }

    private static object ConvertParameter(object value, Type targetType)
    {
        if (value is null)
        {
            return GetDefaultValue(targetType);
        }

        var valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
        {
            return value;
        }

        // Handle numeric conversions
        if (targetType == typeof(int) && value is double d)
        {
            return (int)d;
        }

        if (targetType == typeof(double) && value is int i)
        {
            return (double)i;
        }

        // Handle enum conversions
        if (targetType.IsEnum && value is string s)
        {
            return Enum.Parse(targetType, s);
        }

        if (targetType.IsEnum && value is int enumInt)
        {
            return Enum.ToObject(targetType, enumInt);
        }

        // Use Convert as last resort
        return Convert.ChangeType(value, targetType);
    }

    private static object GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type) ?? 0;
        }

        return null!;
    }
}
