using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PivotLevelComparisonTests
{
    [Fact]
    public void FiveFormulaGoldensAndCurrentOpenSemanticsAreExplicit()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 8, 12, 6, 9, 0),
            new Bar(DateTime.UnixEpoch.AddDays(1), 12, 14, 10, 13, 0),
        };
        var expected = new[]
        {
            new PivotLevelValue(9, 6, 3, 0, null, 12, 15, 18, null),
            new PivotLevelValue(9, 8.45, 7.9, 7.35, 5.7, 9.55, 10.1, 10.65, 12.3),
            new PivotLevelValue(8.25, 4.5, null, null, null, 10.5, null, null, null),
            new PivotLevelValue(9, 6.708, 5.292, 3, null, 11.292, 12.708, 15, null),
            new PivotLevelValue(10.5, 9, 4.5, 3, null, 15, 16.5, 21, null),
        };
        foreach (var style in Enum.GetValues<PivotLevelStyle>())
        {
            var result = PivotLevelSnapshots.Rolling(bars, 1, 0, style);
            Assert.Null(result[0].PP);
            Assert.Equal(expected[(int)style], result[1]);
            Assert.Equal(result, PivotLevelSnapshots.Rolling(bars, 1, 0, style));
        }
        Assert.Equal(
            9.75,
            PivotLevelSnapshots
                .Calendar(bars, PivotCalendarWindow.Day, PivotLevelStyle.Demark)[1]
                .PP
        );
        Assert.Equal(
            10.5,
            PivotLevelSnapshots
                .Calendar(bars, PivotCalendarWindow.Day, PivotLevelStyle.Woodie)[1]
                .PP
        );
    }

    [Fact]
    public void SparseCalendarWindowsNeverMergeDistinctDates()
    {
        var data = CompetitorData.FromOhlc(
            [8, 12, 10, 11, 9],
            [12, 14, 13, 15, 14],
            [6, 10, 8, 9, 7],
            [9, 13, 11, 10, 8]
        );
        var dates = new[]
        {
            new DateTime(2020, 1, 1),
            new DateTime(2020, 2, 1),
            new DateTime(2021, 2, 1),
            new DateTime(2021, 2, 2),
            new DateTime(2022, 2, 2),
        };
        for (var i = 0; i < dates.Length; i++)
        {
            data.Dates[i] = dates[i];
            data.Quotes[i].Date = dates[i];
            var b = data.IndicatorBars[i];
            data.IndicatorBars[i] = new Bar(dates[i], b.Open, b.High, b.Low, b.Close, b.Volume);
        }
        foreach (var window in Enum.GetValues<PivotCalendarWindow>())
        foreach (var style in Enum.GetValues<PivotLevelStyle>())
            ComparisonVerifier.Check(
                PivotLevelComparison.Pair(true, style, window: window),
                data,
                3
            );
        var native = PivotLevelComparison.Native(
            data,
            3,
            0,
            PivotLevelStyle.Standard,
            true,
            PivotCalendarWindow.Hour
        );
        Assert.All(native.Outputs["PP"].Present!, v => Assert.False(v));
        var owned = PivotLevelSnapshots.Calendar(data.IndicatorBars, PivotCalendarWindow.Hour);
        Assert.All(owned.Skip(1), r => Assert.NotNull(r.PP));
    }

    [Fact]
    public void InvalidParametersHugeLookbacksAndNativeOverflowAreExplicit()
    {
        Assert.Throws<ArgumentNullException>(() => PivotLevelSnapshots.Rolling(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotLevelSnapshots.Rolling([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PivotLevelSnapshots.Rolling([], offset: -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PivotLevelSnapshots.Rolling([], style: (PivotLevelStyle)5)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PivotLevelSnapshots.Calendar([], window: (PivotCalendarWindow)4)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PivotLevelSnapshots.Calendar([new Bar(DateTime.UnixEpoch, double.NaN, 1, 1, 1, 0)])
        );
        var data = CompetitorData.Create(4);
        Assert.All(
            PivotLevelSnapshots.Rolling(data.IndicatorBars, int.MaxValue, int.MaxValue),
            r => Assert.Null(r.PP)
        );
        Assert.Throws<OverflowException>(() =>
            data.Quotes.GetRollingPivots(int.MaxValue, int.MaxValue).ToArray()
        );
        var huge = CompetitorData.FromCloses([4e28, 4e28, 4e28]);
        Assert.Throws<OverflowException>(() => huge.Quotes.GetRollingPivots(1, 0).ToArray());
        Assert.All(
            PivotLevelSnapshots.Rolling(huge.IndicatorBars, 1).Skip(1),
            r => Assert.Equal(4e28, r.PP)
        );
    }

    [Fact]
    public void WideAndSubnormalLevelsMatchIndependentRationalFormula()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        foreach (var style in Enum.GetValues<PivotLevelStyle>())
        {
            var bars = Enumerable
                .Range(0, 5)
                .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), scale, scale, scale, scale, 0))
                .ToArray();
            var expected = PivotLevelComparison.ExactLevels(scale, scale, scale, scale, style);
            foreach (var calendar in new[] { false, true })
            {
                var actual = PivotLevelComparison.Owned(
                    bars,
                    1,
                    0,
                    style,
                    calendar,
                    PivotCalendarWindow.Day
                );
                for (var j = 0; j < 9; j++)
                {
                    Assert.Equal(
                        expected[j].HasValue,
                        actual.Outputs[PivotLevelComparison.Names[j]].Present![^1]
                    );
                    if (expected[j].HasValue)
                        Assert.Equal(
                            expected[j]!.Value,
                            actual.Outputs[PivotLevelComparison.Names[j]].Values[^1]
                        );
                }
            }
        }
        var wide = new[]
        {
            new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, 0, 0),
            new Bar(DateTime.UnixEpoch.AddDays(1), 0, 0, 0, 0, 0),
        };
        Assert.Throws<OverflowException>(() => PivotLevelSnapshots.Rolling(wide, 1));
    }

    [Fact]
    public void EveryLevelMaskAndFormulaConfigurationMutationIsDetected()
    {
        var data = CompetitorData.Create(180);
        foreach (var calendar in new[] { false, true })
        {
            var pair = PivotLevelComparison.Pair(calendar, PivotLevelStyle.Camarilla);
            foreach (var name in PivotLevelComparison.Names)
            foreach (var native in new[] { false, true })
            foreach (var mask in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData d, int p)
                {
                    var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                    var output = result.Outputs[name];
                    Assert.True(output.Present![^1]);
                    if (mask)
                        output.Present[^1] = false;
                    else
                        output.Values[^1] += 1;
                    return result;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = PivotLevelComparison
                            .Pair(calendar, PivotLevelStyle.Standard)
                            .Library,
                    },
                    data,
                    3
                )
            );
        }
        var rolling = PivotLevelComparison.Pair(false);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                rolling with
                {
                    Library = PivotLevelComparison.Pair(false, offset: 2).Library,
                },
                data,
                3
            )
        );
    }
}
