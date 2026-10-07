using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TrendBabyTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable
            .Range(0, TrendBabyComparison.Cases.Length)
            .SelectMany(i => new[] { new object[] { i, true }, new object[] { i, false } });

    [Theory, MemberData(nameof(Cases))]
    public void GoldenGapsColorsPercentilesAndDojiBoundaries(int index, bool bullish)
    {
        var item = TrendBabyComparison.Cases[index];
        foreach (var trend in new[] { 1, 3, 5 })
        {
            var data = TrendBabyComparison.Golden(item, bullish, trend);
            var pair = TrendBabyComparison.Pair(bullish);
            Assert.Equal(item.Expected, pair.Ooples(data, trend).Outputs["Value"].Values[^1]);
            Assert.Equal(item.Expected, pair.Competitor(data, trend).Outputs["Value"].Values[^1]);
            Assert.Equal(item.Expected, pair.Reference!(data, trend).Outputs["Value"].Values[^1]);
            ComparisonVerifier.Check(pair, data, trend);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void ConfigurableBullishPercentileAndDojiWithFixedBearishWindow(bool bullish)
    {
        foreach (var period in new[] { 1, 3, 20, 30 })
        foreach (var percentile in new[] { 0m, .25m, .75m, 1m })
        foreach (var fraction in new[] { 0m, .1m, .25m, 1m })
        {
            var data = TrendBabyComparison.Golden(TrendBabyComparison.Cases[4], bullish, 3, period);
            ComparisonVerifier.Check(
                TrendBabyComparison.Pair(bullish, period, percentile, fraction),
                data,
                3
            );
        }
        // A short requested period cannot accelerate Trady's bearish component.
        var all = TrendBabyComparison.Golden(TrendBabyComparison.Cases[0], bullish).IndicatorBars;
        var bars = all.TakeLast(6).ToArray();
        var shortData = CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
        var pair = TrendBabyComparison.Pair(bullish, 1, 0m);
        Assert.Equal(0, pair.Competitor(shortData, 1).Outputs["Value"].Values[^1]);
        ComparisonVerifier.Check(pair, shortData, 1);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void TupleAndGenericRoutesHaveTheSameArguments(bool bullish)
    {
        var data = TrendBabyComparison.Golden(TrendBabyComparison.Cases[0], bullish);
        var input = data.Candles.Select(b => (b.Open, b.High, b.Low, b.Close)).ToArray();
        foreach (var period in new[] { 1, 20 })
        {
            var tuple = bullish
                ? new TC.BullishAbandonedBabyByTuple(input, 3, period, .25m, .25m).Compute()
                : new TC.BearishAbandonedBabyByTuple(input, 3, period, .25m, .25m).Compute();
            var generic = bullish
                ? new TC.BullishAbandonedBaby<(decimal, decimal, decimal, decimal), bool?>(
                    input,
                    x => x,
                    3,
                    period,
                    .25m,
                    .25m
                ).Compute()
                : new TC.BearishAbandonedBaby<(decimal, decimal, decimal, decimal), bool?>(
                    input,
                    x => x,
                    3,
                    period,
                    .25m,
                    .25m
                ).Compute();
            var expected = TrendBabyComparison
                .Pair(bullish, period, .25m, .25m)
                .Competitor(data, 3)
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
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task IndependentFormulaLifecycleAndLazyHugePeriods(bool bullish)
    {
        foreach (
            var (trend, period) in new[] { (1, 1), (3, 7), (5, 20), (int.MaxValue, int.MaxValue) }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    TrendBabyComparison.Create(bullish).GetType(),
                    "periods",
                    () => TrendBabyComparison.Create(bullish, trend, period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendBabyComparison.Create(bullish, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendBabyComparison.Create(bullish, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendBabyComparison.Create(bullish, 3, 20, -.1m)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendBabyComparison.Create(bullish, 3, 20, 1.1m)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendBabyComparison.Create(bullish, 3, 20, .75m, -.1m)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendBabyComparison.Create(bullish, 3, 20, .75m, 1.1m)
        );
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ExtremeSubnormalAndEqualTrendEndpoints(bool bullish)
    {
        var data = TrendBabyComparison.Golden(TrendBabyComparison.Cases[0], bullish);
        foreach (var scale in new[] { 1d, 8 * double.Epsilon, Math.ScaleB(1d, 1015) })
        {
            var bars = data
                .IndicatorBars.Select(b => new Bar(
                    b.Time,
                    b.Open * scale,
                    b.High * scale,
                    b.Low * scale,
                    b.Close * scale,
                    1
                ))
                .ToArray();
            Assert.Equal(1, (await Run(bullish, bars))[^1]);
            var a = bars[^3];
            var previous = bars[^4];
            bars[^3] = new Bar(
                a.Time,
                Math.Min(a.Open, previous.High),
                previous.High,
                a.Low,
                Math.Min(a.Close, previous.High),
                1
            );
            Assert.Equal(0, (await Run(bullish, bars))[^1]);
        }
    }

    [Fact]
    public void FixtureFitsAllCandleComparisons()
    {
        var data = TrendBabyComparison.Fixture(3);
        foreach (var pair in ComparisonPairs.All.Where(p => p.Id.Contains(".Candle")))
            ComparisonVerifier.Check(pair, data, 3);
    }

    private static async Task<double[]> Run(bool bullish, Bar[] bars)
    {
        var indicator = TrendBabyComparison.Create(bullish);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
