using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SumDirectionalComparisonTests
{
    private static Bar[] Points(params double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public async Task IndependentRationalContractCoversLifecycleAndLookbacks(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(SumSeededDirectionalIndex),
                "sum seeded directional",
                () => new SumSeededDirectionalIndex(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownTrendSeedAndRatingAlignmentCoverEveryOutput()
    {
        var result = SumDirectionalComparison
            .Owned(Points(Enumerable.Range(0, 12).Select(i => (double)i).ToArray()), 3)
            .Outputs;
        Assert.Equal(
            new double[] { 100, 100, 100, 100, 100, 100, 100, 100, 100 },
            result["Pdi"].Values.Skip(3)
        );
        Assert.All(result["Mdi"].Values.Skip(3), v => Assert.Equal(0, v));
        Assert.All(result["Dx"].Values.Skip(3), v => Assert.Equal(100, v));
        Assert.Equal(Enumerable.Range(0, 12).Select(i => i >= 5), result["Adx"].Present);
        Assert.Equal(Enumerable.Range(0, 12).Select(i => i >= 8), result["Adxr"].Present);
        Assert.All(result["Adx"].Values.Skip(5), v => Assert.Equal(100, v));
        Assert.All(result["Adxr"].Values.Skip(8), v => Assert.Equal(100, v));
        var flat = Enumerable
            .Range(0, 12)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 2, 0, 1, 0))
            .ToArray();
        var noDirection = SumDirectionalComparison.Owned(flat, 3).Outputs;
        Assert.All(noDirection["Dx"].Values.Skip(3), v => Assert.Equal(0, v));
        Assert.All(noDirection["Adx"].Values.Skip(5), v => Assert.Equal(0, v));
        var zero = SumDirectionalComparison.Owned(Points(new double[12]), 3);
        Assert.All(zero.Outputs.Values, row => Assert.All(row.Present!, v => Assert.False(v)));
    }

    [Fact]
    public void ZeroRangeSkipsSeedOrUpdateAndRatingNeedsPresentEndpoints()
    {
        var e = double.Epsilon;
        var skipped = SumDirectionalComparison
            .Owned(Points(0, e, e, e, 2 * e, 3 * e, 4 * e, 5 * e), 2)
            .Outputs;
        Assert.False(skipped["Dx"].Present![3]);
        Assert.True(skipped["Dx"].Present![4]);
        Assert.All(skipped["Adx"].Present!, v => Assert.False(v));
        var resumed = SumDirectionalComparison
            .Owned(Points(0, e, 2 * e, 2 * e, 2 * e, 2 * e, 3 * e, 4 * e, 5 * e), 2)
            .Outputs;
        Assert.Equal(100, resumed["Adx"].Values[3]);
        Assert.False(resumed["Adx"].Present![4]);
        Assert.Equal(100, resumed["Adx"].Values[6]);
        Assert.False(resumed["Adxr"].Present![6]);
        Assert.Equal(100, resumed["Adxr"].Values[8]);
        foreach (
            var bars in new[]
            {
                Points(0, e, e, e, 2 * e, 3 * e),
                Points(0, e, 2 * e, 2 * e, 2 * e, 3 * e, 4 * e),
            }
        )
            ComparisonVerifier.Compare(
                SumDirectionalComparison.Series(SumDirectionalComparison.Reference(bars, 2)),
                SumDirectionalComparison.Owned(bars, 2),
                "range gaps",
                IndicatorErrorBudget.Exact
            );
    }

    [Fact]
    public void ExactDirectionTiesAndOversizedSumsHaveFiniteRatios()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, 0),
            new Bar(DateTime.UnixEpoch.AddDays(1), 0, 2 * double.Epsilon, -double.Epsilon, 0, 0),
            new Bar(DateTime.UnixEpoch.AddDays(2), 0, 2 * double.Epsilon, -double.Epsilon, 0, 0),
        };
        var tiny = SumDirectionalComparison.Owned(bars, 2).Outputs;
        Assert.True(tiny["Pdi"].Present![2]);
        Assert.Equal(100d / 3, tiny["Pdi"].Values[2]);
        Assert.Equal(0, tiny["Mdi"].Values[2]);
        Assert.Equal(100, tiny["Dx"].Values[2]);
        var tie = bars.Select(
                (b, i) => new Bar(b.Time, 0, i * double.Epsilon, -i * double.Epsilon, 0, 0)
            )
            .ToArray();
        var tied = SumDirectionalComparison.Owned(tie, 2).Outputs;
        Assert.Equal(0, tied["Pdi"].Values[2]);
        Assert.Equal(0, tied["Mdi"].Values[2]);
        Assert.Equal(0, tied["Dx"].Values[2]);
        var maximum = double.MaxValue;
        var huge = Points(
            -maximum,
            maximum,
            -maximum,
            maximum,
            -maximum,
            maximum,
            -maximum,
            maximum
        );
        var result = SumDirectionalComparison.Owned(huge, 2);
        ComparisonVerifier.Compare(
            SumDirectionalComparison.Series(SumDirectionalComparison.Reference(huge, 2)),
            result,
            "oversized ranges",
            IndicatorErrorBudget.Exact
        );
        Assert.Equal(50, result.Outputs["Pdi"].Values[2]);
        Assert.Equal(50, result.Outputs["Mdi"].Values[2]);
        Assert.All(
            result.Outputs.Values,
            row =>
                Assert.All(
                    row.Values.Where((_, i) => row.Present![i]),
                    v => Assert.True(double.IsFinite(v))
                )
        );
    }

    [Fact]
    public void NativeSortingDefaultsAndLimitsAreVerified()
    {
        var data = CompetitorData.Create(45);
        ComparisonVerifier.Compare(
            SumDirectionalComparison.Pair.Competitor(data, 3),
            SumDirectionalComparison.Native(data.Quotes.AsEnumerable().Reverse().GetAdx(3)),
            "sorted input",
            IndicatorErrorBudget.Exact
        );
        ComparisonVerifier.Compare(
            SumDirectionalComparison.Pair.Competitor(data, 14),
            SumDirectionalComparison.Native(data.Quotes.GetAdx()),
            "default period",
            IndicatorErrorBudget.Exact
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAdx(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SumSeededDirectionalIndex(1));
        ComparisonVerifier.Check(SumDirectionalComparison.Pair, data, int.MaxValue);
        foreach (var period in new[] { 2, 3, 14 })
        {
            ComparisonVerifier.Check(SumDirectionalComparison.Pair, data, period);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(
                    SumDirectionalComparison.Pair,
                    ComparisonVerifier.Fixture(shape, 45),
                    period
                );
        }
        var tiny = CompetitorData.FromOhlc(
            [0, 0, 0],
            [0, 2 * double.Epsilon, 2 * double.Epsilon],
            [0, -double.Epsilon, -double.Epsilon],
            [0, 0, 0]
        );
        Assert.Null(tiny.Quotes.GetAdx(2).Last().Pdi);
        Assert.True(
            SumDirectionalComparison.Owned(tiny.IndicatorBars, 2).Outputs["Pdi"].Present![2]
        );
    }

    [Fact]
    public async Task ChainingKeepsHighLowAndReplacesClose()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        var indicator = new SumSeededDirectionalIndex(3);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = SumDirectionalComparison.Reference(bars, 3);
        for (var j = 0; j < 5; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 5]].ToArray()
            );
        }
        Assert.Same(indicator.PositiveIndicator, indicator.PrimaryOutput);
    }

    [Fact]
    public void EveryValueAndPresenceMutationIsDetected()
    {
        var pair = SumDirectionalComparison.Pair;
        foreach (var name in SumDirectionalComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var output = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    output.Outputs[name].Present![^1] = false;
                else
                    output.Outputs[name].Values[^1] += 1;
                return output;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = (d, p) => SumDirectionalComparison.Owned(d.IndicatorBars, p + 1),
                },
                CompetitorData.Create(30),
                3
            )
        );
    }
}
