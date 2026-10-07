using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Trady.Analysis.Candlestick;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CompetitorAvailabilityTests
{
    [Fact]
    public void UnavailableCounterpartsLinkToAnExplicitVerifiedComparison()
    {
        var manifest = ComparisonManifest.Create();
        foreach (var (unavailable, alternate) in ComparisonManifest.AlternateComparisons)
        {
            Assert.Contains(unavailable, CompetitorApiCatalog.Unimplemented);
            var row = Assert.Single(manifest, entry => entry.Id == unavailable);
            var pair = ComparisonPairs.Get(alternate);
            Assert.Equal("unavailable", row.Status);
            Assert.Equal(pair.Indicator, row.OoplesIndicator);
            Assert.Equal(alternate, row.AlternateComparison);
            Assert.DoesNotContain(ComparisonPairs.All, entry => entry.Id == unavailable);
        }
    }

    public static IEnumerable<object[]> UnimplementedApis => CompetitorApiCatalog.Unimplemented
        .Where(id => id.StartsWith("Trady.Candlestick.", StringComparison.Ordinal))
        .OrderBy(id => id, StringComparer.Ordinal).Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(UnimplementedApis))]
    public void TradyReleaseContainsUnimplementedCandleApis(string id)
    {
        var data = CompetitorData.Create(64);
        var typeName = id.Replace("Trady.Candlestick.", "Trady.Analysis.Candlestick.", StringComparison.Ordinal);
        var type = typeof(Doji).Assembly.GetType(typeName, throwOnError: true)!;
        var constructor = type.GetConstructors().Single(ctor => ctor.GetParameters().Length > 0 &&
            ctor.GetParameters()[0].ParameterType.IsInstanceOfType(data.Candles) && ctor.GetParameters().Skip(1).All(parameter => parameter.IsOptional));
        var arguments = constructor.GetParameters().Select((parameter, index) => index == 0 ? (object)data.Candles : parameter.DefaultValue).ToArray();
        var indicator = constructor.Invoke(arguments);
        var compute = type.GetMethod("Compute", new[] { typeof(int?), typeof(int?) })!;
        // Verify the pinned binary directly; successful execution is a package-contract
        // change that must remove this limitation and add a real comparison.
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => compute.Invoke(indicator, new object?[] { null, null }));
        Assert.IsType<NotImplementedException>(exception.InnerException);
    }
}
