using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class QuanAtrComparisonTests
{
    [Fact]
    public void PinnedBarRouteUsesScaledRangeRatherThanTemporalAverage()
    {
        var direct = new QuanTAlib.Atr(3);
        var source = new QuanTAlib.TBarSeries();
        var subscribed = new QuanTAlib.Atr(source, 3);
        var corrected = new QuanTAlib.Ema(1d / 3);
        var outputs = new List<double>();
        foreach (var range in new[] { 2d, 6d, 12d, 4d })
        {
            var bar = new QuanTAlib.TBar(
                DateTime.UnixEpoch.AddDays(outputs.Count),
                0,
                range / 2,
                -range / 2,
                0,
                1,
                true
            );
            var value = direct.Calc(bar).Value;
            source.Add(bar);
            Assert.Equal((1d / 3) * range, value);
            Assert.Equal(value, subscribed.Value);
            Assert.False(direct.Input.IsNew);
            outputs.Add(value);
            corrected.Calc(new QuanTAlib.TValue(range, true, false));
        }
        Assert.Equal(new[] { 2d / 3, 2, 4, 4d / 3 }, outputs);
        Assert.True(Math.Abs(corrected.Value - outputs[^1]) > 1);
        direct.Init();
        Assert.Equal(
            2,
            direct.Calc(new QuanTAlib.TBar(DateTime.UnixEpoch, 0, 3, -3, 0, 1, true)).Value
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task ExactScaledRangeContractAndLifecycle(int divisor)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ScaledTrueRange),
                "scaled range",
                () => new ScaledTrueRange(divisor)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task ScalingPrecedesPublishedOverflowAndRetainsSubnormals()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, 0, 0),
        };
        var indicator = new ScaledTrueRange(2);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(double.MaxValue, run[indicator.Outputs[0]].ToArray()[0]);
        var tiny = new ScaledTrueRange(2);
        using var smallRun = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(new[] { new Bar(DateTime.UnixEpoch, 0, 3 * double.Epsilon, 0, 0, 0) })
            )
            .ConfigureIndicators(tiny)
            .BuildAsync();
        Assert.Equal(2 * double.Epsilon, smallRun[tiny.Outputs[0]].ToArray()[0]);
    }

    [Fact]
    public void GapsAndFirstRangeMatchIndependentReferences()
    {
        foreach (var divisor in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(
                QuanAtrComparison.Pair,
                SeededAtrComparison.Fixture(),
                divisor
            );
            ComparisonVerifier.Check(
                QuanAtrComparison.Pair,
                CompetitorData.FromCloses([5]),
                divisor
            );
        }
    }

    [Fact]
    public void WrongSmoothingOrScaleCannotPassTheOracle()
    {
        var pair = QuanAtrComparison.Pair;
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                return new(0, result.Outputs["Value"].Values.Select(v => v + 1).ToArray());
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    SeededAtrComparison.Fixture(),
                    3
                )
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScaledTrueRange(0));
    }
}
