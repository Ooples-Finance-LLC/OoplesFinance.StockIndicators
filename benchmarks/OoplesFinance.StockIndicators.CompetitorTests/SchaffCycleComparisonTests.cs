using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SchaffCycleComparisonTests
{
    [Fact]
    public void StartupFlatWindowsAndThreeValueSmoothingAreExplicit()
    {
        var data = CompetitorData.FromCloses([1, 2, 4, 8, 16, 32, 64]);
        var result = SchaffTrendCycleSnapshot.Calculate(data.IndicatorBars, 2, 1, 2);
        Assert.Equal(new double?[] { null, null, null, null, 100, 100, 100 }, result);
        var flat = CompetitorData.FromCloses(Enumerable.Repeat(4d, 10).ToArray());
        Assert.All(
            SchaffTrendCycleSnapshot.Calculate(flat.IndicatorBars, 1, 1, 2).Skip(3),
            v => Assert.Equal(0, v)
        );
        ComparisonVerifier.Check(SchaffCycleComparison.Pair(2, 1, 2), data, 20);
    }

    [Fact]
    public void ParametersAndHugePeriodsAreBoundedByObservedData()
    {
        Assert.Throws<ArgumentNullException>(() => SchaffTrendCycleSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SchaffTrendCycleSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchaffTrendCycleSnapshot.Calculate([], fastPeriod: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchaffTrendCycleSnapshot.Calculate([], fastPeriod: 5, slowPeriod: 5)
        );
        var data = CompetitorData.Create(10);
        Assert.All(
            SchaffTrendCycleSnapshot.Calculate(
                data.IndicatorBars,
                int.MaxValue,
                int.MaxValue - 1,
                int.MaxValue
            ),
            v => Assert.Null(v)
        );
        Assert.All(
            SchaffTrendCycleSnapshot.Calculate(data.IndicatorBars, int.MaxValue, 1, 2),
            v => Assert.Null(v)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchaffTrendCycleSnapshot.Calculate([
                new Bar(DateTime.UnixEpoch, 1, 1, 1, double.NaN, 0),
            ])
        );
        // Skender accepts zero in its outer validation but the nested stochastic rejects it.
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetStc(0).ToArray());
    }

    [Fact]
    public void WideAndSubnormalInputsHaveIndependentBoundedOutputs()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable
                .Range(0, 80)
                .Select(i => (i % 4 == 0 ? -1d : 1d) * scale)
                .ToArray();
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 0))
                .ToArray();
            var actual = SchaffTrendCycleSnapshot.Calculate(bars, 3, 2, 5).ToArray();
            Assert.Equal(SchaffCycleComparison.Reference(prices, 3, 2, 5), actual);
            Assert.All(actual.Where(v => v.HasValue), v => Assert.InRange(v!.Value, 0, 100));
            Assert.Equal(actual, SchaffTrendCycleSnapshot.Calculate(bars, 3, 2, 5));
        }
    }

    [Fact]
    public void QuoteAndTupleRoutesAndCausalPrefixesAgree()
    {
        var data = CompetitorData.Create(90);
        var expected = data.Quotes.GetStc(3, 2, 5).Select(r => r.Stc).ToArray();
        Assert.Equal(
            expected,
            data.Quotes.Select(q => (q.Date, (double)q.Close)).GetStc(3, 2, 5).Select(r => r.Stc)
        );
        var owned = SchaffTrendCycleSnapshot.Calculate(data.IndicatorBars, 3, 2, 5).ToArray();
        Assert.Equal(
            owned.Take(40),
            SchaffTrendCycleSnapshot.Calculate(data.IndicatorBars.Take(40).ToArray(), 3, 2, 5)
        );
        ComparisonVerifier.Check(SchaffCycleComparison.Pair(3, 2, 5), data, 20);
    }

    [Fact]
    public void ValueAndPresenceCorruptionFailExactComparison()
    {
        var data = CompetitorData.Create(90);
        var values = SchaffTrendCycleSnapshot.Calculate(data.IndicatorBars, 3, 2, 5).ToArray();
        var expected = SchaffCycleComparison.Series(values);
        foreach (var missing in new[] { false, true })
        {
            var changed = (double?[])values.Clone();
            changed[^1] = missing ? null : values[^1] + 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(
                    expected,
                    SchaffCycleComparison.Series(changed),
                    "mutated STC",
                    IndicatorErrorBudget.Exact
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                SchaffCycleComparison.Series(
                    SchaffTrendCycleSnapshot.Calculate(data.IndicatorBars, 4, 2, 5).ToArray()
                ),
                "mutated cycle",
                IndicatorErrorBudget.Exact
            )
        );
    }
}
