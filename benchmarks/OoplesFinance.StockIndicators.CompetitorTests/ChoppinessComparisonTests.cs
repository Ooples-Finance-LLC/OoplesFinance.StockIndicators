using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ChoppinessComparisonTests
{
    private static Bar[] Raw(double[] high, double[] low, double[] close) =>
        high.Select(
                (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), close[i], v, low[i], close[i], 0)
            )
            .ToArray();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task IndependentLifecycleContracts(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowChoppinessIndex),
                "window choppiness",
                () => new WindowChoppinessIndex(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownBoundsTrueIntervalsAndFlatRecovery()
    {
        var constant = ChoppinessComparison.Owned(Raw([2, 2, 2], [0, 0, 0], [1, 1, 1]), 2).Outputs[
            "Value"
        ];
        Assert.Equal(new[] { false, false, true }, constant.Present);
        Assert.Equal(100, constant.Values[2]);
        var trend = ChoppinessComparison.Owned(Raw([0, 1, 2], [0, 1, 2], [0, 1, 2]), 2).Outputs[
            "Value"
        ];
        Assert.True(trend.Present![2]);
        Assert.Equal(0, trend.Values[2]);
        var gaps = ChoppinessComparison.Owned(Raw([0, 2, 0], [0, 2, 0], [0, 2, 0]), 2).Outputs[
            "Value"
        ];
        Assert.Equal(100, gaps.Values[2]);
        var recovery = ChoppinessComparison
            .Owned(Raw([1, 1, 1, 2, 2, 2], [1, 1, 1, 2, 2, 2], [1, 1, 1, 2, 2, 2]), 2)
            .Outputs["Value"];
        Assert.Equal(new[] { false, false, false, true, true, false }, recovery.Present);
    }

    [Fact]
    public void IndependentWideTinyAndLazyPeriodReferences()
    {
        var fixtures = new[]
        {
            Raw(
                [double.MaxValue, double.MaxValue, double.MaxValue],
                [-double.MaxValue, -double.MaxValue, -double.MaxValue],
                [0, 0, 0]
            ),
            Raw(
                [double.Epsilon, 2 * double.Epsilon, double.Epsilon],
                [0, 0, 0],
                [0, double.Epsilon, 0]
            ),
            Raw([0, 1e-300, 1], [0, 0, 0], [0, 1e-300, 1]),
            Raw([1, Math.BitIncrement(1), 1], [1, 1, Math.BitDecrement(1)], [1, 1, 1]),
        };
        foreach (var bars in fixtures)
        foreach (var period in new[] { 2, 3, int.MaxValue })
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(ChoppinessComparison.Reference(bars, period)),
                ChoppinessComparison.Owned(bars, period),
                "wide/tiny choppiness",
                ChoppinessComparison.Budget
            );
        Assert.Equal(100, ChoppinessComparison.Owned(fixtures[0], 2).Outputs["Value"].Values[2]);
        Assert.True(ChoppinessComparison.Owned(fixtures[2], 2).Outputs["Value"].Values[2] > 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowChoppinessIndex(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowChoppinessIndex(0));
        // Disjoint zero-width true intervals are possible only with inconsistent candle ranges.
        Assert.Throws<IndicatorOutputException>(() =>
            ChoppinessComparison.Owned(Raw([0, 0, 1], [0, 0, 1], [0, 1, 2]), 2)
        );
    }

    [Fact]
    public void NativeDecimalCollapseAndRoundedUnitRatiosRemainVisible()
    {
        var tiny = CompetitorData.FromOhlc(
            [0, 0, 0],
            [double.Epsilon, 2 * double.Epsilon, double.Epsilon],
            [0, 0, 0],
            [0, double.Epsilon, 0]
        );
        Assert.Null(tiny.Quotes.GetChop(2).Last().Chop);
        Assert.True(ChoppinessComparison.Owned(tiny.IndicatorBars, 2).Outputs["Value"].Present![2]);
        var near = CompetitorData.FromOhlc([0, 1e-20, 1], [0, 1e-20, 1], [0, 0, 0], [0, 1e-20, 1]);
        Assert.Equal(0, near.Quotes.GetChop(2).Last().Chop);
        var actual = ChoppinessComparison.Owned(near.IndicatorBars, 2).Outputs["Value"].Values[2];
        Assert.True(actual > 0);
        Assert.True(Math.Abs(actual / (100 * (1e-20 / Math.Log(2))) - 1) < 4e-15);
    }

    [Fact]
    public void NativeGenericSortingDefaultsAndBoundaryRoutes()
    {
        var data = CompetitorData.Create(50);
        Assert.Equal(
            data.Quotes.GetChop(3).Select(r => (r.Date, r.Chop)),
            data.Quotes.AsEnumerable().Reverse().GetChop(3).Select(r => (r.Date, r.Chop))
        );
        Assert.Equal(
            data.Quotes.GetChop(14).Select(r => r.Chop),
            data.Quotes.GetChop().Select(r => r.Chop)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetChop(1).ToArray());
        Assert.Empty(Array.Empty<Quote>().GetChop());
        Assert.All(data.Quotes.Take(2).GetChop(2), r => Assert.Null(r.Chop));
        Assert.All(data.Quotes.GetChop(int.MaxValue), r => Assert.Null(r.Chop));
        foreach (var period in new[] { 2, 3, 14 })
            ComparisonVerifier.Check(ChoppinessComparison.Pair, data, period);
    }

    [Fact]
    public async Task CloseChainingRetainsOriginalHighLow()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var mapped = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        var indicator = new WindowChoppinessIndex(3);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var values = run[indicator.Value].ToArray();
        var present = run[indicator.IsDefined].ToArray();
        ComparisonVerifier.Compare(
            VolumePriceComparison.Mask(ChoppinessComparison.Reference(mapped, 3)),
            VolumePriceComparison.Mask(
                values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray()
            ),
            "chained choppiness",
            ChoppinessComparison.Budget
        );
    }

    [Fact]
    public void ValuePresenceAndMissingPreviousCloseMutationsAreDetected()
    {
        var pair = ChoppinessComparison.Pair;
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        var gaps = CompetitorData.FromOhlc([0, 2, 0], [0, 2, 0], [0, 2, 0], [0, 2, 0]);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = (d, p) => VolumePriceComparison.Mask(new double?[d.Count]),
                },
                gaps,
                2
            )
        );
    }
}
