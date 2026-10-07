using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SparseOutputComparisonTests
{
    private static ComparisonSeries Sparse(bool[]? mask = null) => new(new Dictionary<string, ComparisonOutput>
    {
        ["Value"] = new(0, [1d, double.NaN, 3], mask ?? [true, false, true])
    });
    [Fact]
    public void ChecksAbsentPositionsButCountsOnlyPresentNumericValues()
    {
        var pair = new ComparisonPair("sparse", "sparse", (_, _) => Sparse(), (_, _) => Sparse(), (_, _) => Sparse());
        Assert.Equal(2, ComparisonVerifier.Check(pair, CompetitorData.FromCloses([1d, 2, 3]), 1));
        Assert.Equal(2, ComparisonVerifier.Check(pair, CompetitorData.FromCloses([1d, 2, 3]), 1, false));
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("no-mask")]
    [InlineData("wrong-value")]
    [InlineData("hidden-value")]
    [InlineData("infinity")]
    [InlineData("short-mask")]
    [InlineData("negative-start")]
    [InlineData("late-start")]
    public void RejectsIncorrectSparseOutputs(string mutation)
    {
        var output = Sparse().Outputs["Value"];
        switch (mutation)
        {
            case "missing": output.Present![0] = false; output.Values[0] = double.NaN; break;
            case "extra": output.Present![1] = true; output.Values[1] = 2; break;
            case "no-mask": output = output with { Present = null }; break;
            case "wrong-value": output.Values[2] = 4; break;
            case "hidden-value": output.Values[1] = 2; break;
            case "infinity": output.Values[0] = double.PositiveInfinity; break;
            case "short-mask": output = output with { Present = [true] }; break;
            case "negative-start": output = output with { FirstValid = -1 }; break;
            case "late-start": output = output with { FirstValid = 4 }; break;
        }
        var actual = new ComparisonSeries(new Dictionary<string, ComparisonOutput> { ["Value"] = output });
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Compare(Sparse(), actual, mutation));
        // A broken oracle must fail too, rather than blessing a matching malformed result.
        if (mutation is "hidden-value" or "infinity" or "short-mask" or "negative-start" or "late-start")
            Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Compare(actual, actual, "malformed oracle"));
    }
    [Fact]
    public void DenseMaskAndImplicitPresenceAreEquivalent()
    {
        var implicitMask = new ComparisonSeries(0, [1d, 2, 3]);
        var explicitMask = new ComparisonSeries(new Dictionary<string, ComparisonOutput> { ["Value"] = new(0, [1d, 2, 3], [true, true, true]) });
        ComparisonVerifier.Compare(implicitMask, explicitMask, "dense");
        ComparisonVerifier.Compare(explicitMask, implicitMask, "dense");
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Compare(new(0, [double.NaN]), new(0, [double.NaN]), "unmasked NaN"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetectsMasksSharedAcrossCallsInEitherArm(bool competitor)
    {
        bool[] shared = [true, false, true];
        var pair = new ComparisonPair("alias", "alias", (_, _) => competitor ? Sparse(shared) : Sparse(),
            (_, _) => competitor ? Sparse() : Sparse(shared), (_, _) => Sparse());
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(pair, CompetitorData.FromCloses([1d, 2, 3]), 1));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetectsRetainedMaskMutationEvenWhenNewOutputIsCorrect(bool competitor)
    {
        ComparisonSeries? retained = null;
        ComparisonSeries Mutating()
        {
            if (retained is not null) retained.Outputs["Value"].Present![0] = false;
            return retained = Sparse();
        }
        var pair = new ComparisonPair("retained", "retained", (_, _) => competitor ? Mutating() : Sparse(),
            (_, _) => competitor ? Sparse() : Mutating(), (_, _) => Sparse());
        var error = Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(pair, CompetitorData.FromCloses([1d, 2, 3]), 1));
        Assert.Contains("mutated a retained result", error.Message);
    }
}
