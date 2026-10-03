using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KeltnerMiddleAliasNumericalTests
{
    [Fact]
    public async Task ExistingAliasConfigurationsRetainAllRoutes()
    {
        var cases = IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
            .Where(c => c.IndicatorType == typeof(KeltnerChannelMiddle)).ToArray();
        Assert.NotEmpty(cases);
        var checks = new OrdinalFamilyNumericalTests();
        foreach (var testCase in cases)
        {
            foreach (var route in new[] { "batch", "fast", "arm", "native", "streaming" })
                checks.CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.KeltnerOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
            await checks.SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
            await checks.PublicConfigurationsPassEveryNumericalClass(testCase);
        }
    }
}
