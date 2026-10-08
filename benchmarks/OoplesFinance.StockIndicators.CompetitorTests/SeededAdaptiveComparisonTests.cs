using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededAdaptiveComparisonTests
{
    private static Bar[] BarsOf(params double[] v) =>
        v.Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 0)).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentRationalContractsCoverEveryLifecycleAndLargePeriods(bool quan)
    {
        foreach (
            var c in new[] { (1, 2, 30), (3, 2, 9), (2, 7, 3), (2, int.MaxValue, int.MaxValue) }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(SeededAdaptiveAverage),
                    "seeded adaptive",
                    () => new SeededAdaptiveAverage(c.Item1, c.Item2, c.Item3, quan, quan, !quan)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void MaximumPeriodRemainsLazyWithAccurateWarmup()
    {
        var d = CompetitorData.Create(100);
        foreach (var quan in new[] { false, true })
        {
            var indicator = new SeededAdaptiveAverage(int.MaxValue, 2, 30, quan, quan, !quan);
            Assert.Equal(quan ? 0 : int.MaxValue - 1, indicator.WarmupBars);
            ComparisonVerifier.Compare(
                SeededAdaptiveComparison.Reference(d, int.MaxValue, 2, 30, quan, false),
                SeededAdaptiveComparison.Owned(d.IndicatorBars, int.MaxValue, 2, 30, quan),
                "maximum adaptive period",
                IndicatorErrorBudget.Exact
            );
        }
    }

    [Fact]
    public void AllConfigurationsAndStartupValuesMatchIndependentStages()
    {
        foreach (var quan in new[] { false, true })
        foreach (var c in new[] { (1, 2, 30), (2, 2, 30), (5, 2, 9), (10, 3, 30) })
        {
            var pair = SeededAdaptiveComparison.Pair(quan, c.Item2, c.Item3);
            ComparisonVerifier.Check(pair, CompetitorData.Create(80), c.Item1);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 80), c.Item1);
        }
        var data = CompetitorData.FromCloses([0, 10, 0, 0, 0, 0]);
        var skender = SeededAdaptiveComparison.Pair(false).Ooples(data, 2).Outputs;
        var quanValues = SeededAdaptiveComparison.Pair(true).Ooples(data, 2).Outputs["Kama"].Values;
        Assert.Equal(new[] { false, true, true, true, true, true }, skender["Kama"].Present);
        Assert.Equal(new[] { false, false, true, true, true, true }, skender["ER"].Present);
        Assert.Equal(0, skender["Kama"].Values[4]);
        Assert.NotEqual(0, quanValues[4]);
        var single = SeededAdaptiveComparison.Pair(true).Ooples(data, 1).Outputs["Kama"].Values;
        Assert.Equal(data.Closes, single);
    }

    [Fact]
    public void SkenderQuotesTupleReusableAndSortingRoutesAgree()
    {
        var d = CompetitorData.Create(80);
        var quote = d.Quotes.GetKama(3, 2, 9).ToArray();
        var tuple = d.Quotes.Select(q => (q.Date, (double)q.Close)).GetKama(3, 2, 9).ToArray();
        var reusable = d.Quotes.GetSma(1).GetKama(3, 2, 9).ToArray();
        var reversed = d.Quotes.AsEnumerable().Reverse().GetKama(3, 2, 9).ToArray();
        foreach (var rows in new[] { tuple, reversed })
        {
            Assert.Equal(quote.Select(r => r.Kama), rows.Select(r => r.Kama));
            Assert.Equal(quote.Select(r => r.ER), rows.Select(r => r.ER));
        }
        var reusableReference = SeededAdaptiveComparison.ReferenceValues(
            d.Quotes.GetSma(1).Select(r => r.Sma!.Value).ToArray(),
            3,
            2,
            9,
            false,
            true
        );
        Assert.Equal(reusableReference[0], reusable.Select(r => r.Kama));
        Assert.Equal(reusableReference[1], reusable.Select(r => r.ER));
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetKama(0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetKama(3, 0, 9).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetKama(3, 3, 3).ToArray());
        Assert.Throws<OverflowException>(() => d.Quotes.GetKama(3, 2, int.MaxValue).ToArray());
    }

    [Fact]
    public void QuanEventRevisionResetAndFastClampArePinned()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Kama(source, 3, 2, 9);
        var direct = new QuanTAlib.Kama(3, 2, 9);
        double[] values = [1, 4, 2, 7, 3, 8];
        for (var i = 0; i < values.Length; i++)
        {
            var input = new QuanTAlib.TValue(values[i], true, false);
            var expected = direct.Calc(input).Value;
            source.Add(input);
            Assert.Equal(expected, subscribed.Value);
            Assert.Equal(i >= 2, direct.IsHot);
        }
        var revision = new QuanTAlib.TValue(11, false, false);
        var revised = direct.Calc(revision).Value;
        source.Add(revision);
        var replay = new QuanTAlib.Kama(3, 2, 9);
        foreach (var value in values.Take(5).Append(11))
            replay.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(replay.Value, revised);
        Assert.Equal(revised, subscribed.Value);
        direct.Init();
        Assert.Equal(17, direct.Calc(new QuanTAlib.TValue(17, true, false)).Value);
        var unclamped = new QuanTAlib.Kama(2, 8, 30);
        var clamped = new QuanTAlib.Kama(2, 2, 30);
        foreach (var value in values)
            Assert.Equal(
                clamped.Calc(new QuanTAlib.TValue(value, true, false)).Value,
                unclamped.Calc(new QuanTAlib.TValue(value, true, false)).Value
            );
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Kama(0));
    }

    [Fact]
    public void FiniteConvexResultsSurviveExtremeDifferencesAndPreserveSubnormals()
    {
        foreach (var quan in new[] { false, true })
        foreach (
            var values in new[]
            {
                new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, -double.MaxValue },
                new[] { double.Epsilon, 0, double.Epsilon, 0, double.Epsilon },
            }
        )
        {
            var r = SeededAdaptiveComparison.Owned(BarsOf(values), 2, 2, 30, quan).Outputs["Kama"];
            Assert.All(
                r.Values.Where((_, i) => r.Present![i]),
                v => Assert.True(double.IsFinite(v))
            );
        }
        var same = SeededAdaptiveComparison.Owned(
            BarsOf(double.MaxValue, double.MaxValue, double.MaxValue),
            2,
            2,
            30,
            true
        );
        Assert.All(same.Outputs["Kama"].Values, v => Assert.Equal(double.MaxValue, v));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededAdaptiveAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededAdaptiveAverage(fastPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededAdaptiveAverage(slowPeriod: 0));
    }

    [Fact]
    public async Task ChainingAndAllValuePresenceCoefficientMutationsAreChecked()
    {
        var d = CompetitorData.Create(70);
        var values = FixedWeightedComparison.Stage(d.Closes, 3, false);
        foreach (var quan in new[] { false, true })
        {
            var indicator = new SeededAdaptiveAverage(3, 2, 9, quan, quan, !quan);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(d.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var r = SeededAdaptiveComparison.ReferenceValues(values, 3, 2, 9, quan, false);
            for (var j = 0; j < 2; j++)
            {
                Assert.Equal(r[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
                Assert.Equal(
                    r[j].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[j + 2]].ToArray()
                );
            }
            var pair = SeededAdaptiveComparison.Pair(quan, 2, 9);
            foreach (var name in quan ? new[] { "Kama" } : new[] { "Kama", "ER" })
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int p)
                {
                    var output = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                    if (presence)
                        output.Outputs[name].Present![^1] = false;
                    else
                        output.Outputs[name].Values[^1] += 1;
                    return output;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        d,
                        3
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = SeededAdaptiveComparison.Pair(quan, 1, 9).Ooples,
                    },
                    d,
                    3
                )
            );
        }
    }
}
