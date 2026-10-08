using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class VolatilityFisherComparisonTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(20, true)]
    public async Task FisherRationalContractsCoverLifecycle(int period, bool midpoint)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowFisherTransform),
                "window Fisher",
                () => new WindowFisherTransform(period, midpoint)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void VolatilitySnapshotPreservesRetrospectivePrefixRemovalAndPriorAtr()
    {
        var bars = BarsOf([1, 2, 3, 4, 0, 1, 2]);
        var copy = bars.ToArray();
        var before = VolatilityStopSnapshot.Calculate(bars.Take(4).ToArray(), 2, 1);
        Assert.Equal(2, before[3].Sar);
        Assert.False(before[3].IsStop);
        Assert.False(before[2].IsStop);
        Assert.Null(before[2].Sar);
        var result = VolatilityStopSnapshot.Calculate(bars, 2, 1);
        Assert.All(
            result.Take(5),
            r => Assert.Equal(new VolatilityStopValue(null, null, null, null), r)
        );
        Assert.Equal(new VolatilityStopValue(2.5, 2.5, null, false), result[5]);
        Assert.Equal(new VolatilityStopValue(1.75, 1.75, null, true), result[6]);
        Assert.Equal(result, VolatilityStopSnapshot.Calculate(bars, 2, 1));
        Assert.Equal(copy, bars);
        var reversed = bars.Reverse().ToArray();
        ComparisonVerifier.Compare(
            VolatilityStopComparison.Series(
                VolatilityStopComparison.Reference(reversed, 2, 1, false)
            ),
            VolatilityStopComparison.Owned(reversed, 2, 1),
            "supplied snapshot order",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public void BoundariesHugePeriodsAndNonfiniteInputsAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowFisherTransform(0));
        Assert.Throws<ArgumentNullException>(() => VolatilityStopSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => VolatilityStopSnapshot.Calculate([], 1));
        foreach (var bad in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VolatilityStopSnapshot.Calculate([], multiplier: bad)
            );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VolatilityStopSnapshot.Calculate(BarsOf([double.NaN]))
        );
        var bars = BarsOf([1, 2, 3, 4, 5]);
        Assert.All(VolatilityStopSnapshot.Calculate(bars, int.MaxValue), r => Assert.Null(r.Sar));
        ComparisonVerifier.Compare(
            WindowFisherComparison.Series(
                WindowFisherComparison.Reference([1, 2, 3, 4, 5], int.MaxValue, false)
            ),
            WindowFisherComparison.Owned(bars, int.MaxValue, false),
            "lazy Fisher",
            IndicatorErrorBudget.Exact
        );
        var flat = WindowFisherComparison.Owned(BarsOf([5, 5, 5, 5]), 1, false);
        Assert.Equal(new[] { true, true, true, true }, flat.Outputs["Fisher"].Present!);
        Assert.Equal(new[] { false, true, true, true }, flat.Outputs["Trigger"].Present!);
        Assert.All(flat.Outputs["Fisher"].Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void ExtremeFisherAndRetainedVolatilityOutputsAreVerified()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable.Range(0, 80).Select(i => i % 2 == 0 ? scale : -scale).ToArray();
            var bars = BarsOf(prices);
            var expected = WindowFisherComparison.Reference(prices, 7, false);
            Assert.All(
                expected.SelectMany(r => r).Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            foreach (var midpoint in new[] { false, true })
                ComparisonVerifier.Compare(
                    WindowFisherComparison.Series(expected),
                    WindowFisherComparison.Owned(bars, 7, midpoint),
                    "wide Fisher",
                    IndicatorErrorBudget.Exact
                );
            var native = prices
                .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
                .GetFisherTransform(7)
                .ToArray();
            var nativeExpected = WindowFisherComparison.Reference(prices, 7, true);
            Assert.Equal(nativeExpected[0], native.Select(r => r.Fisher));
            Assert.Equal(nativeExpected[1], native.Select(r => r.Trigger));
            var flat = BarsOf(Enumerable.Repeat(scale, 20).ToArray());
            ComparisonVerifier.Compare(
                VolatilityStopComparison.Series(
                    VolatilityStopComparison.Reference(flat, 3, 3, false)
                ),
                VolatilityStopComparison.Owned(flat, 3, 3),
                "wide flat volatility",
                IndicatorErrorBudget.Exact
            );
        }
        var wide = Enumerable
            .Range(0, 10)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                double.MaxValue,
                double.MaxValue,
                -double.MaxValue,
                double.MaxValue,
                0
            ))
            .ToArray();
        Assert.Throws<OverflowException>(() => VolatilityStopSnapshot.Calculate(wide, 2, 2));
    }

    [Fact]
    public async Task ChainingAndAllOutputsMasksAndConfigurationMutationsAreDetected()
    {
        var data = CompetitorData.Create(180);
        var owner = new WindowFisherTransform(3, false);
        owner.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(owner)
            .BuildAsync();
        var expected = WindowFisherComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 3, false),
            3,
            false
        );
        for (var j = 0; j < 2; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[owner.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[owner.Outputs[j + 2]].ToArray()
            );
        }
        foreach (
            var pair in new[] { WindowFisherComparison.Pair(), VolatilityStopComparison.Pair(1) }
        )
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var series = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = series.Outputs[name];
                var index = Array.FindLastIndex(output.Present!, v => v);
                Assert.True(index >= 0);
                if (mask)
                    output.Present![index] = false;
                else
                    output.Values[index] += 1;
                return series;
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
                WindowFisherComparison.Pair() with
                {
                    Library = WindowFisherComparison.Pair(false).Library,
                },
                data,
                3
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                VolatilityStopComparison.Pair() with
                {
                    Library = VolatilityStopComparison.Pair(1).Library,
                },
                data,
                3
            )
        );
    }

    private static Bar[] BarsOf(double[] prices) =>
        prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddDays(i), p, p, p, p, 0)).ToArray();
}
