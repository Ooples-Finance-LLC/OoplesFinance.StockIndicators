using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RegressionChannelComparisonTests
{
    [Fact]
    public void ExactBlockGoldensAndAppendRealignmentAreExplicit()
    {
        var bars = BarsOf([1, 3, 5, 10, 20]);
        var before = bars.ToArray();
        var result = RegressionChannelSnapshot.Calculate(bars, 2, 1);
        Assert.Equal(new RegressionChannelValue(null, null, null, false), result[0]);
        Assert.Equal(new RegressionChannelValue(3, 4, 2, true), result[1]);
        Assert.Equal(new RegressionChannelValue(5, 6, 4, false), result[2]);
        Assert.Equal(new RegressionChannelValue(10, 15, 5, true), result[3]);
        Assert.Equal(new RegressionChannelValue(20, 25, 15, false), result[4]);
        var appended = RegressionChannelSnapshot.Calculate(BarsOf([1, 3, 5, 10, 20, 30]), 2, 1);
        Assert.True(appended[0].BreakPoint);
        Assert.False(appended[1].BreakPoint);
        Assert.Equal(result, RegressionChannelSnapshot.Calculate(bars, 2, 1));
        Assert.Equal(before, bars);
        var all = RegressionChannelSnapshot.Calculate(bars);
        Assert.True(all[0].BreakPoint);
        Assert.All(all.Skip(1), r => Assert.False(r.BreakPoint));
    }

    [Fact]
    public void ParameterBoundariesHugePeriodsAndInvalidInputsAreExplicit()
    {
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d } })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RegressionChannelComparison.Native(prices, null, 2)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RegressionChannelComparison.Reference(prices, null, 2, false)
            );
        }
        Assert.Throws<ArgumentNullException>(() => RegressionChannelSnapshot.Calculate(null!));
        foreach (var n in new[] { 0, 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RegressionChannelSnapshot.Calculate([], n)
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => RegressionChannelSnapshot.Calculate([]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegressionChannelSnapshot.Calculate(BarsOf([1]))
        );
        foreach (var k in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RegressionChannelSnapshot.Calculate([], 2, k)
            );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegressionChannelSnapshot.Calculate(BarsOf([double.NaN]), 2)
        );
        Assert.Empty(RegressionChannelSnapshot.Calculate([], 2));
        Assert.All(
            RegressionChannelSnapshot.Calculate(BarsOf([1, 2, 3]), int.MaxValue),
            r => Assert.Equal(new RegressionChannelValue(null, null, null, false), r)
        );
    }

    [Fact]
    public void WideAndSubnormalFitsAndGenuineOverflowAreVerified()
    {
        foreach (var scale in new[] { double.MaxValue, 1e300, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable.Repeat(scale, 21).ToArray();
            foreach (int? period in new int?[] { 2, 3, null })
                ComparisonVerifier.Compare(
                    RegressionChannelComparison.Series(
                        RegressionChannelComparison.Reference(prices, period, 2, false)
                    ),
                    RegressionChannelComparison.Owned(BarsOf(prices), period, 2),
                    "wide channel",
                    IndicatorErrorBudget.Exact
                );
        }
        var alternating = Enumerable
            .Range(0, 12)
            .Select(i => (i % 2 == 0 ? 1 : -1) * 1e-200)
            .ToArray();
        ComparisonVerifier.Compare(
            RegressionChannelComparison.Series(
                RegressionChannelComparison.Reference(alternating, 3, 2, false)
            ),
            RegressionChannelComparison.Owned(BarsOf(alternating), 3, 2),
            "tiny channel",
            IndicatorErrorBudget.Exact
        );
        Assert.Throws<OverflowException>(() =>
            RegressionChannelSnapshot.Calculate(BarsOf([-double.MaxValue, double.MaxValue]), 2, 2)
        );
        var decimalOverflow = Enumerable.Repeat(1e40, 10).ToArray();
        Assert.Throws<OverflowException>(() =>
            RegressionChannelComparison.Native(decimalOverflow, 3, 2)
        );
        Assert.Throws<OverflowException>(() =>
            RegressionChannelComparison.Reference(decimalOverflow, 3, 2, true)
        );
        Assert.All(
            RegressionChannelSnapshot.Calculate(BarsOf(decimalOverflow), 3, 2).Skip(1),
            r => Assert.Equal(1e40, r.Centerline)
        );
    }

    [Fact]
    public void QuoteTupleReusableRoutesAndAllOutputMutationsAreVerified()
    {
        var data = CompetitorData.Create(90);
        var tuples = data.Closes.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v)).ToArray();
        var quoted = data.Quotes.GetStdDevChannels(3, 2).ToArray();
        var reusable = tuples
            .Select(t => (IReusableResult)new SmaResult(t.Item1) { Sma = t.v })
            .GetStdDevChannels(3, 2)
            .ToArray();
        var direct = tuples.GetStdDevChannels(3, 2).ToArray();
        foreach (
            var (other, prices) in new[]
            {
                (quoted, data.Quotes.Select(q => (double)q.Close).ToArray()),
                (reusable, data.Closes),
                (direct, data.Closes),
            }
        )
        {
            var expected = RegressionChannelComparison.Reference(prices, 3, 2, true);
            Assert.Equal(expected[0], other.Select(v => v.Centerline));
            Assert.Equal(expected[1], other.Select(v => v.UpperChannel));
            Assert.Equal(expected[2], other.Select(v => v.LowerChannel));
            Assert.Equal(expected[3], other.Select(v => (double?)(v.BreakPoint ? 1 : 0)));
        }
        var pair = RegressionChannelComparison.Pair();
        foreach (var name in RegressionChannelComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                if (mask)
                    output.Present![^1] = false;
                else
                    output.Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = RegressionChannelComparison.Pair(true).Library,
                },
                data,
                3
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = RegressionChannelComparison.Pair(false, 1).Library,
                },
                data,
                3
            )
        );
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
