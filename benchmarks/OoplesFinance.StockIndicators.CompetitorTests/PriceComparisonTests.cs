using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PriceComparisonTests
{
    public static IEnumerable<object[]> Cases => PriceComparison.Pairs.Select(p => new object[] { p.Id });
    [Theory]
    [MemberData(nameof(Cases))]
    public void HandCheckedPricesAndBothGapDirections(string id)
    {
        var pair = ComparisonPairs.Get(id);
        var data = PriceComparison.Fixture();
        var name = id.Split('.').Last();
        var expected = name switch
        {
            "AvgPrice" => new[] { 5d, 21.25, -6.5, 3.75, 6 },
            "MedPrice" => [5d, 21.5, -7, 2.5, 6],
            "TypPrice" => [16d/3, 65d/3, -6, 4, 6],
            "WclPrice" => [5.5, 21.75, -5.5, 4.75, 6],
            _ => [double.NaN, 19, 34, 15, 1]
        };
        var first = PriceComparison.Transforms.Contains(name) ? 0 : 1;
        var reference = new ComparisonSeries(first, expected);
        ComparisonVerifier.Compare(reference, pair.Ooples(data, 20), id + " Ooples golden");
        ComparisonVerifier.Compare(reference, pair.Competitor(data, 20), id + " competitor golden");
    }
    [Theory]
    [InlineData("AvgPrice")]
    [InlineData("MedPrice")]
    [InlineData("TypPrice")]
    [InlineData("WclPrice")]
    public void PinnedTaLibReleaseRejectsOneBarEvenForZeroLookbackTransforms(string name)
    {
        var pair = ComparisonPairs.Get("TaLib.Functions." + name);
        Assert.Equal(2, pair.MinimumInputCount);
        var data = CompetitorData.FromOhlc([4d], [10d], [0d], [6d]);
        var error = Assert.Throws<InvalidOperationException>(() => pair.Competitor(data, 20));
        Assert.Contains("OutOfRangeParam", error.Message);
        var expected = name switch { "AvgPrice" or "MedPrice" => 5d, "TypPrice" => 16d/3, _ => 5.5 };
        ComparisonVerifier.Compare(new ComparisonSeries(0, [expected]), pair.Ooples(data, 20), "one bar Ooples");
    }
}
