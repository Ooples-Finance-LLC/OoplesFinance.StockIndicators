using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TradyAdaptiveComparisonTests
{
    [Fact]
    public async Task ConventionalFlatContinuationHasIndependentLifecycleContract()
    {
        foreach (var c in new[] { (1, 2, 30), (3, 2, 9), (2, 7, 3) })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(SeededAdaptiveAverage),
                    "Trady adaptive counterpart",
                    () => new SeededAdaptiveAverage(c.Item1, c.Item2, c.Item3, resetFlat: false)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void EveryConfigurationChecksDecimalStagesAndExpectedFlatFailures()
    {
        foreach (var c in new[] { (1, 2, 30), (2, 2, 30), (5, 2, 9), (10, 3, 30) })
        {
            var pair = TradyAdaptiveComparison.Pair(c.Item2, c.Item3);
            foreach (var count in new[] { 0, 1, c.Item1, c.Item1 + 1, 90 }.Distinct())
            {
                TradyAdaptiveComparison.Check(pair, CompetitorData.Create(count), c.Item1);
                foreach (var shape in ComparisonVerifier.Shapes)
                    TradyAdaptiveComparison.Check(
                        pair,
                        ComparisonVerifier.Fixture(shape, count),
                        c.Item1
                    );
            }
        }
    }

    [Fact]
    public void TupleNullableGenericAndArbitraryIndexRoutesAgree()
    {
        var d = CompetitorData.Create(40);
        var prices = d.Candles.Select(c => c.Close).ToArray();
        var tuple = new Trady.Analysis.Indicator.KaufmanAdaptiveMovingAverageByTuple(
            prices,
            3,
            2,
            9
        );
        var generic = new Trady.Analysis.Indicator.KaufmanAdaptiveMovingAverage<decimal, decimal?>(
            prices,
            v => v,
            3,
            2,
            9
        );
        var candle = new Trady.Analysis.Indicator.KaufmanAdaptiveMovingAverage(d.Candles, 3, 2, 9);
        var reference = TradyAdaptiveComparison.DecimalReference(
            prices.Select(v => (decimal?)v).ToArray(),
            3,
            2,
            9
        );
        Assert.Equal(reference, tuple.Compute());
        Assert.Equal(reference, generic.Compute());
        Assert.Equal(reference, candle.Compute().Select(r => r.Tick));
        Assert.Equal(reference.Skip(7).Take(12), tuple.Compute(startIndex: 7, endIndex: 18));
        IEnumerable<int> indices = new[] { 18, 4, 2, 7, 0, 30 };
        Assert.Equal(indices.Select(i => reference[i]), generic.Compute(indices));
        Assert.Equal(reference[18], tuple.Compute(new[] { 18, 7 }));
        var n = tuple.ComputeNeighbour(8);
        Assert.Equal(reference[7], n.Prev);
        Assert.Equal(reference[8], n.Current);
        Assert.Equal(reference[9], n.Next);
        decimal?[] missing = [1, 2, null, 4, 5, 6];
        var nullable = new Trady.Analysis.Indicator.KaufmanAdaptiveMovingAverage<
            decimal?,
            decimal?
        >(missing, v => v, 2, 2, 9);
        Assert.Throws<InvalidOperationException>(() => nullable.Compute());
        Assert.Throws<InvalidOperationException>(() =>
            TradyAdaptiveComparison.DecimalReference(missing, 2, 2, 9)
        );
    }

    [Fact]
    public void FlatWindowNativeFailureCannotBeSilentlyReplacedWithOutput()
    {
        var d = CompetitorData.FromCloses([0, 10, 0, 0, 0, 0]);
        var pair = TradyAdaptiveComparison.Pair();
        var error = Assert.Throws<InvalidOperationException>(() => pair.Competitor(d, 2));
        Assert.Equal("Nullable object must have a value.", error.Message);
        var r = pair.Ooples(d, 2).Outputs["Kama"];
        Assert.True(r.Values[4] > 0);
        Assert.True(r.Values[5] < r.Values[4]);
        Assert.Throws<InvalidOperationException>(() =>
            TradyAdaptiveComparison.Check(pair with { Competitor = pair.Ooples }, d, 2)
        );
        Assert.Throws<InvalidOperationException>(() =>
            TradyAdaptiveComparison.Check(
                pair with
                {
                    Library = (data, period) =>
                        new ComparisonSeries(
                            new Dictionary<string, ComparisonOutput>
                            {
                                ["Kama"] = SeededAdaptiveComparison
                                    .Pair(false)
                                    .Ooples(data, period)
                                    .Outputs["Kama"],
                            }
                        ),
                },
                d,
                2
            )
        );
        var seed = CompetitorData.FromCloses([3, 7]);
        Assert.Equal(
            new decimal?[] { null, 7 },
            TradyAdaptiveComparison.DecimalReference([3, 7], 2, 2, 30)
        );
        ComparisonVerifier.Check(pair, seed, 2);
    }

    [Fact]
    public async Task ChainingOutputMasksAndCoefficientMutationsAreVerified()
    {
        var d = CompetitorData.Create(60);
        var indicator = new SeededAdaptiveAverage(3, 2, 9, resetFlat: false);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = SeededAdaptiveComparison.ReferenceValues(
            FixedWeightedComparison.Stage(d.Closes, 3, false),
            3,
            2,
            9,
            false,
            false,
            false
        );
        for (var j = 0; j < 2; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 2]].ToArray()
            );
        }
        var pair = TradyAdaptiveComparison.Pair(2, 9);
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int p)
            {
                var r = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                if (presence)
                    r.Outputs["Kama"].Present![^1] = false;
                else
                    r.Outputs["Kama"].Values[^1] += 1;
                return r;
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
                    Library = TradyAdaptiveComparison.Pair(1, 9).Ooples,
                },
                d,
                3
            )
        );
    }
}
