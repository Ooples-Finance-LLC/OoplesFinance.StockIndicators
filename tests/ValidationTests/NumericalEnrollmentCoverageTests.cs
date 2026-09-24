using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class NumericalEnrollmentCoverageTests
{
    [Fact]
    public void MissingNumericalConfigurationsCannotGrowOrHideCompletedWork()
    {
        using var stream = typeof(NumericalEnrollmentCoverageTests).Assembly
            .GetManifestResourceStream("NumericalFixtureBacklog.txt");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var recorded = reader.ReadToEnd().Split('\n').Select(line => line.Trim())
            .Where(line => line.Length != 0 && !line.StartsWith('#')).ToArray();
        Assert.Equal(recorded.OrderBy(name => name, StringComparer.Ordinal).Distinct(StringComparer.Ordinal), recorded);
        var cases = IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly });
        Assert.NotEmpty(cases);
        Assert.Equal(cases.Count, cases.Select(testCase => testCase.ToString()).Distinct(StringComparer.Ordinal).Count());
        var missing = cases.Where(testCase => !IndicatorValidation.IncludesNumericalFixtures(testCase.Factory()))
            .Select(testCase => testCase.ToString()).ToArray();
        var added = missing.Except(recorded, StringComparer.Ordinal).ToArray();
        var completed = recorded.Except(missing, StringComparer.Ordinal).ToArray();
        Assert.True(added.Length == 0, "Enroll new or regressed configurations in numerical fixtures: " + string.Join(", ", added.Take(20)));
        Assert.True(completed.Length == 0, "Remove completed configurations from NumericalFixtureBacklog.txt: " + string.Join(", ", completed.Take(20)));
    }

    [Fact]
    public void CustomerIndicatorsAndTheirConsumersAreAlwaysEnrolled()
    {
        var customer = new SharedIndicatorValidationTests.Identity();
        Assert.True(IndicatorValidation.IncludesNumericalFixtures(customer));
        Assert.True(IndicatorValidation.IncludesNumericalFixtures(new Sma(3).Of(customer)));
    }
}
