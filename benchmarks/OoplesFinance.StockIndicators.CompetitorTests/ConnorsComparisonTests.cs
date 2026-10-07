using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ConnorsComparisonTests
{
    [Fact]
    public void GoldensRetainInclusiveRanksAndDelayedStreakPresence()
    {
        var data = CompetitorData.FromCloses([1, 2, 3, 4, 5, 6, 7]);
        var rows = ConnorsStrengthSnapshot.Calculate(data.IndicatorBars, 2, 2, 2);
        Assert.Null(rows[1].Rsi);
        Assert.Equal(100, rows[2].Rsi);
        Assert.Equal(50, rows[2].PercentRank);
        Assert.Equal(0, rows[3].PercentRank);
        Assert.Null(rows[3].RsiStreak);
        Assert.Null(rows[3].ConnorsRsi);
        Assert.Equal(100, rows[4].RsiStreak);
        Assert.Equal(200d / 3, rows[4].ConnorsRsi);
        ComparisonVerifier.Check(ConnorsComparison.Pair(2, 2, 2), data, 20);
        var fractional = CompetitorData.FromCloses([1, 2, 1, 3, 4, 2, 7, 8, 4]);
        ComparisonVerifier.Check(ConnorsComparison.Pair(2, 3, 3), fractional, 20);
        Assert.All(
            ConnorsStrengthSnapshot.Calculate(fractional.IndicatorBars, 2, 3, 3).Skip(3),
            r => Assert.Equal(Math.Truncate(r.PercentRank!.Value), r.PercentRank)
        );
    }

    [Fact]
    public void InvalidRequestsAndHugePeriodsDoNotAllocatePeriodSizedStorage()
    {
        Assert.Throws<ArgumentNullException>(() => ConnorsStrengthSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnorsStrengthSnapshot.Calculate([], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConnorsStrengthSnapshot.Calculate([], streakPeriod: 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConnorsStrengthSnapshot.Calculate([], rankPeriod: 1)
        );
        var data = CompetitorData.Create(10);
        Assert.All(
            ConnorsStrengthSnapshot.Calculate(
                data.IndicatorBars,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue
            ),
            r => Assert.Equal(new ConnorsStrengthValue(null, null, null, null), r)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConnorsStrengthSnapshot.Calculate([
                new Bar(DateTime.UnixEpoch, 1, 1, 1, double.PositiveInfinity, 0),
            ])
        );
        Assert.Throws<OverflowException>(() =>
            data.Quotes.GetConnorsRsi(2, 2, int.MaxValue).ToArray()
        );
    }

    [Fact]
    public void ExactReturnsPreserveWideTinyAndNonpositivePriceCases()
    {
        foreach (
            var prices in new[]
            {
                new[]
                {
                    double.Epsilon,
                    double.MaxValue,
                    double.Epsilon * 2,
                    double.MaxValue / 2,
                    double.Epsilon,
                    double.MaxValue / 4,
                    0,
                    -double.MaxValue,
                    double.Epsilon,
                    1,
                },
                new[] { 0d, -1, -2, 0, 1, 2, 2, 1, 3, 7 },
                new[]
                {
                    1d,
                    Math.BitIncrement(1),
                    Math.BitIncrement(Math.BitIncrement(1)),
                    1,
                    Math.BitDecrement(1),
                    1,
                    2,
                    1,
                    2,
                    3,
                },
            }
        )
        {
            var bars = prices
                .Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 0))
                .ToArray();
            ComparisonVerifier.Compare(
                ConnorsComparison.Series(ConnorsComparison.Reference(prices, 2, 2, 3, false)),
                ConnorsComparison.Owned(bars, 2, 2, 3),
                "exact Connors",
                IndicatorErrorBudget.Exact
            );
        }
        var ranking = new[] { double.Epsilon, 1d, double.Epsilon, 2d };
        var rankBars = ranking
            .Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 0))
            .ToArray();
        Assert.Equal(100, ConnorsStrengthSnapshot.Calculate(rankBars, 2, 2, 2)[3].PercentRank);
    }

    [Fact]
    public void QuoteTupleRoutesAndIndependentCallsPreserveAllComponents()
    {
        var data = CompetitorData.Create(150);
        var quote = data.Quotes.GetConnorsRsi(3, 2, 20).ToArray();
        var tuple = data
            .Quotes.Select(q => (q.Date, (double)q.Close))
            .GetConnorsRsi(3, 2, 20)
            .ToArray();
        Assert.Equal(
            quote.Select(r => (r.Rsi, r.RsiStreak, r.PercentRank, r.ConnorsRsi)),
            tuple.Select(r => (r.Rsi, r.RsiStreak, r.PercentRank, r.ConnorsRsi))
        );
        var first = ConnorsStrengthSnapshot.Calculate(data.IndicatorBars, 3, 2, 20);
        Assert.Equal(first, ConnorsStrengthSnapshot.Calculate(data.IndicatorBars, 3, 2, 20));
        Assert.Equal(
            first.Take(50),
            ConnorsStrengthSnapshot.Calculate(data.IndicatorBars.Take(50).ToArray(), 3, 2, 20)
        );
        ComparisonVerifier.Check(ConnorsComparison.Pair(3, 2, 20), data, 20);
    }

    [Fact]
    public void EveryOutputAndMaskMutationFails()
    {
        var data = CompetitorData.Create(150);
        var expected = ConnorsComparison.Owned(data.IndicatorBars, 3, 2, 20);
        foreach (var name in ConnorsComparison.Names)
        foreach (var missing in new[] { false, true })
        {
            var changed = ConnorsComparison.Owned(data.IndicatorBars, 3, 2, 20);
            if (missing)
                changed.Outputs[name].Present![^1] = false;
            else
                changed.Outputs[name].Values[^1] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(expected, changed, name, IndicatorErrorBudget.Exact)
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                ConnorsComparison.Owned(data.IndicatorBars, 3, 2, 21),
                "rank period",
                IndicatorErrorBudget.Exact
            )
        );
    }
}
