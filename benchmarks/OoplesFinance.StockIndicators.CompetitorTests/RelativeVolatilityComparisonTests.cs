using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RelativeVolatilityComparisonTests
{
    [Fact]
    public void SampleDeviationAvailableHistoryAndZeroSeedHaveGoldens()
    {
        var data = CompetitorData.FromCloses([1, 3, 2]);
        Assert.Equal(
            new[] { 0d, 100, 75 },
            RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, 2)
        );
        ComparisonVerifier.Check(RelativeVolatilityComparison.Pair(), data, 2);
        var equalChanges = CompetitorData.FromCloses([1, 2, 1]);
        Assert.Equal(
            new[] { 0d, 0, 50 },
            RelativeVolatilitySnapshot.Calculate(equalChanges.IndicatorBars, 2)
        );
        Assert.All(
            RelativeVolatilitySnapshot.Calculate(
                CompetitorData.FromCloses(new double[20]).IndicatorBars,
                3
            ),
            v => Assert.Equal(0, v)
        );
    }

    [Fact]
    public void InvalidAndHugePeriodsAreExplicit()
    {
        Assert.Throws<ArgumentNullException>(() => RelativeVolatilitySnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RelativeVolatilitySnapshot.Calculate([], 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RelativeVolatilitySnapshot.Calculate([
                new Bar(DateTime.UnixEpoch, 1, 1, 1, double.NaN, 0),
            ])
        );
        var data = CompetitorData.Create(12);
        Assert.Equal(
            RelativeVolatilityComparison.Reference(data.Closes, int.MaxValue),
            RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, int.MaxValue)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Rvi(1));
    }

    [Fact]
    public void WideChangesAndTinyDeviationsMatchPairwiseSquareRootReference()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable.Range(0, 35).Select(i => i % 3 == 0 ? -scale : scale).ToArray();
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 0))
                .ToArray();
            var actual = RelativeVolatilitySnapshot.Calculate(bars, 3);
            Assert.Equal(RelativeVolatilityComparison.Reference(prices, 3), actual);
            Assert.All(actual, v => Assert.InRange(v, 0, 100));
        }
        var tiny = Enumerable.Range(0, 30).Select(i => i % 3 == 0 ? 1e-200 : 2e-200).ToArray();
        var native = new QuanTAlib.Rvi(3);
        Assert.All(
            tiny.Select(v => native.Calc(new QuanTAlib.TValue(v, true, false)).Value),
            v => Assert.Equal(0, v)
        );
        Assert.Contains(RelativeVolatilityComparison.Reference(tiny, 3), v => v > 0);
    }

    [Fact]
    public void OwnedCallsAreFreshAndNativeResetRetainsNestedHistory()
    {
        var data = CompetitorData.Create(80);
        var first = RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, 3);
        Assert.Equal(first, RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, 3));
        Assert.Equal(
            first.Take(40),
            RelativeVolatilitySnapshot.Calculate(data.IndicatorBars.Take(40).ToArray(), 3)
        );
        var native = new QuanTAlib.Rvi(3);
        foreach (var value in new[] { 1d, 3, 2, 5, 1 })
            native.Calc(new QuanTAlib.TValue(value, true, false));
        native.Init();
        var replay = new[] { 10d, 8, 12, 9 }
            .Select(v => native.Calc(new QuanTAlib.TValue(v, true, false)).Value)
            .ToArray();
        var clean = new QuanTAlib.Rvi(3);
        var fresh = new[] { 10d, 8, 12, 9 }
            .Select(v => clean.Calc(new QuanTAlib.TValue(v, true, false)).Value)
            .ToArray();
        Assert.NotEqual(fresh, replay);
    }

    [Fact]
    public void CorruptedValueAndPeriodFailExactVerification()
    {
        var data = CompetitorData.Create(80);
        var expected = new ComparisonSeries(
            0,
            RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, 3).ToArray()
        );
        var changed = RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, 3).ToArray();
        changed[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                new(0, changed),
                "RVI corruption",
                IndicatorErrorBudget.Exact
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                new(0, RelativeVolatilitySnapshot.Calculate(data.IndicatorBars, 4).ToArray()),
                "RVI period",
                IndicatorErrorBudget.Exact
            )
        );
    }
}
