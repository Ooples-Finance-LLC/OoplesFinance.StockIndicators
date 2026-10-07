using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class McGinleyComparisonTests
{
    private static Bar[] BarsOf(params double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(McGinleyStartup.Immediate)]
    [InlineData(McGinleyStartup.ResetDelay)]
    public async Task RationalContractsCoverLifecycleAndParameterExtremes(McGinleyStartup mode)
    {
        foreach (
            var config in new[]
            {
                (1, .6),
                (3, 2d),
                (int.MaxValue, .6),
                (3, double.MaxValue),
                (2, double.Epsilon),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(QuarticDynamicAverage),
                    "McGinley",
                    () => new QuarticDynamicAverage(config.Item1, config.Item2, mode)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void AllConfigurationsAndShapesHaveCompleteIndependentTrajectories()
    {
        foreach (var reset in new[] { false, true })
        foreach (var p in new[] { 1, 2, 7, 20 })
        foreach (var k in new[] { .6, 1d, 2 })
        {
            var pair = McGinleyComparison.Pair(reset, k);
            McGinleyComparison.Check(pair, CompetitorData.Create(80), p);
            foreach (var shape in ComparisonVerifier.Shapes)
                McGinleyComparison.Check(pair, ComparisonVerifier.Fixture(shape, 80), p);
        }
    }

    [Fact]
    public void QuarticKnownValueAndZeroRestartConventionsAreExplicit()
    {
        foreach (var reset in new[] { false, true })
            Assert.Equal(
                17d / 16,
                McGinleyComparison.Owned(BarsOf(1, 2), 1, 1, reset).Outputs["Value"].Values[1]
            );
        var prices = new[] { 0d, 2, 2, 2, 2, 2 };
        var delayed = McGinleyComparison.Owned(BarsOf(prices), 3, 1, true).Outputs["Value"];
        Assert.Equal(new[] { false, false, false, false, true, true }, delayed.Present);
        Assert.Equal(2, delayed.Values[4]);
        var immediate = McGinleyComparison.Owned(BarsOf(prices), 3, 1, false).Outputs["Value"];
        Assert.All(immediate.Present!, Assert.True);
        Assert.Equal(0, immediate.Values[0]);
        Assert.Equal(2d / 3, immediate.Values[1]);
        var roundToZero = McGinleyComparison.Owned(BarsOf(2, 1, 4, 4, 4), 1, 16, true).Outputs[
            "Value"
        ];
        Assert.Equal(1, roundToZero.Values[1]);
        var restart = McGinleyComparison.Owned(BarsOf(2, 1, 4, 4, 4), 1, 8, true).Outputs["Value"];
        Assert.Equal(0, restart.Values[1]);
        Assert.False(restart.Present![2]);
        Assert.Equal(4, restart.Values[3]);
        var max = McGinleyComparison.Owned(BarsOf(0, 2, 2), int.MaxValue, 1, true).Outputs["Value"];
        Assert.All(max.Present!, v => Assert.False(v));
        var native = prices.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v));
        Assert.Throws<OverflowException>(() => native.GetDynamic(int.MaxValue).ToArray());
    }

    [Fact]
    public void ExactRecurrenceRetainsOverflowingDifferenceAndRejectsSingularStates()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue };
        foreach (var reset in new[] { false, true })
        {
            var result = McGinleyComparison.Owned(BarsOf(prices), 1, 16, reset).Outputs["Value"];
            Assert.Equal(-double.MaxValue * .875, result.Values[1]);
            Assert.True(
                double.IsPositiveInfinity(
                    McGinleyComparison.NativeReference(prices, 1, 16, reset)[1]!.Value
                )
            );
            Assert.Throws<IndicatorOutputException>(() =>
                McGinleyComparison.Owned(BarsOf(1, 0), 3, .6, reset)
            );
            Assert.Throws<IndicatorOutputException>(() =>
                McGinleyComparison.Owned(BarsOf(1, .5), 1, double.Epsilon, reset)
            );
        }
        Assert.Throws<IndicatorOutputException>(() =>
            McGinleyComparison.Owned(BarsOf(0, 1, 0), 20, 1, true)
        );
        foreach (var v in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        foreach (var reset in new[] { false, true })
        {
            var result = McGinleyComparison
                .Owned(BarsOf(v, v, v), int.MaxValue, double.MaxValue, reset)
                .Outputs["Value"];
            Assert.All(result.Values.Where((_, i) => result.Present![i]), x => Assert.Equal(v, x));
        }
    }

    [Fact]
    public void NativeSubscriptionRevisionResetHotAndInvalidInputsAreCovered()
    {
        var source = new QuanTAlib.TSeries();
        var subscriber = new QuanTAlib.Mgdi(source, 3);
        var direct = new QuanTAlib.Mgdi(3);
        var prices = new[] { 100d, 101, 99, 100, 102 };
        for (var i = 0; i < prices.Length; i++)
        {
            var tick = new QuanTAlib.TValue(prices[i], true, false);
            source.Add(tick);
            var row = direct.Calc(tick);
            Assert.Equal(row.Value, subscriber.Value);
            Assert.Equal(i >= 2, row.IsHot);
            var revised = direct.Calc(new QuanTAlib.TValue(prices[i] + 1, false, false));
            Assert.Equal(
                McGinleyComparison.NativeReference(
                    prices.Take(i).Append(prices[i] + 1).ToArray(),
                    3,
                    .6,
                    false
                )[^1],
                revised.Value
            );
            direct.Calc(new QuanTAlib.TValue(prices[i], false, false));
        }
        var retained = direct.Value;
        Assert.Equal(retained, direct.Calc(new QuanTAlib.TValue(double.NaN, true, false)).Value);
        Assert.Equal(
            retained,
            direct.Calc(new QuanTAlib.TValue(double.PositiveInfinity, true, false)).Value
        );
        direct.Init();
        Assert.Equal(8, direct.Calc(new QuanTAlib.TValue(8, true, false)).Value);
        Assert.False(direct.IsHot);
        var nonfinite = new QuanTAlib.Mgdi(3, double.NaN);
        nonfinite.Calc(new QuanTAlib.TValue(1, true, false));
        Assert.True(double.IsNaN(nonfinite.Calc(new QuanTAlib.TValue(2, true, false)).Value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Mgdi(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Mgdi(2, 0));
    }

    [Fact]
    public void SkenderRoutesSortingReusableStartupAndParameterHoles()
    {
        var data = CompetitorData.Create(40);
        var q = data.Quotes;
        var t = q.Select(v => (v.Date, (double)v.Close)).ToArray();
        var expected = q.GetDynamic(3).Select(v => v.Dynamic).ToArray();
        foreach (
            var rows in new[]
            {
                q.GetDynamic(3, .6),
                q.AsEnumerable().Reverse().GetDynamic(3),
                t.Reverse().GetDynamic(3),
                q.GetSma(1).GetDynamic(3),
            }
        )
            Assert.Equal(expected, rows.Select(v => v.Dynamic));
        var sma = q.GetSma(3).ToArray();
        Assert.Equal(
            McGinleyComparison.NativeReference(
                sma.Skip(2).Select(v => v.Sma!.Value).ToArray(),
                3,
                .6,
                true
            ),
            sma.GetDynamic(3).Skip(2).Select(v => v.Dynamic)
        );
        Assert.All(q.GetDynamic(3, double.NaN), v => Assert.Null(v.Dynamic));
        Assert.Equal((double)q[0].Close, q.GetDynamic(3, double.PositiveInfinity).Last().Dynamic);
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetDynamic(0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => q.GetDynamic(3, 0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuarticDynamicAverage(0));
        foreach (var k in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new QuarticDynamicAverage(3, k));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new QuarticDynamicAverage(3, .6, (McGinleyStartup)99)
        );
    }

    [Fact]
    public async Task ChainingAndValuePresenceFactorAndModeMutationsAreDetected()
    {
        var data = CompetitorData.Create(40);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        foreach (var reset in new[] { false, true })
        {
            var indicator = new QuarticDynamicAverage(
                3,
                .6,
                reset ? McGinleyStartup.ResetDelay : McGinleyStartup.Immediate
            );
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = McGinleyComparison.GridReference(closes, 3, .6, reset);
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
            var pair = McGinleyComparison.Pair(reset);
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
                    McGinleyComparison.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                McGinleyComparison.Check(
                    pair with
                    {
                        Library = McGinleyComparison.Pair(!reset).Ooples,
                    },
                    data,
                    3
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                McGinleyComparison.Check(
                    pair with
                    {
                        Library = McGinleyComparison.Pair(reset, 2).Ooples,
                    },
                    data,
                    3
                )
            );
            var singular = CompetitorData.FromCloses([1, 0, 1]);
            Assert.True(McGinleyComparison.Check(pair, singular, 3) > 0);
            Assert.Throws<InvalidOperationException>(() =>
                McGinleyComparison.Check(
                    pair with
                    {
                        Library = (d, p) => McGinleyComparison.Series(new double?[d.Count]),
                    },
                    singular,
                    3
                )
            );
            ComparisonSeries Hide(CompetitorData d, int p)
            {
                var r = pair.Competitor(d, p);
                r.Outputs["Value"].Values[1] = 0;
                return r;
            }
            Assert.Throws<InvalidOperationException>(() =>
                McGinleyComparison.Check(pair with { Competitor = Hide }, singular, 3)
            );
        }
    }
}
