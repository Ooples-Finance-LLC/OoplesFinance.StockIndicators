using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class LogVolatilityComparisonTests
{
    private static Bar[] BarsOf(params double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IndependentRationalLifecycleAndLazyMaximumPeriods(bool realized, bool annual)
    {
        foreach (var p in new[] { 2, 5, int.MaxValue })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WindowLogVolatility),
                    "log volatility",
                    () => new WindowLogVolatility(p, realized, annual)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void FullTrajectoriesCoverBothModesAndAnnualization()
    {
        foreach (var realized in new[] { false, true })
        foreach (var annual in new[] { false, true })
        foreach (var p in new[] { 2, 3, 7, 20 })
        {
            var pair = LogVolatilityComparison.Pair(realized, annual);
            LogVolatilityComparison.Check(pair, CompetitorData.Create(90), p);
            foreach (var shape in ComparisonVerifier.Shapes)
                LogVolatilityComparison.Check(pair, ComparisonVerifier.Fixture(shape, 80), p);
        }
    }

    [Fact]
    public void KnownConstantReturnsSampleCorrectionAndAnnualization()
    {
        foreach (var annual in new[] { false, true })
        {
            var historical = LogVolatilityComparison
                .Owned(BarsOf(1, 2, 4, 8, 16), 2, false, annual)
                .Outputs["Value"];
            Assert.All(historical.Values, v => Assert.Equal(0, v));
            var realized = LogVolatilityComparison
                .Owned(BarsOf(1, 2, 4, 8, 16), 2, true, annual)
                .Outputs["Value"];
            Assert.Equal(0, realized.Values[1]);
            Assert.InRange(
                realized.Values[2],
                Math.Log(2) * (annual ? Math.Sqrt(252) : 1) * (1 - 1e-15),
                Math.Log(2) * (annual ? Math.Sqrt(252) : 1) * (1 + 1e-15)
            );
            var mixed = LogVolatilityComparison.Owned(BarsOf(1, 2, 2), 2, false, annual).Outputs[
                "Value"
            ];
            Assert.InRange(
                mixed.Values[2],
                Math.Log(2) * Math.Sqrt((annual ? 252 : 1) / 2d) * (1 - 1e-15),
                Math.Log(2) * Math.Sqrt((annual ? 252 : 1) / 2d) * (1 + 1e-15)
            );
        }
    }

    [Fact]
    public void UndefinedReturnRecoveryAndSkippedZeroPriorPricesAreExplicit()
    {
        var prices = new[] { 1d, 2, 0, 1, 2, 4, 8 };
        var history = LogVolatilityComparison.Owned(BarsOf(prices), 2, false, false).Outputs[
            "Value"
        ];
        var realized = LogVolatilityComparison.Owned(BarsOf(prices), 2, true, false).Outputs[
            "Value"
        ];
        Assert.Equal(new[] { true, true, false, false, false, true, true }, history.Present);
        Assert.Equal(new[] { true, true, false, true, false, false, false }, realized.Present);
        Assert.Equal(0, realized.Values[3]);
        foreach (var mode in new[] { false, true })
        {
            var pair = LogVolatilityComparison.Pair(mode, false);
            var d = CompetitorData.FromCloses(prices);
            Assert.True(LogVolatilityComparison.Check(pair, d, 2) > 0);
            ComparisonSeries Hide(CompetitorData data, int p)
            {
                var r = pair.Competitor(data, p);
                r.Outputs["Value"].Values[2] = 0;
                return r;
            }
            Assert.Throws<InvalidOperationException>(() =>
                LogVolatilityComparison.Check(pair with { Competitor = Hide }, d, 2)
            );
            LogVolatilityComparison.Check(
                pair,
                CompetitorData.FromCloses([1, -1, -2, -4, -8, -16]),
                2
            );
        }
    }

    [Fact]
    public void ExtremeRatiosAndNearEqualLogReturnsRetainFiniteDispersion()
    {
        foreach (var realized in new[] { false, true })
        foreach (var annual in new[] { false, true })
        foreach (
            var prices in new[]
            {
                new[] { double.Epsilon, double.MaxValue, double.Epsilon, double.MaxValue },
                new[]
                {
                    1d,
                    Math.BitIncrement(1),
                    Math.BitIncrement(Math.BitIncrement(1)),
                    Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(1))),
                },
                new[] { 1d, 1.5, 2.25, Math.BitIncrement(3.375) },
                new[] { -1d, -2, -4, -8 },
            }
        )
        {
            var owned = LogVolatilityComparison.Owned(BarsOf(prices), 2, realized, annual);
            ComparisonVerifier.Compare(
                LogVolatilityComparison.Series(
                    LogVolatilityComparison.Reference(prices, 2, realized, annual)
                ),
                owned,
                "log extremes",
                IndicatorErrorBudget.Exact
            );
            Assert.All(owned.Outputs["Value"].Values, v => Assert.True(double.IsFinite(v)));
        }
        var close = new[] { 1d, 1.5, 2.25, Math.BitIncrement(3.375) };
        Assert.True(
            LogVolatilityComparison.Owned(BarsOf(close), 2, false, false).Outputs["Value"].Values[
                ^1
            ] > 0
        );
        var native = LogVolatilityComparison.NativeReference(
            [double.Epsilon, double.MaxValue, double.Epsilon],
            2,
            true,
            false
        );
        Assert.True(double.IsInfinity(native[2]));
    }

    [Fact]
    public void NativeRevisionDefectsSubscriptionsResetAndHotFlagsAreVerified()
    {
        var prices = new[] { 1d, 2, 4, 5, 8, 16 };
        var changes = new[] { true, true, true, false, true, true };
        foreach (var realized in new[] { false, true })
        foreach (var annual in new[] { false, true })
        {
            var indicator = LogVolatilityComparison.Indicator(2, realized, annual);
            var expected = LogVolatilityComparison.NativeReference(
                prices,
                2,
                realized,
                annual,
                changes
            );
            var count = 0;
            for (var i = 0; i < prices.Length; i++)
            {
                if (changes[i])
                    count++;
                var r = indicator.Calc(new QuanTAlib.TValue(prices[i], changes[i], false));
                Assert.Equal(expected[i], r.Value);
                Assert.Equal(count >= 3, r.IsHot);
            }
            Assert.NotEqual(
                LogVolatilityComparison.NativeReference([1, 2, 5], 2, realized, annual)[^1],
                expected[3]
            );
            var keep = indicator.Value;
            Assert.Equal(keep, indicator.Calc(new QuanTAlib.TValue(double.NaN, true, false)).Value);
            indicator.Init();
            Assert.Equal(0, indicator.Calc(new QuanTAlib.TValue(4, true, false)).Value);
            Assert.False(indicator.IsHot);
            var paired = LogVolatilityComparison.Indicator(2, realized, annual);
            var direct = LogVolatilityComparison.Indicator(2, realized, annual);
            foreach (var p in new[] { 1d, 2, 3, 4 })
                Assert.Equal(
                    direct.Calc(new QuanTAlib.TValue(p, true, false)).Value,
                    paired
                        .Calc(
                            new QuanTAlib.TValue(p, true, false),
                            new QuanTAlib.TValue(999, true, false)
                        )
                        .Value
                );
            var barOnly = LogVolatilityComparison.Indicator(2, realized, annual);
            foreach (var p in new[] { 1d, 2, 4, 8 })
                Assert.Equal(
                    0,
                    barOnly.Calc(new QuanTAlib.TBar(DateTime.UnixEpoch, p, p, p, p, 1, true)).Value
                );
        }
        var source = new QuanTAlib.TSeries();
        var subscriber = new QuanTAlib.Historical(source, 2, false);
        var target = new QuanTAlib.Historical(2, false);
        foreach (var p in prices)
        {
            var tick = new QuanTAlib.TValue(p, true, false);
            source.Add(tick);
            Assert.Equal(target.Calc(tick).Value, subscriber.Value);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Historical(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Realized(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowLogVolatility(1));
        Assert.Throws<OverflowException>(() => new QuanTAlib.Historical(int.MaxValue));
    }

    [Fact]
    public async Task ChainingAndValuePresenceAnnualizationAndModeMutations()
    {
        var data = CompetitorData.Create(40);
        var prices = FixedWeightedComparison.Stage(data.Closes, 3, false);
        foreach (var realized in new[] { false, true })
        {
            var indicator = new WindowLogVolatility(3, realized);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = LogVolatilityComparison.Reference(prices, 3, realized, true);
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
            var pair = LogVolatilityComparison.Pair(realized);
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
                    LogVolatilityComparison.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                LogVolatilityComparison.Check(
                    pair with
                    {
                        Library = LogVolatilityComparison.Pair(realized, false).Ooples,
                    },
                    data,
                    3
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                LogVolatilityComparison.Check(
                    pair with
                    {
                        Library = LogVolatilityComparison.Pair(!realized).Ooples,
                    },
                    data,
                    3
                )
            );
        }
    }
}
