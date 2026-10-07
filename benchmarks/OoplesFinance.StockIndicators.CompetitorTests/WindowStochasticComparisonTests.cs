using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class WindowStochasticComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RationalContractsCoverBothSmoothersAndLifecycle(bool wilder)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowStochasticKdj),
                "window stochastic " + wilder,
                () => new WindowStochasticKdj(3, 2, 4, wilder, .5, 1.5)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void AllPeriodSmoothingAndFactorSettingsMatchIndependentModels()
    {
        foreach (var wilder in new[] { false, true })
        foreach (var periods in new[] { (1, 1, 1), (2, 1, 3), (3, 2, 1), (5, 3, 3), (14, 5, 3) })
        foreach (var factors in new[] { (3d, 2d), (.5, 1.5), (1d, 1d) })
        {
            var pair = WindowStochasticComparison.Pair(
                periods.Item1,
                periods.Item2,
                periods.Item3,
                wilder,
                factors.Item1,
                factors.Item2
            );
            ComparisonVerifier.Check(pair, CompetitorData.Create(80), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 50), 20);
        }
    }

    [Fact]
    public void StandardExtendedAliasesAndReusableOutputAgree()
    {
        var data = CompetitorData.Create(50);
        var standard = data.Quotes.GetStoch(5, 4, 3).ToArray();
        var extended = data
            .Quotes.AsEnumerable()
            .Reverse()
            .GetStoch(5, 4, 3, 3, 2, MaType.SMA)
            .ToArray();
        var reference = WindowStochasticComparison.NativeReference(
            WindowStochasticComparison.QuoteBars(data),
            5,
            3,
            4,
            false,
            3,
            2
        );
        Assert.Equal(reference[0], standard.Select(r => r.K));
        Assert.Equal(reference[1], standard.Select(r => r.D));
        Assert.Equal(reference[2], standard.Select(r => r.J));
        Assert.Equal(standard.Select(r => r.Date), extended.Select(r => r.Date));
        Assert.Equal(standard.Select(r => r.K), extended.Select(r => r.Oscillator));
        Assert.Equal(standard.Select(r => r.D), extended.Select(r => r.Signal));
        Assert.Equal(standard.Select(r => r.J), extended.Select(r => r.PercentJ));
        Assert.Equal(standard.Select(r => r.K), standard.Select(r => ((IReusableResult)r).Value));
    }

    [Fact]
    public void EachOutputHasItsOwnStartupAndFlatConvention()
    {
        var data = CompetitorData.Create(15);
        foreach (var wilder in new[] { false, true })
        {
            var result = WindowStochasticComparison.Owned(
                data.IndicatorBars,
                3,
                2,
                4,
                wilder,
                3,
                2
            );
            Assert.Equal(wilder ? 2 : 3, Array.FindIndex(result.Outputs["K"].Present!, p => p));
            Assert.Equal(wilder ? 2 : 6, Array.FindIndex(result.Outputs["D"].Present!, p => p));
            Assert.Equal(result.Outputs["D"].Present, result.Outputs["J"].Present);
            var flat = WindowStochasticComparison.Owned(
                ComparisonVerifier.Fixture("constant", 15).CloseBars,
                3,
                2,
                4,
                wilder,
                3,
                2
            );
            foreach (var output in flat.Outputs.Values)
                Assert.All(
                    output.Values.Where((_, i) => output.Present![i]),
                    value => Assert.Equal(0, value)
                );
        }
        var tiny = CompetitorData.FromOhlcv([0], [double.Epsilon], [0], [double.Epsilon], [1]);
        var pair = WindowStochasticComparison.Pair(1, 1, 1);
        ComparisonVerifier.Check(pair, tiny, 20);
        Assert.Equal(100, pair.Ooples(tiny, 20).Outputs["K"].Values[0]);
        Assert.Equal(0, pair.Competitor(tiny, 20).Outputs["K"].Values[0]);
    }

    [Fact]
    public void NativeShortSmmaAndPeriodOverflowAreExplicitAndOwnedHistoryIsLazy()
    {
        var data = CompetitorData.Create(3);
        foreach (var count in new[] { 0, 1, 2 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                data.Quotes.Take(count).GetStoch(3, 2, 2, 3, 2, MaType.SMMA).ToArray()
            );
            var owner = WindowStochasticComparison.Owned(
                data.IndicatorBars.Take(count).ToArray(),
                3,
                2,
                2,
                true,
                3,
                2
            );
            Assert.All(owner.Outputs["K"].Present!, value => Assert.False(value));
        }
        Assert.Throws<OverflowException>(() =>
            data.Quotes.GetStoch(1, int.MaxValue, int.MaxValue, 3, 2, MaType.SMMA).ToArray()
        );
        foreach (var wilder in new[] { false, true })
        {
            var huge = WindowStochasticComparison.Owned(
                data.IndicatorBars,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                wilder,
                3,
                2
            );
            Assert.All(huge.Outputs["K"].Present!, value => Assert.False(value));
            var wideSmoothing = WindowStochasticComparison.Owned(
                data.IndicatorBars,
                1,
                int.MaxValue,
                int.MaxValue,
                wilder,
                3,
                2
            );
            Assert.All(wideSmoothing.Outputs["K"].Present!, value => Assert.Equal(wilder, value));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetStoch(0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetStoch(1, 0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetStoch(1, 1, 0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetStoch(1, 1, 1, 3, 2, MaType.EMA).ToArray()
        );
    }

    [Fact]
    public void NativeNonfiniteFactorsAndOwnerCancellationAreVerified()
    {
        var data = CompetitorData.FromOhlcv([.5], [1], [0], [.5], [1]);
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.MaxValue })
        {
            var native = data.Quotes.GetStoch(1, 1, 1, factor, factor, MaType.SMA).Single();
            var expected = WindowStochasticComparison.NativeReference(
                WindowStochasticComparison.QuoteBars(data),
                1,
                1,
                1,
                false,
                factor,
                factor
            );
            Assert.Equal(expected[2][0], native.J);
            Assert.True(double.IsNaN(native.J!.Value));
        }
        var owned = WindowStochasticComparison.Owned(
            data.IndicatorBars,
            1,
            1,
            1,
            false,
            double.MaxValue,
            double.MaxValue
        );
        Assert.Equal(0, owned.Outputs["J"].Values[0]);
        foreach (
            var factor in new[]
            {
                0d,
                -1,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            }
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WindowStochasticKdj(kFactor: factor)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WindowStochasticKdj(dFactor: factor)
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowStochasticKdj(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowStochasticKdj(kPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowStochasticKdj(dPeriod: 0));
    }

    [Fact]
    public async Task WideUnpublishedRatiosCancelAndPublishedOverflowIsRejected()
    {
        Bar[] oversized =
        [
            new(DateTime.UnixEpoch, 0, 1, 0, double.MaxValue / 25, 1),
            new(DateTime.UnixEpoch.AddDays(1), 0, 1, 0, -double.MaxValue / 25, 1),
        ];
        ComparisonVerifier.Compare(
            WindowStochasticComparison.Series(
                WindowStochasticComparison.Reference(oversized, 1, 2, 1, false, 3, 2)
            ),
            WindowStochasticComparison.Owned(oversized, 1, 2, 1, false, 3, 2),
            "unpublished cancellation",
            IndicatorErrorBudget.Exact
        );
        foreach (var scale in new[] { double.Epsilon, double.MaxValue })
        foreach (var wilder in new[] { false, true })
        {
            Bar[] bars =
            [
                new(DateTime.UnixEpoch, 0, scale, -scale, 0, 1),
                new(DateTime.UnixEpoch.AddDays(1), scale, scale, -scale, scale, 1),
            ];
            ComparisonVerifier.Compare(
                WindowStochasticComparison.Series(
                    WindowStochasticComparison.Reference(bars, 1, 2, 2, wilder, .5, 1.5)
                ),
                WindowStochasticComparison.Owned(bars, 1, 2, 2, wilder, .5, 1.5),
                "extreme range",
                IndicatorErrorBudget.Exact
            );
        }
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowStochasticKdj),
                "published overflow",
                () => new WindowStochasticKdj(1, 1, 1)
            ),
            new IndicatorValidationOptions
            {
                AdditionalFixtures =
                [
                    new IndicatorValidationFixture("oversized published K", oversized),
                ],
            }
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        Assert.True(report.OutputOverflowRejectionsChecked > 0);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new WindowStochasticKdj(1, 1, 1, false, double.MaxValue, 1),
                (1, 0, 1, 1)
            )
        );
    }

    [Fact]
    public async Task ChainingAndAllOutputMaskAndParameterMutationsAreDetected()
    {
        var data = CompetitorData.Create(70);
        var indicator = new WindowStochasticKdj(3, 2, 4, true, .5, 1.5);
        indicator.Of(new FixedPeriodWma(3));
        var downstream = new FixedPeriodWma(3);
        downstream.Of(indicator);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator, downstream)
            .BuildAsync();
        var source = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, source[i], b.Volume)
            )
            .ToArray();
        var expected = WindowStochasticComparison.Reference(bars, 3, 2, 4, true, .5, 1.5);
        for (var j = 0; j < 3; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 3]].ToArray()
            );
        }
        Assert.Equal(
            FixedWeightedComparison.Stage(expected[0].Select(v => v ?? 0).ToArray(), 3, false),
            run[downstream.Outputs[0]].ToArray()
        );
        var pair = WindowStochasticComparison.Pair(3, 2, 4, true, .5, 1.5);
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        foreach (var name in WindowStochasticComparison.Names)
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (mask)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    20
                )
            );
        }
        foreach (
            var wrong in new[]
            {
                WindowStochasticComparison.Pair(3, 2, 4, false, .5, 1.5),
                WindowStochasticComparison.Pair(2, 2, 4, true, .5, 1.5),
                WindowStochasticComparison.Pair(3, 2, 4, true, 1.5, .5),
            }
        )
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair with { Library = wrong.Library }, data, 20)
            );
    }
}
