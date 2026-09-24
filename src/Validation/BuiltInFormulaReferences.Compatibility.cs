namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static IndicatorValidationRule FullReference(int slot,
        Func<IReadOnlyList<Indicators.Bar>, IReadOnlyList<double>> reference) =>
        IndicatorValidationRule.Reference(slot, reference, new IndicatorErrorBudget(1e-9, 1e-9), includeWarmup: true);

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
    {
        if (minimum > maximum) throw new ArgumentException("Minimum exceeds maximum.");
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    // Keep reference calculations executable on every supported target framework.
    private static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum) throw new ArgumentException("Minimum exceeds maximum.");
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        if (minimum > maximum) throw new ArgumentException("Minimum exceeds maximum.");
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }
}

#if NETFRAMEWORK
internal static class ValidationEnumerableCompatibility
{
    internal static IEnumerable<T> Append<T>(this IEnumerable<T> source, T value)
    {
        foreach (var item in source) yield return item;
        yield return value;
    }
    internal static IEnumerable<T> Prepend<T>(this IEnumerable<T> source, T value)
    {
        yield return value;
        foreach (var item in source) yield return item;
    }
    internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);
}
#endif
