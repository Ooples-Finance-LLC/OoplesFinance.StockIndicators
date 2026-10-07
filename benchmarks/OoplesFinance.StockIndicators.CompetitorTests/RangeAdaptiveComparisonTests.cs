using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RangeAdaptiveComparisonTests
{
    [Theory]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 20)]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(false, 20)]
    public async Task RationalContractsCoverResetAndLifecycle(bool fractal, int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                fractal ? typeof(RangeFractalAverage) : typeof(FilteredDeviationAverage),
                "range adaptive",
                () => RangeAdaptiveComparison.Indicator(period, fractal)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void BoundariesAndHugePeriodsRemainLazy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RangeFractalAverage(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FilteredDeviationAverage(0));
        foreach (var bad in new[] { 0d, -1, 1.1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new FilteredDeviationAverage(3, bad));
        Assert.Equal(int.MaxValue, new FilteredDeviationAverage(int.MaxValue).WarmupBars);
        var data = CompetitorData.Create(15);
        foreach (var fractal in new[] { false, true })
            ComparisonVerifier.Compare(
                new ComparisonSeries(
                    0,
                    RangeAdaptiveComparison.GridReference(data.Closes, int.MaxValue, fractal)
                ),
                RangeAdaptiveComparison.Owned(data.IndicatorBars, int.MaxValue, fractal),
                "lazy adaptive",
                IndicatorErrorBudget.Exact
            );
        var nan = new QuanTAlib.Dsma(3, double.NaN);
        Assert.Equal(1, nan.Calc(new QuanTAlib.TValue(1, true, false)).Value);
        Assert.True(double.IsNaN(nan.Calc(new QuanTAlib.TValue(2, true, false)).Value));
    }

    [Fact]
    public void ExtremeAndSubnormalInputsKeepFiniteConvexOwnedOutputs()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200, 0d })
        foreach (var alternate in new[] { false, true })
        foreach (var fractal in new[] { false, true })
        {
            var prices = Enumerable
                .Range(0, 90)
                .Select(i => alternate && i % 2 != 0 ? -scale : scale)
                .ToArray();
            var expected = RangeAdaptiveComparison.GridReference(prices, 7, fractal);
            Assert.All(expected, v => Assert.True(double.IsFinite(v)));
            Assert.All(expected, v => Assert.InRange(v, -scale, scale));
            ComparisonVerifier.Compare(
                new ComparisonSeries(0, expected),
                RangeAdaptiveComparison.Owned(BarsOf(prices), 7, fractal),
                "wide range average",
                IndicatorErrorBudget.Exact
            );
            var nativeExpected = RangeAdaptiveComparison.NativeReference(prices, 7, fractal, .9);
            Assert.Equal(
                nativeExpected,
                RangeAdaptiveComparison.Native(prices, 7, fractal, .9).Outputs["Value"].Values
            );
        }
    }

    [Fact]
    public void NativeRevisionsAndRetainedResetSquaresAreExplicit()
    {
        var prices = CompetitorData.Create(60).Closes;
        foreach (var fractal in new[] { false, true })
        foreach (var count in new[] { 1, 2, 26 })
        {
            var owner = RangeAdaptiveComparison.NativeIndicator(20, fractal);
            foreach (var value in prices.Take(count))
                owner.Calc(new QuanTAlib.TValue(value, true, false));
            var updated = prices.Take(count).ToArray();
            updated[^1] += 7;
            Assert.Equal(
                RangeAdaptiveComparison.NativeReference(updated, 20, fractal, .9)[^1],
                owner.Calc(new QuanTAlib.TValue(updated[^1], false, false)).Value
            );
        }
        var reused = new QuanTAlib.Dsma(20);
        foreach (var value in new[] { 1000d, -1000, 1000, -1000 })
            reused.Calc(new QuanTAlib.TValue(value, true, false));
        reused.Init();
        reused.Calc(new QuanTAlib.TValue(1, true, false));
        var dirty = reused.Calc(new QuanTAlib.TValue(2, true, false)).Value;
        var fresh = new QuanTAlib.Dsma(20);
        fresh.Calc(new QuanTAlib.TValue(1, true, false));
        Assert.NotEqual(fresh.Calc(new QuanTAlib.TValue(2, true, false)).Value, dirty);
    }

    [Fact]
    public async Task ChainingAndFormulaConfigurationMutationsAreDetected()
    {
        var data = CompetitorData.Create(90);
        foreach (var fractal in new[] { false, true })
        {
            var owner = RangeAdaptiveComparison.Indicator(7, fractal);
            owner.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(owner)
                .BuildAsync();
            Assert.Equal(
                RangeAdaptiveComparison.GridReference(
                    FixedWeightedComparison.Stage(data.Closes, 3, false),
                    7,
                    fractal
                ),
                run[owner.Outputs[0]].ToArray()
            );
            var pair = RangeAdaptiveComparison.Pair(fractal);
            foreach (var native in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData d, int p)
                {
                    var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                    result.Outputs["Value"].Values[^1] += 1;
                    return result;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        7
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = (d, p) =>
                            RangeAdaptiveComparison.Owned(d.IndicatorBars, p + 2, fractal),
                    },
                    data,
                    7
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                RangeAdaptiveComparison.Pair(false) with
                {
                    Library = RangeAdaptiveComparison.Pair(false, .2).Library,
                },
                data,
                20
            )
        );
    }

    private static Bar[] BarsOf(double[] prices) =>
        prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddDays(i), p, p, p, p, 0)).ToArray();
}
