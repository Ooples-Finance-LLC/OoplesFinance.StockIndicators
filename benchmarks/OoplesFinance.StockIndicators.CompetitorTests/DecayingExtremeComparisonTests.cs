using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DecayingExtremeComparisonTests
{
    [Fact]
    public async Task ChainedCloseUsesTheUpstreamSeries()
    {
        var prices = new[] { 1d, 8, -3, 7, 2, 9 };
        foreach (var maximum in new[] { false, true })
        {
            var indicator = new DecayingWindowExtreme(3, maximum, 2);
            indicator.Of(new FixedPeriodWma(2));
            var actual = await VolumePriceComparisonTests.Run(
                indicator,
                prices.Select(v => (v, v, v, 0d)).ToArray()
            );
            var upstream = FixedWeightedComparison.Stage(prices, 2, false);
            Assert.Equal(
                DecayingExtremeComparison.Reference(upstream, 3, maximum, 2, false),
                actual[0]
            );
        }
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    public async Task IndependentLifecycleContracts(bool maximum, double decay)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(DecayingWindowExtreme),
                "decaying extreme",
                () => new DecayingWindowExtreme(3, maximum, decay)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllConfigurationsMatchIndependentReferences(bool maximum)
    {
        foreach (var period in new[] { 1, 2, 3, 9, 20 })
        foreach (var decay in new[] { 0d, double.Epsilon, .1, 2, 1000 })
            ComparisonVerifier.Check(
                DecayingExtremeComparison.Create(maximum, decay),
                DecayingExtremeComparison.Fixture(),
                period
            );
        var zero = DecayingExtremeComparison.Create(maximum);
        var prices = DecayingExtremeComparison.Fixture().Closes;
        Assert.Equal(
            prices,
            zero.Ooples(DecayingExtremeComparison.Fixture(), 1).Outputs["Value"].Values
        );
        var tied = CompetitorData.FromCloses(maximum ? [10, 2, 10, 2, 2] : [-10, -2, -10, -2, -2]);
        var values = DecayingExtremeComparison
            .Create(maximum, 2)
            .Ooples(tied, 3)
            .Outputs["Value"]
            .Values;
        Assert.Equal(tied.Closes[2], values[2]);
        Assert.NotEqual(values[2], values[3]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WideAndTinyPricesAndMaximumPeriodsRemainFinite(bool maximum)
    {
        foreach (var period in new[] { 1, 3, int.MaxValue })
        foreach (var decay in new[] { 0d, 2, double.MaxValue })
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var indicator = new DecayingWindowExtreme(period, maximum, decay);
            var result = await VolumePriceComparisonTests.Run(
                indicator,
                (value, value, value, 0),
                (value, value, value, 0),
                (value, value, value, 0)
            );
            Assert.Equal(new[] { value, value, value }, result[0]);
        }
        foreach (var decay in new[] { 0d, 2, double.MaxValue })
        {
            var prices = new[]
            {
                double.MaxValue,
                -double.MaxValue,
                double.Epsilon,
                -double.Epsilon,
                0,
            };
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            var actual = DecayingExtremeComparison
                .Owned(bars, 3, maximum, decay)
                .Outputs["Value"]
                .Values;
            Assert.Equal(
                DecayingExtremeComparison.Reference(prices, 3, maximum, decay, false),
                actual
            );
            Assert.All(actual, v => Assert.True(double.IsFinite(v)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeSubscriptionRevisionAndRetainedResetWindowAreExplicit(bool maximum)
    {
        var source = new QuanTAlib.TSeries();
        QuanTAlib.AbstractBase subscribed = maximum
            ? new QuanTAlib.Max(source, 3, 2)
            : new QuanTAlib.Min(source, 3, 2);
        var direct = DecayingExtremeComparison.NativeIndicator(3, maximum, 2);
        foreach (var value in new[] { 1d, 5, 2, 4 })
        {
            var tick = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(tick).Value;
            source.Add(tick);
            Assert.Equal(expected, subscribed.Value);
        }
        var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
        var replay = DecayingExtremeComparison.NativeIndicator(3, maximum, 2);
        foreach (var value in new[] { 1d, 5, 2, 7 })
            replay.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(replay.Value, revised);
        direct.Init();
        var fresh = DecayingExtremeComparison.NativeIndicator(3, maximum, 2);
        foreach (var value in maximum ? new[] { 10d, 0 } : new[] { -10d, 0 })
        {
            direct.Calc(new QuanTAlib.TValue(value, true, false));
            fresh.Calc(new QuanTAlib.TValue(value, true, false));
        }
        Assert.NotEqual(fresh.Value, direct.Value);
    }

    [Fact]
    public void NativeNonfiniteAndParameterHolesAreNotHidden()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DecayingWindowExtreme(0));
        foreach (var maximum in new[] { false, true })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DecayingExtremeComparison.NativeIndicator(0, maximum, 0)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DecayingExtremeComparison.NativeIndicator(3, maximum, -1)
            );
            foreach (
                var decay in new[]
                {
                    -1d,
                    double.NaN,
                    double.PositiveInfinity,
                    double.NegativeInfinity,
                }
            )
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new DecayingWindowExtreme(3, maximum, decay)
                );
            foreach (var decay in new[] { double.NaN, double.PositiveInfinity })
            {
                var native = DecayingExtremeComparison.NativeIndicator(3, maximum, decay);
                Assert.False(
                    double.IsFinite(native.Calc(new QuanTAlib.TValue(1, true, false)).Value)
                );
            }
            var extreme = DecayingExtremeComparison.NativeIndicator(3, maximum, 0);
            extreme.Calc(new QuanTAlib.TValue(double.MaxValue, true, false));
            Assert.False(
                double.IsFinite(
                    extreme.Calc(new QuanTAlib.TValue(double.MaxValue, true, false)).Value
                )
            );
            var pair = DecayingExtremeComparison.Create(maximum);
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Competitor = (_, _) => new(0, Enumerable.Repeat(double.NaN, 15).ToArray()),
                    },
                    DecayingExtremeComparison.Fixture(),
                    3
                )
            );
        }
    }

    [Fact]
    public void EveryOutputAndStartupAndDecayMutationIsDetected()
    {
        foreach (var maximum in new[] { false, true })
        foreach (var native in new[] { false, true })
        foreach (var startup in new[] { false, true })
        {
            var pair = DecayingExtremeComparison.Create(maximum, 2);
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var values = (native ? pair.Competitor(data, period) : pair.Ooples(data, period))
                    .Outputs["Value"]
                    .Values;
                if (!startup)
                    values[^1] += 1;
                return new(startup ? period - 1 : 0, values);
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    DecayingExtremeComparison.Fixture(),
                    3
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = DecayingExtremeComparison.Create(maximum).Ooples,
                    },
                    DecayingExtremeComparison.Fixture(),
                    3
                )
            );
        }
    }
}
