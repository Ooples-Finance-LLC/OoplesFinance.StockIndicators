using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FoundationCompositionNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Name.StartsWith("foundation-composition/", StringComparison.Ordinal))
        .Select(c => new object[] { c });

    [Fact]
    public void SharedDiscoveryIncludesEveryPromotedComposition()
    {
        var cases = Cases.Select(row => (IndicatorValidationCase)row[0]).ToArray();
        Assert.Equal(36, cases.Length);
        foreach (var type in new[] { typeof(Tma), typeof(TriangularMovingAverage), typeof(SlowSmoothedMovingAverage) })
            Assert.Equal(12, cases.Count(c => c.IndicatorType == type));
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task ComposedWeightedStagesReceiveAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }
}
