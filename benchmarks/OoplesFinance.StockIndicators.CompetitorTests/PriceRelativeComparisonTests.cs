using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PriceRelativeComparisonTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public async Task RationalLifecycleAndLazyPeriods(int? period, int? mean)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(PriceRelativeStrength),
                "price relative",
                () => new PriceRelativeStrength(period, mean)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void AllOutputsAndOptionsHaveIndependentReferences()
    {
        foreach (var enabled in new[] { false, true })
        foreach (var mean in new int?[] { null, 1, 3, 20 })
        foreach (var p in new[] { 1, 2, 7, 20 })
        {
            var pair = PriceRelativeComparison.Create(enabled, mean);
            ComparisonVerifier.Check(pair, CompetitorData.Create(80), p);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 60), p);
        }
    }

    [Fact]
    public void KnownRatiosReturnDifferencesAndMissingMeanRecovery()
    {
        var data = CompetitorData.FromBodies([2, 4, 0, 8, 10, 12], [4, 12, 16, 32, 50, 72]);
        var rows = PriceRelativeComparison.Owned(data.IndicatorBars, 1, 2).Outputs;
        Assert.Equal(new[] { 2d, 3, double.NaN, 4, 5, 6 }, rows["Prs"].Values);
        Assert.Equal(new[] { false, true, false, false, true, true }, rows["PrsSma"].Present);
        Assert.Equal(new[] { double.NaN, 2.5, double.NaN, double.NaN, 4.5, 5.5 }, rows["PrsSma"].Values);
        Assert.Equal(1, rows["PrsPercent"].Values[1]);
        Assert.False(rows["PrsPercent"].Present![3]);
        var reversed = PriceRelativeComparison.Owned(
            data.IndicatorBars,
            null,
            null,
            CandlePriceField.Open,
            CandlePriceField.Close
        );
        Assert.Equal(.5, reversed.Outputs["Prs"].Values[0]);
    }

    [Fact]
    public void HugeReturnTermsCancelBeforeFinalRoundingAndOverflowIsRejected()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, double.Epsilon, 1, 1, double.Epsilon, 0),
            new Bar(DateTime.UnixEpoch.AddDays(1), double.MaxValue, 1, 1, double.MaxValue, 0),
        };
        var result = PriceRelativeComparison.Owned(bars, 1, 2).Outputs;
        Assert.Equal(0, result["PrsPercent"].Values[1]);
        Assert.True(result["PrsPercent"].Present![1]);
        Assert.Equal(1, result["PrsSma"].Values[1]);
        var tuple = bars.Select(b => (b.Time, b.Close)).ToArray();
        Assert.Null(tuple.GetPrs(tuple, 1, 2).Last().PrsPercent);
        Assert.Throws<IndicatorOutputException>(() =>
            PriceRelativeComparison.Owned(
                [new Bar(DateTime.UnixEpoch, double.Epsilon, 1, 1, double.MaxValue, 0)],
                null,
                null
            )
        );
    }

    [Fact]
    public void NativeRoutesSortingDefaultsAlignmentAndParameterFailures()
    {
        var data = CompetitorData.Create(60);
        var basis = data
            .Quotes.Select(q => new Quote
            {
                Date = q.Date,
                Open = q.Open,
                High = q.High,
                Low = q.Low,
                Close = q.Open,
                Volume = q.Volume,
            })
            .ToArray();
        var expected = PriceRelativeComparison.FromNative(data.Quotes.GetPrs(basis, 3, 4));
        var tuples = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var baseTuples = basis.Select(q => (q.Date, (double)q.Close)).ToArray();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetPrs(baseTuples.Reverse(), 3, 4),
                data.Quotes.AsEnumerable().Reverse().GetPrs(basis.Reverse(), 3, 4),
                data.Quotes.GetSma(1).GetPrs(basis.GetSma(1), 3, 4),
            }
        )
            ComparisonVerifier.Compare(
                expected,
                PriceRelativeComparison.FromNative(rows),
                "PRS routes",
                IndicatorErrorBudget.Exact
            );
        ComparisonVerifier.Compare(
            PriceRelativeComparison.FromNative(
                data.Dates.Zip(data.Closes, (t, v) => (t, v))
                    .GetPrs(data.Dates.Zip(data.Opens, (t, v) => (t, v)))
            ),
            PriceRelativeComparison.Create(false, null).Ooples(data, 3),
            "PRS default",
            IndicatorErrorBudget.Exact
        );
        var smooth = data.Quotes.GetSma(3).ToArray();
        var smoothBase = basis.GetSma(3).ToArray();
        ComparisonVerifier.Compare(
            PriceRelativeComparison.Series(
                PriceRelativeComparison.Reference(
                    smooth.Skip(2).Select(v => v.Sma!.Value).ToArray(),
                    smoothBase.Skip(2).Select(v => v.Sma!.Value).ToArray(),
                    3,
                    4,
                    true
                )
            ),
            PriceRelativeComparison.FromNative(smooth.GetPrs(smoothBase, 3, 4).Skip(2)),
            "PRS reusable startup",
            IndicatorErrorBudget.Exact
        );
        Assert.Throws<InvalidQuotesException>(() => tuples.GetPrs(baseTuples.Skip(1)).ToArray());
        Assert.Throws<InvalidQuotesException>(() =>
            tuples.GetPrs(baseTuples.Select(x => (x.Item1.AddHours(1), x.Item2))).ToArray()
        );
        Assert.Throws<InvalidQuotesException>(() => tuples.GetPrs(baseTuples, 61).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => tuples.GetPrs(baseTuples, 0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tuples.GetPrs(baseTuples, null, 0).ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new PriceRelativeStrength(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PriceRelativeStrength(null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriceRelativeStrength(evaluation: (CandlePriceField)999)
        );
    }

    [Fact]
    public async Task ChainingAndEveryOutputPresenceAndConfigurationMutation()
    {
        var data = CompetitorData.Create(45);
        var indicator = new PriceRelativeStrength(3, 4);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = PriceRelativeComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 3, false),
            data.Opens,
            3,
            4,
            false
        );
        for (var j = 0; j < 3; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 3]].ToArray()
            );
        }
        var pair = PriceRelativeComparison.Create(true, 4);
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        foreach (var name in PriceRelativeComparison.Names)
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var r = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    r.Outputs[name].Present![^1] = false;
                else
                    r.Outputs[name].Values[^1] += 1;
                return r;
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
                    Library = PriceRelativeComparison.Create(false, 4).Ooples,
                },
                data,
                3
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = PriceRelativeComparison.Create(true, 5).Ooples,
                },
                data,
                3
            )
        );
    }
}
