using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CompensatedAverageComparisonTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(2, 20)]
    [InlineData(3, 20)]
    public async Task IndependentLifecycleContracts(int order, int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(CompensatedExponentialAverage),
                "compensated EMA",
                () => new CompensatedExponentialAverage(period, order)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void StartupCutoffAndMaximumPeriodFollowIndependentReferences(int order)
    {
        var pair = CompensatedAverageComparison.Create(order);
        foreach (var period in new[] { 1, 2, 3, 20, 100, int.MaxValue })
            ComparisonVerifier.Check(pair, CompensatedAverageComparison.Fixture(), period);
        foreach (var period in new[] { 2, 3, 20 })
            ComparisonVerifier.Check(
                pair,
                ComparisonVerifier.Fixture("walk", 16 * period + 13),
                period
            );
        var data = CompetitorData.FromCloses([4, 8, 2, 12]);
        Assert.Equal(data.Closes, pair.Ooples(data, 1).Outputs["Value"].Values);
        Assert.Equal(0, pair.Ooples(data, 3).Outputs["Value"].FirstValid);
        var actual = pair.Ooples(data, 3).Outputs["Value"].Values;
        Assert.Equal(4, actual[0]);
        Assert.NotEqual(6, actual[1]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ExtendedStagesCancellationSubnormalsAndTrueOverflow(int order)
    {
        foreach (var period in new[] { 1, 3, int.MaxValue })
        foreach (var value in new[] { double.Epsilon, -double.Epsilon, 1e200, -1e200 })
        {
            var prices = new[] { value, value, -value, value };
            var result = await VolumePriceComparisonTests.Run(
                new CompensatedExponentialAverage(period, order),
                prices.Select(v => (v, v, v, 0d)).ToArray()
            );
            Assert.Equal(
                CompensatedAverageComparison.Reference(prices, period, order, false),
                result[0]
            );
        }
        var constant = await VolumePriceComparisonTests.Run(
            new CompensatedExponentialAverage(3, order),
            (double.MaxValue, double.MaxValue, double.MaxValue, 0)
        );
        Assert.Equal(double.MaxValue, constant[0][0]);
        var maxBars = new[]
        {
            new Bar(
                DateTime.UnixEpoch,
                double.MaxValue,
                double.MaxValue,
                double.MaxValue,
                double.MaxValue,
                0
            ),
        };
        Assert.Equal(
            double.MaxValue,
            CompensatedAverageComparison.Owned(maxBars, 1, order).Outputs["Value"].Values[0]
        );
        var native = CompensatedAverageComparison.NativeIndicator(1, order);
        Assert.False(
            double.IsFinite(native.Calc(new QuanTAlib.TValue(double.MaxValue, true, false)).Value)
        );
        var pricesOverflow = new[] { 0d, double.MaxValue, double.MaxValue };
        Assert.True(
            double.IsInfinity(
                CompensatedAverageComparison.Reference(pricesOverflow, 3, order, false)[2]
            )
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(async () =>
            await VolumePriceComparisonTests.Run(
                new CompensatedExponentialAverage(3, order),
                (0, 0, 0, 0),
                (double.MaxValue, double.MaxValue, double.MaxValue, 0),
                (double.MaxValue, double.MaxValue, double.MaxValue, 0)
            )
        );
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SubscriptionRevisionResetAndChaining(int order)
    {
        var source = new QuanTAlib.TSeries();
        QuanTAlib.AbstractBase subscribed =
            order == 2 ? new QuanTAlib.Dema(source, 3) : new QuanTAlib.Tema(source, 3);
        var direct = CompensatedAverageComparison.NativeIndicator(3, order);
        foreach (var value in new[] { 1d, 5, 2, 4 })
        {
            var tick = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(tick).Value;
            source.Add(tick);
            Assert.Equal(expected, subscribed.Value);
        }
        var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
        var replay = CompensatedAverageComparison.NativeIndicator(3, order);
        foreach (var value in new[] { 1d, 5, 2, 7 })
            replay.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(replay.Value, revised);
        direct.Init();
        var fresh = CompensatedAverageComparison.NativeIndicator(3, order);
        foreach (var value in new[] { 8d, 3, 9 })
            Assert.Equal(
                fresh.Calc(new QuanTAlib.TValue(value, true, false)).Value,
                direct.Calc(new QuanTAlib.TValue(value, true, false)).Value
            );
        var prices = new[] { 1d, 8, -3, 7, 2, 9 };
        var indicator = new CompensatedExponentialAverage(3, order);
        indicator.Of(new FixedPeriodWma(2));
        var actual = await VolumePriceComparisonTests.Run(
            indicator,
            prices.Select(v => (v, v, v, 0d)).ToArray()
        );
        Assert.Equal(
            CompensatedAverageComparison.Reference(
                FixedWeightedComparison.Stage(prices, 2, false),
                3,
                order,
                false
            ),
            actual[0]
        );
    }

    [Fact]
    public void InvalidParametersAndCorruptedOutputsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompensatedExponentialAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompensatedExponentialAverage(3, 1));
        foreach (var order in new[] { 2, 3 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CompensatedAverageComparison.NativeIndicator(0, order)
            );
            var pair = CompensatedAverageComparison.Create(order);
            foreach (var native in new[] { false, true })
            foreach (var startup in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int period)
                {
                    var values = (
                        native ? pair.Competitor(data, period) : pair.Ooples(data, period)
                    )
                        .Outputs["Value"]
                        .Values;
                    if (!startup)
                        values[^1] += 1;
                    return new(startup ? period - 1 : 0, values);
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        CompensatedAverageComparison.Fixture(),
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = CompensatedAverageComparison.Create(order == 2 ? 3 : 2).Ooples,
                    },
                    CompensatedAverageComparison.Fixture(),
                    3
                )
            );
        }
    }
}
