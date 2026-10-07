using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededPhaseComparisonTests
{
    [Fact]
    public void NativeNonfiniteStagesRemainVisibleOnExtremeFiniteInputs()
    {
        var prices = Enumerable.Repeat(double.MaxValue, 20).ToArray();
        var skender = prices
            .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
            .GetMama()
            .ToArray();
        var skenderExpected = SeededPhaseComparison.NativeReference(prices, .5, .05, false);
        Assert.Equal(skenderExpected[0], skender.Select(v => v.Mama));
        Assert.Equal(skenderExpected[1], skender.Select(v => v.Fama));
        Assert.Contains(skender, v => v.Mama.HasValue && double.IsInfinity(v.Mama.Value));
        var quan = new QuanTAlib.Mama();
        var expected = SeededPhaseComparison.NativeReference(prices, .5, .05, true);
        for (var i = 0; i < prices.Length; i++)
        {
            Assert.Equal(
                expected[0][i],
                quan.Calc(new QuanTAlib.TValue(prices[i], true, false)).Value
            );
            Assert.Equal(expected[1][i], quan.Fama.Value);
        }
        Assert.Contains(expected[0], v => v.HasValue && !double.IsFinite(v.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentRationalContractCoversLifecycleAndBoundedHistory(bool quan)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(SeededPhaseAdaptiveAverage),
                "seeded phase average",
                () => new SeededPhaseAdaptiveAverage(.5, .05, quan, quan, !quan)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryOutputStartupAndPhaseFallbackMatchesIndependentReferences()
    {
        foreach (var quan in new[] { false, true })
        foreach (var c in new[] { (.5, .05), (.8, .1), (.2, .01) })
        {
            var pair = SeededPhaseComparison.Pair(quan, c.Item1, c.Item2);
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 90), 20);
        }
        var d = CompetitorData.FromCloses([1, 2, 3, 4, 5, 6, 7, 8]);
        var full = SeededPhaseComparison.Owned(d.IndicatorBars, .5, .05, false).Outputs;
        var expanding = SeededPhaseComparison.Owned(d.IndicatorBars, .5, .05, true).Outputs;
        foreach (var name in SeededPhaseComparison.Names)
        {
            Assert.Equal(
                new[] { false, false, false, false, false, true, true, true },
                full[name].Present
            );
            Assert.Equal(3.5, full[name].Values[5]);
            Assert.Equal(new[] { 1d, 1.5, 2, 2.5, 3, 3.5 }, expanding[name].Values.Take(6));
        }
    }

    [Fact]
    public void SkenderHl2TupleReusableAndSortingRoutesAreExplicit()
    {
        var d = CompetitorData.Create(100);
        var input = SeededPhaseComparison.Input(d, false, true);
        var native = d.Quotes.GetMama().ToArray();
        var tuple = d.Quotes.Select((q, i) => (q.Date, input[i])).GetMama().ToArray();
        var reverse = d.Quotes.AsEnumerable().Reverse().GetMama().ToArray();
        Assert.Equal(native.Select(v => v.Mama), tuple.Select(v => v.Mama));
        Assert.Equal(native.Select(v => v.Fama), tuple.Select(v => v.Fama));
        Assert.Equal(native.Select(v => v.Mama), reverse.Select(v => v.Mama));
        var reusable = d.Quotes.GetSma(2).ToArray();
        var result = reusable.GetMama().ToArray();
        var defined = reusable.Where(v => v.Sma.HasValue).Select(v => v.Sma!.Value).ToArray();
        var expected = SeededPhaseComparison.NativeReference(defined, .5, .05, false);
        Assert.Null(result[0].Mama);
        Assert.Equal(expected[0], result.Skip(1).Select(v => v.Mama));
        Assert.Equal(expected[1], result.Skip(1).Select(v => v.Fama));
        Assert.NotEqual(input, d.Closes);
    }

    [Fact]
    public void QuanEventRevisionAndIncompleteResetArePinned()
    {
        var d = CompetitorData.Create(40);
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Mama(source);
        var direct = new QuanTAlib.Mama();
        for (var i = 0; i < d.Count; i++)
        {
            var input = new QuanTAlib.TValue(d.Closes[i], true, false);
            var value = direct.Calc(input).Value;
            source.Add(input);
            Assert.Equal(value, subscribed.Value);
            Assert.Equal(direct.Fama.Value, subscribed.Fama.Value);
            Assert.Equal(i >= 5, direct.IsHot);
        }
        var edit = new QuanTAlib.TValue(121, false, false);
        direct.Calc(edit);
        source.Add(edit);
        Assert.Equal(direct.Value, subscribed.Value);
        Assert.Equal(direct.Fama.Value, subscribed.Fama.Value);
        var reference = SeededPhaseComparison.NativeReference(
            d.Closes.Take(39).Append(121).ToArray(),
            .5,
            .05,
            true
        );
        Assert.Equal(reference[0][^1], direct.Value);
        Assert.Equal(reference[1][^1], direct.Fama.Value);
        direct.Init();
        Assert.Equal(0, direct.Fama.Value);
        Assert.NotEqual(17, direct.Calc(new QuanTAlib.TValue(17, true, false)).Value);
    }

    [Fact]
    public void ExtremeFiltersRemainFiniteAndMatchGridStages()
    {
        foreach (var quan in new[] { false, true })
        foreach (
            var prices in new[]
            {
                Enumerable.Repeat(double.MaxValue, 12).ToArray(),
                Enumerable
                    .Range(0, 16)
                    .Select(i => i % 2 == 0 ? double.MaxValue : -double.MaxValue)
                    .ToArray(),
                Enumerable.Range(0, 16).Select(i => (i % 3 - 1) * double.Epsilon).ToArray(),
            }
        )
        {
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            var expected = SeededPhaseComparison.Series(
                SeededPhaseComparison.GridReference(prices, .5, .05, quan)
            );
            ComparisonVerifier.Compare(
                expected,
                SeededPhaseComparison.Owned(bars, .5, .05, quan),
                "extreme phase stages",
                IndicatorErrorBudget.Exact
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededPhaseAdaptiveAverage(double.NaN)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededPhaseAdaptiveAverage(1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededPhaseAdaptiveAverage(slowLimit: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededPhaseAdaptiveAverage(.2, .3));
        var quotes = CompetitorData.Create(12).Quotes;
        Assert.Throws<ArgumentOutOfRangeException>(() => quotes.GetMama(1, .1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => quotes.GetMama(.1, .1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => quotes.GetMama(.5, 0).ToArray());
    }

    [Fact]
    public async Task ChainingAndBothOutputsMasksAndCoefficientMutationsAreChecked()
    {
        var d = CompetitorData.Create(80);
        var prices = FixedWeightedComparison.Stage(d.Closes, 3, false);
        var indicator = new SeededPhaseAdaptiveAverage();
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = SeededPhaseComparison.GridReference(prices, .5, .05, false);
        for (var j = 0; j < 2; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 2]].ToArray()
            );
        }
        foreach (var quan in new[] { false, true })
        {
            var pair = SeededPhaseComparison.Pair(quan);
            foreach (var name in SeededPhaseComparison.Names)
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int p)
                {
                    var r = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                    if (presence)
                        r.Outputs[name].Present![^1] = false;
                    else
                        r.Outputs[name].Values[^1] += 1;
                    return r;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        d,
                        20
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = SeededPhaseComparison.Pair(quan, .8, .1).Ooples,
                    },
                    d,
                    20
                )
            );
        }
    }
}
