using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class BenchmarkSetupVerificationTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void SetupChecksBothDelegatesOnceAndCorrectnessKeepsIsolationReplays(bool isolation, int calls)
    {
        var ours = 0; var theirs = 0;
        var pair = new ComparisonPair("test", "test", (_, _) => { theirs++; return Series(); },
            (_, _) => { ours++; return Series(); }, (_, _) => Series());
        Assert.Equal(3, ComparisonVerifier.Check(pair, CompetitorData.FromCloses([1d, 2, 3]), 1, isolation));
        Assert.Equal(calls, ours); Assert.Equal(calls, theirs);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetupStillRejectsIncorrectFinalValuesInEitherArm(bool wrongCompetitor)
    {
        ComparisonSeries Result(bool wrong) => new(0, [1d, 2, wrong ? 4 : 3]);
        var pair = new ComparisonPair("test", "test", (_, _) => Result(wrongCompetitor),
            (_, _) => Result(!wrongCompetitor), (_, _) => Series());
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(pair, CompetitorData.FromCloses([1d, 2, 3]), 1, false));
    }
    [Fact]
    public void ActualBenchmarkSetupChecksTheSameCompleteOutputsAsTimedMethods()
    {
        var benchmark = new LibraryPairBenchmarks { PairId = "TaLib.Functions.MinMax", Bars = 1000 };
        benchmark.Setup();
        var ours = Assert.IsType<ComparisonSeries>(benchmark.Ooples());
        var theirs = Assert.IsType<ComparisonSeries>(benchmark.Competitor());
        ComparisonVerifier.Compare(ours, theirs, "timed arms");
        Assert.Equal(2, ours.Outputs.Count);
        Assert.All(ours.Outputs.Values, output => Assert.Equal(1000, output.Values.Length));
    }
    private static ComparisonSeries Series() => new(0, [1d, 2, 3]);
}
