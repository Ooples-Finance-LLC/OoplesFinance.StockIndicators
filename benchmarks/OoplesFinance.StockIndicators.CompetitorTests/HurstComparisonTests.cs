using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class HurstComparisonTests
{
    [Theory]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(100)]
    [InlineData(340)]
    [InlineData(341)]
    public void BothExponentsMatchIndependentReferences(int period) =>
        ComparisonVerifier.Check(
            HurstComparison.Pair(),
            CompetitorData.Create(period + 40),
            period
        );

    [Fact]
    public void FlatAndNonpositiveInputsHaveExplicitMasks()
    {
        var flat = HurstSnapshot.Calculate(
            ComparisonVerifier.Fixture("constant", 50).IndicatorBars,
            20
        );
        Assert.All(
            flat.Take(20),
            x =>
            {
                Assert.Null(x.HurstExponent);
                Assert.Null(x.HurstExponentAL);
            }
        );
        Assert.All(
            flat.Skip(20),
            x =>
            {
                Assert.Null(x.HurstExponent);
                Assert.True(double.IsFinite(x.HurstExponentAL!.Value));
            }
        );
        foreach (var shape in new[] { "zero", "negative" })
            Assert.All(
                HurstSnapshot.Calculate(ComparisonVerifier.Fixture(shape, 50).IndicatorBars, 20),
                x =>
                {
                    Assert.Null(x.HurstExponent);
                    Assert.Null(x.HurstExponentAL);
                }
            );
    }

    [Fact]
    public void ExtremePositiveRatiosAndMaximumPeriodDoNotOverflow()
    {
        var bars = Enumerable
            .Range(0, 45)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                0,
                0,
                0,
                i % 2 == 0 ? double.Epsilon : double.MaxValue,
                0
            ))
            .ToArray();
        var values = HurstSnapshot.Calculate(bars, 20);
        Assert.All(
            values.Skip(20),
            x =>
            {
                Assert.True(double.IsFinite(x.HurstExponent!.Value));
                Assert.True(double.IsFinite(x.HurstExponentAL!.Value));
            }
        );
        ComparisonVerifier.Compare(
            HurstComparison.Series(
                HurstComparison.Reference(bars.Select(b => b.Close).ToArray(), 20)
            ),
            HurstComparison.Series(values),
            "extreme Hurst",
            HurstComparison.Budget
        );
        Assert.All(HurstSnapshot.Calculate(bars, int.MaxValue), x => Assert.Null(x.HurstExponent));
        Assert.Throws<ArgumentOutOfRangeException>(() => HurstSnapshot.Calculate([], 19));
        Assert.Throws<ArgumentNullException>(() => HurstSnapshot.Calculate(null!));
    }

    [Theory]
    [InlineData("HurstExponent", false)]
    [InlineData("HurstExponent", true)]
    [InlineData("HurstExponentAL", false)]
    [InlineData("HurstExponentAL", true)]
    public void EveryOutputAndMaskMutationIsDetected(string name, bool mask)
    {
        var bars = CompetitorData.Create(80).IndicatorBars;
        var expected = HurstComparison.Series(HurstSnapshot.Calculate(bars, 20));
        var actual = HurstComparison.Series(HurstSnapshot.Calculate(bars, 20));
        Assert.True(actual.Outputs[name].Present![^1]);
        if (mask)
            actual.Outputs[name].Present![^1] = false;
        else
            actual.Outputs[name].Values[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(expected, actual, "Hurst mutation", HurstComparison.Budget)
        );
    }
}
