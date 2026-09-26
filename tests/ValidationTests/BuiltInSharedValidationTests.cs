using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed partial class BuiltInSharedValidationTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery
        .Discover(new[] { typeof(IIndicator).Assembly }).Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryConfigurationSatisfiesTheSharedInvariants(IndicatorValidationCase testCase)
        => await IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });
}
