using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class UlcerComparisonTests
{
    private static Bar[] Raw(double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task IndependentLifecycleContracts(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowUlcerIndex),
                "window ulcer",
                () => new WindowUlcerIndex(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void WindowPeaksRestartAndMissingPrefixesRecover()
    {
        var result = UlcerComparison.Owned(Raw([10, 5, 10, 5]), 3).Outputs["Value"];
        Assert.Equal(new[] { false, false, true, true }, result.Present);
        Assert.Equal(50 / Math.Sqrt(3), result.Values[2], 12);
        Assert.Equal(result.Values[2], result.Values[3]);
        var missing = UlcerComparison.Owned(Raw([0, -1, 2, 1, 0, -3, 4, 2, 1]), 3).Outputs["Value"];
        Assert.Equal(
            new[] { false, false, false, false, true, true, false, false, true },
            missing.Present
        );
        Assert.Equal(0, UlcerComparison.Owned(Raw([3, 3, 3]), 3).Outputs["Value"].Values[2]);
    }

    [Fact]
    public void IndependentWideTinyAndLazyPeriodCases()
    {
        foreach (
            var prices in new[]
            {
                new[] { double.Epsilon, 2 * double.Epsilon, double.Epsilon },
                new[] { double.MaxValue, -double.MaxValue, 0d },
                new[] { 1d, Math.BitDecrement(1), Math.BitIncrement(1) },
                new[] { 1d, -1e153, 1d },
            }
        )
        foreach (var period in new[] { 1, 2, 3, int.MaxValue })
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(UlcerComparison.Reference(prices, period, false)),
                UlcerComparison.Owned(Raw(prices), period),
                "ulcer extreme",
                IndicatorErrorBudget.Exact
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowUlcerIndex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowUlcerIndex(-1));
        Assert.Throws<IndicatorOutputException>(() =>
            UlcerComparison.Owned(Raw([double.Epsilon, -double.MaxValue]), 2)
        );
    }

    [Fact]
    public void NativeIntermediateOverflowAndDecimalCollapseRemainVisible()
    {
        var prices = new[] { 1d, -1e153 };
        var tuples = prices.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v)).ToArray();
        Assert.True(double.IsPositiveInfinity(tuples.GetUlcerIndex(2).Last().UI!.Value));
        Assert.True(
            double.IsFinite(UlcerComparison.Owned(Raw(prices), 2).Outputs["Value"].Values[1])
        );
        var tiny = CompetitorData.FromCloses([double.Epsilon, 2 * double.Epsilon, double.Epsilon]);
        Assert.Null(tiny.Quotes.GetUlcerIndex(3).Last().UI);
        Assert.True(UlcerComparison.Owned(tiny.IndicatorBars, 3).Outputs["Value"].Present![2]);
    }

    [Fact]
    public void NativeGenericTupleReusableSortingAndDefaultRoutes()
    {
        var data = CompetitorData.Create(50);
        var tuples = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var expected = data.Quotes.GetUlcerIndex(3).Select(r => (r.Date, r.UI)).ToArray();
        Assert.Equal(expected, tuples.GetUlcerIndex(3).Select(r => (r.Date, r.UI)));
        Assert.Equal(
            expected,
            data.Quotes.AsEnumerable().Reverse().GetUlcerIndex(3).Select(r => (r.Date, r.UI))
        );
        Assert.Equal(expected, tuples.Reverse().GetUlcerIndex(3).Select(r => (r.Date, r.UI)));
        Assert.Equal(
            data.Quotes.GetUlcerIndex(14).Select(r => r.UI),
            data.Quotes.GetUlcerIndex().Select(r => r.UI)
        );
        var sma = data.Quotes.GetSma(3).ToArray();
        var chained = sma.GetUlcerIndex(3).ToArray();
        Assert.Equal(data.Count, chained.Length);
        Assert.All(chained.Take(4), r => Assert.Null(r.UI));
        Assert.Equal(
            sma.Skip(2).Select(r => (r.Date, r.Sma!.Value)).GetUlcerIndex(3).Select(r => r.UI),
            chained.Skip(2).Select(r => r.UI)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetUlcerIndex(0).ToArray());
        foreach (var period in new[] { 1, 2, 3, 14 })
            ComparisonVerifier.Check(UlcerComparison.Pair, data, period);
    }

    [Fact]
    public async Task CloseChainingMatchesIndependentFormula()
    {
        var data = CompetitorData.Create(30);
        var expectedCloses = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var indicator = new WindowUlcerIndex(3);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = UlcerComparison.Reference(expectedCloses, 3, false);
        Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
        Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0), run[indicator.IsDefined].ToArray());
    }

    [Fact]
    public void ValueAndPresenceMutationsAreDetected()
    {
        var pair = UlcerComparison.Pair;
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var r = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    r.Outputs["Value"].Present![^1] = false;
                else
                    r.Outputs["Value"].Values[^1] += 1;
                return r;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
    }
}
