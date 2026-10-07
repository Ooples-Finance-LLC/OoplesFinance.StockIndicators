using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TrendMethodsTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable
            .Range(0, TrendMethodsComparison.Cases.Length)
            .SelectMany(i => new[] { new object[] { i, true }, new object[] { i, false } });

    [Theory, MemberData(nameof(Cases))]
    public void GoldenVariableLengthSearchAndStrictEndpoints(int index, bool rising)
    {
        var item = TrendMethodsComparison.Cases[index];
        foreach (var trend in new[] { 1, 3, 5 })
        {
            var data = TrendMethodsComparison.Golden(item, rising, trend);
            var pair = TrendMethodsComparison.Pair(rising);
            Assert.Equal(item.Expected, pair.Ooples(data, trend).Outputs["Value"].Values[^1]);
            Assert.Equal(item.Expected, pair.Competitor(data, trend).Outputs["Value"].Values[^1]);
            Assert.Equal(item.Expected, pair.Reference!(data, trend).Outputs["Value"].Values[^1]);
            ComparisonVerifier.Check(pair, data, trend);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void OverlappingPercentilesAndRejectedAnchorsMatchIndependentBackwardScan(bool rising)
    {
        foreach (var period in new[] { 2, 3, 7, 20 })
        foreach (var shortQ in new[] { 0m, .25m, .75m, 1m })
        foreach (var longQ in new[] { 0m, .25m, .75m, 1m })
        {
            var pair = TrendMethodsComparison.Pair(rising, period, shortQ, longQ);
            ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture("walk", 80), 3);
            var data = TrendMethodsComparison.Golden(
                TrendMethodsComparison.Cases[2],
                rising,
                3,
                period
            );
            ComparisonVerifier.Check(pair, data, 3);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void TupleGenericRoutesAndFirstBarAbsence(bool rising)
    {
        var data = TrendMethodsComparison.Golden(TrendMethodsComparison.Cases[2], rising);
        var input = data.Candles.Select(b => (b.Open, b.High, b.Low, b.Close)).ToArray();
        foreach (var period in new[] { 3, 20 })
        {
            var tuple = rising
                ? new TC.RisingThreeMethodsByTuple(input, 3, period, .75m, .25m).Compute()
                : new TC.FallingThreeMethodsByTuple(input, 3, period, .75m, .25m).Compute();
            var generic = rising
                ? new TC.RisingThreeMethods<(decimal, decimal, decimal, decimal), bool?>(
                    input,
                    x => x,
                    3,
                    period,
                    .75m,
                    .25m
                ).Compute()
                : new TC.FallingThreeMethods<(decimal, decimal, decimal, decimal), bool?>(
                    input,
                    x => x,
                    3,
                    period,
                    .75m,
                    .25m
                ).Compute();
            var expected = TrendMethodsComparison
                .Pair(rising, period, .75m, .25m)
                .Competitor(data, 3)
                .Outputs["Value"]
                .Values;
            Assert.True(double.IsNaN(expected[0]));
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
    public async Task IndependentReferenceLifecycleAndHugePeriods(bool rising)
    {
        foreach (
            var (trend, period, shortQ, longQ) in new[]
            {
                (1, 1, .25m, .75m),
                (3, 7, 1m, 0m),
                (5, 20, .25m, .75m),
                (int.MaxValue, int.MaxValue, .25m, .75m),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    TrendMethodsComparison.Create(rising).GetType(),
                    "windows",
                    () => TrendMethodsComparison.Create(rising, trend, period, shortQ, longQ)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => TrendMethodsComparison.Create(rising, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrendMethodsComparison.Create(rising, 3, 0)
        );
        foreach (var q in new[] { -.1m, 1.1m })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrendMethodsComparison.Create(rising, 3, 20, q)
            );
            if (rising)
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    TrendMethodsComparison.Create(true, 3, 20, .25m, q)
                );
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ExactExtremeAndSubnormalLengths(bool rising)
    {
        var data = TrendMethodsComparison.Golden(TrendMethodsComparison.Cases[2], rising);
        foreach (var scale in new[] { 8 * double.Epsilon, Math.ScaleB(1d, 1015) })
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
            var indicator = TrendMethodsComparison.Create(rising);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            Assert.Equal(1, run[indicator.Outputs[0]].ToArray()[^1]);
        }
    }

    [Fact]
    public void FixtureFitsExistingCandlePairs()
    {
        var data = TrendMethodsComparison.Fixture(3);
        foreach (var pair in ComparisonPairs.All.Where(p => p.Id.Contains(".Candle")))
            ComparisonVerifier.Check(pair, data, 3);
    }
}
