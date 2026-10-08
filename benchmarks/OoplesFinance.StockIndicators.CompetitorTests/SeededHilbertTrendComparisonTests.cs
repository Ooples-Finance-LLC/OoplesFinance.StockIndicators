using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededHilbertTrendComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RationalContractsCoverAllOutputsAndLifecycle(bool midpoint)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(SeededHilbertTrendline),
                "early Hilbert trendline",
                () => new SeededHilbertTrendline(midpoint)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void StartupAndAllNativeRoutesAreExplicit()
    {
        var data = CompetitorData.Create(160);
        foreach (var midpoint in new[] { false, true })
            ComparisonVerifier.Check(SeededHilbertTrendComparison.Pair(midpoint), data, 20);
        var flat = SeededHilbertTrendComparison.Owned(
            ComparisonVerifier.Fixture("constant", 20).CloseBars,
            false
        );
        Assert.All(flat.Outputs["Trendline"].Present!, v => Assert.True(v));
        Assert.Equal(
            Enumerable.Range(0, 20).Select(i => i >= 6),
            flat.Outputs["SmoothPrice"].Present!
        );
        Assert.Equal(
            Enumerable.Range(0, 20).Select(i => i >= 7),
            flat.Outputs["DcPeriods"].Present!
        );
        var tuples = data.Closes.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v)).ToArray();
        var expected = tuples.GetHtTrendline().ToArray();
        var reversed = tuples.Reverse().GetHtTrendline().ToArray();
        Assert.Equal(expected.Select(v => v.Trendline), reversed.Select(v => v.Trendline));
        var reusable = tuples
            .Select(t => new SmaResult(t.Item1) { Sma = t.v })
            .Cast<IReusableResult>()
            .GetHtTrendline()
            .ToArray();
        Assert.Equal(expected.Select(v => v.Trendline), reusable.Select(v => v.Trendline));
        Assert.Equal(expected.Select(v => v.SmoothPrice), reusable.Select(v => v.SmoothPrice));
        Assert.Equal(expected.Select(v => v.DcPeriods), reusable.Select(v => v.DcPeriods));
    }

    [Fact]
    public void WideMidpointsMeansAndSubnormalsHaveFiniteOwnedOutputs()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200, 0d })
        foreach (var alternate in new[] { false, true })
        {
            var prices = Enumerable
                .Range(0, 150)
                .Select(i => alternate && i % 2 != 0 ? -scale : scale)
                .ToArray();
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            var expected = SeededHilbertTrendComparison.Reference(prices, false);
            Assert.All(
                expected.SelectMany(r => r).Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            foreach (var midpoint in new[] { false, true })
                ComparisonVerifier.Compare(
                    SeededHilbertTrendComparison.Series(expected),
                    SeededHilbertTrendComparison.Owned(bars, midpoint),
                    "wide early trend",
                    IndicatorErrorBudget.Exact
                );
            var native = prices
                .Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
                .GetHtTrendline()
                .ToArray();
            var nativeExpected = SeededHilbertTrendComparison.Reference(prices, true);
            Assert.Equal(nativeExpected[0], native.Select(v => v.Trendline));
            Assert.Equal(nativeExpected[1], native.Select(v => v.SmoothPrice));
            Assert.Equal(nativeExpected[2], native.Select(v => (double?)v.DcPeriods));
        }
    }

    [Fact]
    public async Task ChainingAndEveryOutputMaskAndInputMutationAreDetected()
    {
        var data = CompetitorData.Create(150);
        var owner = new SeededHilbertTrendline(false);
        owner.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(owner)
            .BuildAsync();
        var expected = SeededHilbertTrendComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 3, false),
            false
        );
        for (var j = 0; j < 3; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[owner.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[owner.Outputs[j + 3]].ToArray()
            );
        }
        var pair = SeededHilbertTrendComparison.Pair();
        foreach (var name in SeededHilbertTrendComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (mask)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    20
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = SeededHilbertTrendComparison.Pair(false).Library,
                },
                data,
                20
            )
        );
    }
}
