using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class KlingerComparisonTests
{
    [Fact]
    public void ZeroForceAndIndependentStartupMasksHaveGoldens()
    {
        var bars = Enumerable
            .Range(0, 12)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 1, 1, 1, 0))
            .ToArray();
        var result = KlingerVolumeSnapshot.Calculate(bars, 3, 4, 2);
        Assert.All(result.Take(5), r => Assert.Equal(new KlingerVolumeValue(null, null), r));
        Assert.Equal(new KlingerVolumeValue(0, null), result[5]);
        Assert.All(result.Skip(6), r => Assert.Equal(new KlingerVolumeValue(0, 0), r));
        ComparisonVerifier.Check(KlingerComparison.Pair(3, 4, 2), CompetitorData.Create(50), 20);
    }

    [Fact]
    public void InvalidAndMaximumPeriodsHaveExplicitBehavior()
    {
        Assert.Throws<ArgumentNullException>(() => KlingerVolumeSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => KlingerVolumeSnapshot.Calculate([], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => KlingerVolumeSnapshot.Calculate([], 4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KlingerVolumeSnapshot.Calculate([], 3, 4, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KlingerVolumeSnapshot.Calculate([new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, double.NaN)])
        );
        var data = CompetitorData.Create(10);
        Assert.All(
            KlingerVolumeSnapshot.Calculate(
                data.IndicatorBars,
                int.MaxValue - 1,
                int.MaxValue,
                int.MaxValue
            ),
            r => Assert.Equal(new KlingerVolumeValue(null, null), r)
        );
        Assert.Throws<OverflowException>(() => data.Quotes.GetKvo(3, int.MaxValue, 2).ToArray());
    }

    [Fact]
    public void WideRangesTinyPricesAndFinalOverflowAreVerified()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var bars = Enumerable
                .Range(0, 30)
                .Select(i => new Bar(
                    DateTime.UnixEpoch.AddDays(i),
                    0,
                    scale,
                    -scale,
                    i % 3 == 0 ? -scale : scale,
                    i % 5
                ))
                .ToArray();
            ComparisonVerifier.Compare(
                KlingerComparison.Series(KlingerComparison.Reference(bars, 3, 5, 2)),
                KlingerComparison.Owned(bars, 3, 5, 2),
                "wide Klinger",
                IndicatorErrorBudget.Exact
            );
        }
        var largeVolume = Enumerable
            .Range(0, 12)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                i,
                i + 2,
                i - 2,
                i,
                double.MaxValue
            ))
            .ToArray();
        Assert.Throws<OverflowException>(() =>
            KlingerVolumeSnapshot.Calculate(largeVolume, 3, 4, 2)
        );
    }

    [Fact]
    public void RangeCancellationBranchesAndFreshPrefixesMatchReference()
    {
        var bars = Enumerable
            .Range(0, 30)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                0,
                i % 4 == 0 ? -2 : 2,
                i % 3 == 0 ? 2 : -2,
                i % 5,
                i % 6
            ))
            .ToArray();
        var expected = KlingerComparison.Series(KlingerComparison.Reference(bars, 3, 5, 2));
        ComparisonVerifier.Compare(
            expected,
            KlingerComparison.Owned(bars, 3, 5, 2),
            "range branches",
            IndicatorErrorBudget.Exact
        );
        var first = KlingerVolumeSnapshot.Calculate(bars, 3, 5, 2);
        Assert.Equal(first, KlingerVolumeSnapshot.Calculate(bars, 3, 5, 2));
        Assert.Equal(
            first.Take(20),
            KlingerVolumeSnapshot.Calculate(bars.Take(20).ToArray(), 3, 5, 2)
        );
    }

    [Fact]
    public void EveryOutputAndMaskMutationFails()
    {
        var data = CompetitorData.Create(100);
        var expected = KlingerComparison.Owned(data.IndicatorBars, 3, 5, 2);
        foreach (var name in new[] { "Oscillator", "Signal" })
        foreach (var missing in new[] { false, true })
        {
            var actual = KlingerComparison.Owned(data.IndicatorBars, 3, 5, 2);
            if (missing)
                actual.Outputs[name].Present![^1] = false;
            else
                actual.Outputs[name].Values[^1] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(
                    expected,
                    actual,
                    "Klinger mutation",
                    IndicatorErrorBudget.Exact
                )
            );
        }
    }
}
