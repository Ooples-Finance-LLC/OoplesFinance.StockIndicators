using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PenetrationBoundaryComparisonTests
{
    [Theory]
    [InlineData("MorningStar")]
    [InlineData("EveningStar")]
    [InlineData("MorningDojiStar")]
    [InlineData("EveningDojiStar")]
    [InlineData("AbandonedBaby")]
    public void ExactAndRoundedPenetrationContractsHavePinnedDistinctSignals(string name)
    {
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 4, 10, 0, 6, 1))
            .ToList();
        bars.Add(new(DateTime.UnixEpoch.AddDays(10), 7, 12, 2, 2, 1));
        bars.Add(new(DateTime.UnixEpoch.AddDays(11), 0, 1, -1, 1, 1));
        bars.Add(new(DateTime.UnixEpoch.AddDays(12), 1.25, 6, 1.125, 3.5, 1));
        var up = !name.StartsWith("Evening", StringComparison.Ordinal);
        var data = CrowSoldierComparison.FromBars(
            (
                up ? bars : bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1))
            ).ToArray()
        );
        var pair = ComparisonPairs.Get("TaLib.Candles." + name);
        Assert.Equal(up ? 100 : -100, pair.Ooples(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(up ? 100 : -100, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(0, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(0, pair.CompetitorReference!(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(1, ComparisonVerifier.Check(pair, data, 20));
        // A documented arithmetic contract must not become a blanket mismatch exemption.
        var corrupt = pair with
        {
            Competitor = (_, _) =>
                new ComparisonSeries(12, Enumerable.Repeat(99d, data.Count).ToArray()),
        };
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(corrupt, data, 20));
        corrupt = pair with
        {
            Library = (_, _) => new ComparisonSeries(12, new double[data.Count]),
        };
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(corrupt, data, 20));
    }

    [Fact]
    public void SeparateCompetitorReferenceMustCoverEveryDeclaredOutput()
    {
        var pair = ComparisonPairs.Get("TaLib.Candles.MorningStar") with
        {
            CompetitorReference = (_, _) =>
                new ComparisonSeries(new Dictionary<string, ComparisonOutput>()),
        };
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(pair, StarReversalComparison.Fixture(), 20)
        );
    }
}
