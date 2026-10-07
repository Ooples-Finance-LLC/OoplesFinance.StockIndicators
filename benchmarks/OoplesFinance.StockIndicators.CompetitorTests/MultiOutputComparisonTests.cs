using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MultiOutputComparisonTests
{
    private static ComparisonSeries Expected() => new(new Dictionary<string, ComparisonOutput>
    {
        ["Min"] = new(1, [double.NaN, 2, 3]),
        ["Max"] = new(1, [double.NaN, 4, 5])
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("swapped")]
    [InlineData("nonprimary-error")]
    [InlineData("alignment")]
    [InlineData("nonfinite")]
    public void RejectsWrongOrIncompleteOutputSets(string mutation)
    {
        var actual = Expected().Outputs.ToDictionary(pair => pair.Key, pair => pair.Value);
        switch (mutation)
        {
            case "missing": actual.Remove("Max"); break;
            case "extra": actual.Add("Signal", actual["Max"]); break;
            case "swapped": (actual["Min"], actual["Max"]) = (actual["Max"], actual["Min"]); break;
            case "nonprimary-error": actual["Max"].Values[1] = 123; break;
            case "alignment": actual["Max"] = new(2, actual["Max"].Values); break;
            case "nonfinite": actual["Max"].Values[2] = double.NaN; break;
        }
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Compare(Expected(), new(actual), mutation));
    }

    [Fact]
    public void IncludesBothMinMaxTrajectories()
    {
        var pair = ComparisonPairs.Get("TaLib.Functions.MinMax");
        var data = ComparisonVerifier.Fixture("alternating", 17);
        var expected = pair.Reference!(data, 3);
        Assert.Equal(new[] { "Max", "Min" }, expected.Outputs.Keys.OrderBy(key => key));
        Assert.Equal(30, ComparisonVerifier.Check(pair, data, 3));
    }
}
