using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RenkoComparisonTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BothModesAndPriceSourcesHaveIndependentReferences(bool atr, bool highLow) =>
        ComparisonVerifier.Check(
            RenkoComparison.Pair(atr, .5, highLow),
            CompetitorData.Create(100),
            5
        );

    [Fact]
    public void MultiBrickCandleSharesTimestampRangeAndVolume()
    {
        Bar[] bars =
        [
            new(DateTime.UnixEpoch, 10, 10, 10, 10, 1),
            new(DateTime.UnixEpoch.AddTicks(1), 10, 12.5, 9, 12.5, 9),
            new(DateTime.UnixEpoch.AddTicks(2), 12.5, 13, 8, 8, 12),
        ];
        var bricks = RenkoSnapshot.Calculate(bars).ToArray();
        Assert.Equal(5, bricks.Length);
        Assert.All(
            bricks.Take(2),
            b =>
            {
                Assert.Equal(bars[1].Time, b.Date);
                Assert.Equal(4.5, b.Volume);
                Assert.Equal(12.5, b.High);
                Assert.Equal(9, b.Low);
                Assert.True(b.IsUp);
            }
        );
        Assert.Equal(10, bricks[0].Open);
        Assert.Equal(11, bricks[0].Close);
        Assert.Equal(12, bricks[1].Close);
        Assert.All(
            bricks.Skip(2),
            b =>
            {
                Assert.False(b.IsUp);
                Assert.Equal(4, b.Volume);
            }
        );
        Assert.Equal(11, bricks[2].Open);
        Assert.Equal(8, bricks[^1].Close);
    }

    [Fact]
    public void HugeCountsAreLazyAndVolumesUseWideSums()
    {
        Bar[] huge =
        [
            new(DateTime.UnixEpoch, 0, 0, 0, 0, 0),
            new(DateTime.UnixEpoch.AddDays(1), 0, double.MaxValue, 0, double.MaxValue, 1),
        ];
        var first = RenkoSnapshot.Calculate(huge, double.Epsilon).First();
        Assert.Equal(double.Epsilon, first.Close);
        Assert.Equal(0, first.Volume);
        Bar[] volume =
        [
            new(DateTime.UnixEpoch, 0, 0, 0, 0, 0),
            new(DateTime.UnixEpoch.AddDays(1), 0, 0, 0, 0, double.MaxValue),
            new(DateTime.UnixEpoch.AddDays(2), 0, 2, 0, 2, double.MaxValue),
        ];
        Assert.All(RenkoSnapshot.Calculate(volume), b => Assert.Equal(double.MaxValue, b.Volume));
    }

    [Fact]
    public void AtrStartupAndZeroRangesProduceNoBricks()
    {
        Assert.Empty(RenkoSnapshot.CalculateAtr(CompetitorData.Create(5).IndicatorBars, 5));
        var bars = Enumerable
            .Range(0, 30)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 1, 1, 1, 1))
            .ToArray();
        Assert.Empty(RenkoSnapshot.CalculateAtr(bars, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenkoSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RenkoSnapshot.CalculateAtr([], 0));
        Assert.Throws<ArgumentNullException>(() => RenkoSnapshot.Calculate(null!));
    }

    [Fact]
    public void EveryBrickFieldAndTimestampWordMutationFails()
    {
        var bars = CompetitorData.Create(80).IndicatorBars;
        var expected = RenkoComparison.Series(RenkoSnapshot.Calculate(bars));
        foreach (var name in expected.Outputs.Keys)
        {
            var actual = RenkoComparison.Series(RenkoSnapshot.Calculate(bars));
            actual.Outputs[name].Values[^1] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(expected, actual, name, IndicatorErrorBudget.Exact)
            );
        }
    }
}
