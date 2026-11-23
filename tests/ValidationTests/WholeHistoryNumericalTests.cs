using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>The whole-history families pass the same numerical fixtures as every per-bar family.</summary>
public sealed class WholeHistoryNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RegressionWithChannels) || c.IndicatorType == typeof(RegressionWithFinalLine)
            || c.IndicatorType == typeof(RetrospectiveFractals)).Select(c => new object[] { c });

    [Fact]
    public void AllThreeFamiliesAreCovered() =>
        Assert.Equal(3, Cases.Select(c => ((IndicatorValidationCase)c[0]).IndicatorType).Distinct().Count());

    [Theory, MemberData(nameof(Cases))]
    public Task NumericalFixturesAreEnrolled(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
