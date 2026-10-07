using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class JurikComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    public void FreshNativeAndOwnedHistoriesHaveIndependentReferences(int period) =>
        ComparisonVerifier.Check(JurikComparison.Pair(), CompetitorData.Create(160), period);

    [Fact]
    public void PeriodOneAndPhaseClampsHaveGoldens()
    {
        var bars = CompetitorData.Create(60).IndicatorBars;
        Assert.All(JurikAdaptiveSnapshot.Calculate(bars, 1), v => Assert.Equal(bars[0].Close, v));
        Assert.Equal(
            JurikAdaptiveSnapshot.Calculate(bars, 20, 100),
            JurikAdaptiveSnapshot.Calculate(bars, 20, double.MaxValue)
        );
        Assert.Equal(
            JurikAdaptiveSnapshot.Calculate(bars, 20, -100),
            JurikAdaptiveSnapshot.Calculate(bars, 20, -double.MaxValue)
        );
    }

    [Fact]
    public void WideTinyAndLargePeriodInputsRemainFinite()
    {
        foreach (var scale in new[] { double.Epsilon, 1e-200, double.MaxValue / 32 })
        {
            var bars = Enumerable
                .Range(0, 50)
                .Select(i => new Bar(
                    DateTime.UnixEpoch.AddDays(i),
                    0,
                    0,
                    0,
                    (i % 7 - 3) * scale,
                    0
                ))
                .ToArray();
            Assert.Equal(
                JurikComparison.Reference(bars.Select(b => b.Close).ToArray(), 5, 0, 4, false),
                JurikAdaptiveSnapshot.Calculate(bars, 5, 0, 4)
            );
        }
        var shortInput = CompetitorData.Create(5).IndicatorBars;
        Assert.All(
            JurikAdaptiveSnapshot.Calculate(shortInput, int.MaxValue, 0, int.MaxValue),
            v => Assert.True(double.IsFinite(v))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => JurikAdaptiveSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JurikAdaptiveSnapshot.Calculate([], volatilityPeriod: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JurikAdaptiveSnapshot.Calculate([], phase: double.NaN)
        );
    }

    [Fact]
    public void OutputMutationCannotPass()
    {
        var bars = CompetitorData.Create(80).IndicatorBars;
        var expected = JurikAdaptiveSnapshot.Calculate(bars).ToArray();
        var changed = (double[])expected.Clone();
        changed[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                new(0, expected),
                new(0, changed),
                "Jurik mutation",
                IndicatorErrorBudget.Exact
            )
        );
    }
}
