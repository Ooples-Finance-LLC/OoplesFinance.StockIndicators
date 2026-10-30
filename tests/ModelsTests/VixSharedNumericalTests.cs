using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class VixSharedNumericalTests
{
    public static IEnumerable<object[]> Cases =>
        IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
            .Where(c => c.IndicatorType == typeof(VixTradingSystem))
            .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public Task PublishedConfigurationsKeepIndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new ValidationTests.OrdinalFamilyNumericalTests()
            .PublicConfigurationsPassEveryNumericalClass(testCase);
}
