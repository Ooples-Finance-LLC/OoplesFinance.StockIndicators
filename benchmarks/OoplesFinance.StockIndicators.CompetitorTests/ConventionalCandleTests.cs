using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ConventionalCandleTests
{
    private static double Last(
        IIndicator indicator,
        params (double O, double H, double L, double C)[] bars
    )
    {
        var input = bars.Select(
                (b, i) => new Bar(DateTime.UnixEpoch.AddDays(i), b.O, b.H, b.L, b.C, 1)
            )
            .ToArray();
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(input))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return run[indicator.Outputs[0]].ToArray()[^1];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StarRequiresBothBodySizesAndStrictBodyGap(bool reverse)
    {
        var anchor = (O: 2d, H: 8d, L: 0d, C: 6d);
        (double O, double H, double L, double C) Map((double O, double H, double L, double C) b) =>
            reverse ? (-b.O, -b.L, -b.H, -b.C) : b;
        double Signal(
            (double O, double H, double L, double C) a,
            (double O, double H, double L, double C) b
        ) => Last(new ConventionalStarCandle(), Map(a), Map(b));
        Assert.Equal(1, Signal(anchor, (7, 10, 6, 8))); // both size boundaries inclusive
        Assert.Equal(1, Signal(anchor, (8, 10, 6, 7))); // either middle color
        Assert.Equal(1, Signal((6, 8, 0, 2), (7, 10, 6, 8))); // either anchor color
        Assert.Equal(0, Signal(anchor, (6, 10, 6, 7))); // touching body
        Assert.Equal(0, Signal(anchor, (5.5, 9, 5, 6.5))); // overlapping body
        Assert.Equal(0, Signal(anchor, (7, 10, 6, Math.BitIncrement(8)))); // too long
        Assert.Equal(0, Signal((2, Math.BitIncrement(8), 0, 6), (7, 10, 6, 8))); // anchor too short
        Assert.Equal(0, Signal((6, 6, 6, 6), (7, 10, 6, 8))); // zero prior range
        Assert.Equal(0, Signal(anchor, (7, 7, 7, 7))); // zero current range
        Assert.Equal(1, Signal(anchor, (7, 10, 6, 7))); // nonzero-range doji is short
        Assert.Equal(0, Last(new ConventionalStarCandle(), Map(anchor))); // no anchor
    }

    [Fact]
    public void BothShadowsMustQualifyIncludingExactThreshold()
    {
        var indicator = new ShortShadowsCandle();
        Assert.Equal(1, Last(indicator, (1, 10, 0, 9)));
        Assert.Equal(1, Last(indicator, (9, 10, 0, 1)));
        Assert.Equal(1, Last(indicator, (0, 10, 0, 10)));
        Assert.Equal(0, Last(indicator, (1, 10, 0, Math.BitDecrement(9))));
        Assert.Equal(0, Last(indicator, (Math.BitIncrement(1), 10, 0, 9)));
        Assert.Equal(0, Last(indicator, (5, 10, 0, 5)));
        Assert.Equal(0, Last(indicator, (5, 5, 5, 5)));
    }

    [Fact]
    public void ExactComparisonsRetainExtremeAndSubnormalSignals()
    {
        foreach (var scale in new[] { double.Epsilon, Math.ScaleB(1d, 1019) })
        {
            Assert.Equal(
                1,
                Last(
                    new ConventionalStarCandle(),
                    (2 * scale, 8 * scale, 0, 6 * scale),
                    (7 * scale, 10 * scale, 6 * scale, 8 * scale)
                )
            );
            Assert.Equal(1, Last(new ShortShadowsCandle(), (scale, 10 * scale, 0, 9 * scale)));
        }
        Assert.Equal(
            1,
            Last(
                new ShortShadowsCandle(),
                (-double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue)
            )
        );
        Assert.Equal(
            1,
            Last(
                new ConventionalStarCandle(),
                (-double.MaxValue, double.MaxValue, -double.MaxValue, 0),
                (1, 4, 0, 2)
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentReferenceAndLifecycle(bool star)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                star ? typeof(ConventionalStarCandle) : typeof(ShortShadowsCandle),
                "owned conventional definition",
                () => star ? new ConventionalStarCandle() : new ShortShadowsCandle()
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void AvailabilityDoesNotClaimNativeParityOrTimings()
    {
        foreach (var (id, indicator) in ComparisonManifest.OwnedDefinitions)
        {
            Assert.Contains(id, CompetitorApiCatalog.Unimplemented);
            var row = Assert.Single(ComparisonManifest.Create(), r => r.Id == id);
            Assert.Equal("unavailable", row.Status);
            Assert.Equal(indicator, row.OoplesIndicator);
            Assert.Equal(indicator, row.OwnedDefinition);
            Assert.Null(row.AlternateComparison);
            Assert.Contains("Ooples conventional definition", row.CompetitorLimitation);
            Assert.DoesNotContain(ComparisonPairs.All, p => p.Id == id);
        }
    }
}
