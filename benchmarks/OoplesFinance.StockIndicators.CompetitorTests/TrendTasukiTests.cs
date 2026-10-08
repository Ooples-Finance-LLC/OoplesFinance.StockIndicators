using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TrendTasukiTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, TrendTasukiComparison.Cases.Length).Select(i => new object[] { i });

    [Theory, MemberData(nameof(Cases))]
    public void GoldenSignalsPinAsymmetricGapBoundariesAndIgnoredThreshold(int index)
    {
        var item = TrendTasukiComparison.Cases[index];
        foreach (var period in new[] { 1, 3, 5 })
        {
            var data = TrendTasukiComparison.Golden(item, period);
            foreach (var threshold in new[] { -1m, 0m, .1m, 1m, 100m })
            {
                var pair = TrendTasukiComparison.CreatePair(item.Upside, threshold);
                Assert.Equal(
                    item.Expected,
                    pair.Competitor(data, period).Outputs["Value"].Values[^1]
                );
                Assert.Equal(item.Expected, pair.Ooples(data, period).Outputs["Value"].Values[^1]);
                Assert.Equal(
                    item.Expected,
                    pair.Reference!(data, period).Outputs["Value"].Values[^1]
                );
                ComparisonVerifier.Check(pair, data, period);
            }
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void TupleAndGenericRoutesRetainTheSameDefinition(bool upside)
    {
        foreach (var period in new[] { 1, 3, 5 })
        foreach (var threshold in new[] { -1m, .1m, 100m })
        {
            var data = TrendTasukiComparison.Fixture(period);
            var inputs = data.Candles.Select(b => (b.Open, b.High, b.Low, b.Close)).ToArray();
            var tuple = upside
                ? new TC.UpsideTasukiGapByTuple(inputs, period, threshold).Compute()
                : new TC.DownsideTasukiGapByTuple(inputs, period, threshold).Compute();
            var generic = upside
                ? new TC.UpsideTasukiGap<(decimal, decimal, decimal, decimal), bool?>(
                    inputs,
                    x => x,
                    period,
                    threshold
                ).Compute()
                : new TC.DownsideTasukiGap<(decimal, decimal, decimal, decimal), bool?>(
                    inputs,
                    x => x,
                    period,
                    threshold
                ).Compute();
            var expected = TrendTasukiComparison
                .CreatePair(upside, threshold)
                .Competitor(data, period)
                .Outputs["Value"]
                .Values;
            Assert.Equal(
                expected,
                tuple.Select(v =>
                    v.HasValue
                        ? v.Value
                            ? 1d
                            : 0
                        : double.NaN
                )
            );
            Assert.Equal(
                expected,
                generic.Select(v =>
                    v.HasValue
                        ? v.Value
                            ? 1d
                            : 0
                        : double.NaN
                )
            );
            Assert.All(expected.Take(2), v => Assert.True(double.IsNaN(v)));
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task IndependentReferenceLifecycleAndConstantSpaceAtHugePeriod(bool upside)
    {
        foreach (var period in new[] { 1, 3, 7, int.MaxValue })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    TrendTasukiComparison.Create(upside, period).GetType(),
                    "trend",
                    () => TrendTasukiComparison.Create(upside, period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendTasukiComparison.Create(upside, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendTasukiComparison.Create(upside, -1));
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task StrictTrendAndFullRangeGapWithExtremeAndSubnormalPrices(bool upside)
    {
        var item = TrendTasukiComparison.Cases.First(c => c.Upside == upside && c.Expected == 1);
        var input = TrendTasukiComparison.Golden(item, 3).IndicatorBars;
        foreach (var scale in new[] { 1d, double.Epsilon * 8, Math.ScaleB(1d, 1018) })
        {
            var bars = input
                .Select(b => new Bar(
                    b.Time,
                    b.Open * scale,
                    b.High * scale,
                    b.Low * scale,
                    b.Close * scale,
                    1
                ))
                .ToArray();
            Assert.Equal(1, (await Run(upside, bars))[^1]);
            // One equal high interrupts an otherwise valid three-transition trend.
            var original = bars[0];
            bars[0] = new Bar(
                original.Time,
                Math.Min(original.Open, bars[1].High),
                bars[1].High,
                original.Low,
                Math.Min(original.Close, bars[1].High),
                1
            );
            Assert.Equal(0, (await Run(upside, bars))[^1]);
            bars[0] = new Bar(
                original.Time,
                original.Open,
                original.High,
                bars[1].Low,
                original.Close,
                1
            );
            // Preserve valid OHLC when lowering/raising a prefix endpoint.
            bars[0] = new Bar(
                original.Time,
                Math.Max(bars[0].Low, original.Open),
                original.High,
                bars[0].Low,
                Math.Max(bars[0].Low, original.Close),
                1
            );
            Assert.Equal(0, (await Run(upside, bars))[^1]);
        }
        var noGap = input.ToArray();
        var first = noGap[^3];
        var second = noGap[^2];
        noGap[^3] = upside
            ? new Bar(first.Time, first.Open, second.Low, first.Low, first.Close, 1)
            : new Bar(first.Time, first.Open, first.High, second.High, first.Close, 1);
        Assert.Equal(0, (await Run(upside, noGap))[^1]);
    }

    [Fact]
    public void FixtureIsAcceptedByEveryExistingCandleComparison()
    {
        var data = TrendTasukiComparison.Fixture(3);
        foreach (var pair in ComparisonPairs.All.Where(p => p.Id.Contains(".Candle")))
            ComparisonVerifier.Check(pair, data, 3);
    }

    private static async Task<double[]> Run(bool upside, Bar[] bars)
    {
        var indicator = TrendTasukiComparison.Create(upside, 3);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
