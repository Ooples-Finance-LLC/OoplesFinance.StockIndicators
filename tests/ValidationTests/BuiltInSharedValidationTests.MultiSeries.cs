using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed partial class BuiltInSharedValidationTests
{
    public static IEnumerable<object[]> MultiSeriesCases => IndicatorValidationDiscovery
        .DiscoverMultiSeries(new[] { typeof(IIndicator).Assembly }).Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(MultiSeriesCases))]
    public void EveryMultiSeriesConfigurationSatisfiesIndependentFormulas(MultiSeriesIndicatorValidationCase testCase)
        => MultiSeriesIndicatorValidation.ValidateAndThrow(testCase, new() { RequireFormulaReference = true });
}
