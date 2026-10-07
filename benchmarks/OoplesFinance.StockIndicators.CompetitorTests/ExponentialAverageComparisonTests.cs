using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ExponentialAverageComparisonTests
{
    public static IEnumerable<object[]> Pairs =>
        ExponentialAverageComparison.Pairs.Select(pair => new object[] { pair.Id });

    [Theory]
    [MemberData(nameof(Pairs))]
    public void SeedAndEverySubsequentStepMatchHandCalculatedValues(string id)
    {
        var data = CompetitorData.FromCloses([3d, 6, 9, 12, 3, 6]);
        var expected = id.StartsWith("Trady.", StringComparison.Ordinal)
            ? new ComparisonSeries(0, [3d, 4.5, 6.75, 9.375, 6.1875, 6.09375])
            : new ComparisonSeries(2, [double.NaN, double.NaN, 6, 9, 6, 6]);
        var pair = ComparisonPairs.Get(id);
        ComparisonVerifier.Compare(expected, pair.Reference!(data, 3), "oracle");
        ComparisonVerifier.Compare(expected, pair.Ooples(data, 3), "Ooples");
        ComparisonVerifier.Compare(expected, pair.Competitor(data, 3), "competitor");
    }

    [Fact]
    public void QuanTAlibCompensatedModeIsADifferentFormula()
    {
        var compensated = new QuanTAlib.Ema(3, false);
        var values = new[] { 3d, 6, 9 }
            .Select(value => compensated.Calc(new QuanTAlib.TValue(value, true, false)).Value)
            .ToArray();
        Assert.NotEqual(6d, values[^1]);
        var row = Assert.Single(ComparisonManifest.Create(), item => item.Id == "QuanTAlib.Ema");
        Assert.Null(row.PendingConfigurations);
        var data = CompetitorData.FromCloses([3d, 6, 9]);
        ComparisonVerifier.Check(MassNormalizedComparison.Create(), data, 3);
        ComparisonVerifier.Check(MassNormalizedComparison.Create([.5]), data, 3);
    }
}
