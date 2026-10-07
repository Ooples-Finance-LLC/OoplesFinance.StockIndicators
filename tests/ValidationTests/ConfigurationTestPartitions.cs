using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

internal static class ConfigurationTestPartitions
{
    internal const int Count = 20;

    internal static IEnumerable<object[]> Select(IEnumerable<IndicatorValidationCase> cases, int partition)
    {
        if (partition < 0 || partition >= Count)
            throw new ArgumentOutOfRangeException(nameof(partition));
        return cases.OrderBy(c => c.ToString(), StringComparer.Ordinal)
            .Where((_, index) => index % Count == partition)
            .Select(c => new object[] { c });
    }
}
