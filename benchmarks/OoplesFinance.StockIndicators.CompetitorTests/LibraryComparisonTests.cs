using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class LibraryComparisonTests
{
    public static IEnumerable<object[]> Pairs => ComparisonPairs.All.Select(pair => new object[] { pair.Id });
    [Theory]
    [MemberData(nameof(Pairs))]
    public void FullTrajectoryMatchesIndependentFormula(string id) => Assert.True(ComparisonVerifier.Verify(ComparisonPairs.Get(id)) > 0);

    [Fact]
    public void EveryPairHasAnActualPackageApi()
    {
        var catalog = CompetitorApiCatalog.Discover();
        Assert.Equal(catalog.Length, catalog.Select(api => api.Id).Distinct().Count());
        Assert.Equal(ComparisonPairs.All.Length, ComparisonPairs.All.Select(pair => pair.Id).Distinct().Count());
        foreach (var pair in ComparisonPairs.All) Assert.Contains(catalog, api => api.Id == pair.Id);
        foreach (var id in CompetitorApiCatalog.Exclusions.Keys) Assert.Contains(catalog, api => api.Id == id);
    }

    [Fact]
    public void EveryApiHasAnExplicitAndSupportedCoverageStatus()
    {
        var manifest = ComparisonManifest.Create();
        Assert.Equal(CompetitorApiCatalog.Discover().Select(api => api.Id), manifest.Select(row => row.Id));
        Assert.DoesNotContain(manifest, row => row.Status == "pending");
        foreach (var row in manifest)
        {
            Assert.Contains(row.Status, new[] { "paired", "pending", "utility", "unavailable" });
            if (row.Status == "paired") Assert.Contains(ComparisonPairs.All, pair => pair.Id == row.Id);
            if (row.Status == "pending") Assert.Null(row.OoplesIndicator);
            if (row.Status == "utility") Assert.False(string.IsNullOrWhiteSpace(row.ExclusionReason));
            if (row.Status == "unavailable")
            {
                Assert.Contains(row.Id, CompetitorApiCatalog.Unimplemented);
                Assert.False(string.IsNullOrWhiteSpace(row.CompetitorLimitation));
                if (row.OwnedDefinition is not null)
                {
                    Assert.Null(row.AlternateComparison);
                    Assert.Equal(ComparisonManifest.OwnedDefinitions[row.Id], row.OwnedDefinition);
                    Assert.Equal(row.OwnedDefinition, row.OoplesIndicator);
                }
                else
                {
                    Assert.NotNull(row.AlternateComparison);
                    Assert.Equal(ComparisonPairs.Get(row.AlternateComparison).Indicator, row.OoplesIndicator);
                }
            }
        }
    }
}
