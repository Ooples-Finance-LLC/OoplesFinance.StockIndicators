using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class HeikinAshiComparisonTests
{
    [Fact]
    public async Task IndependentContractsVerifyRecurrenceLifecycleAndExtremes()
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(HeikinAshiCandles),
                "Heikin-Ashi",
                () => new HeikinAshiCandles()
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void CompleteOhlcvAndFirstCandleAreExplicit()
    {
        var data = HeikinAshiComparison.Fixture();
        ComparisonVerifier.Check(HeikinAshiComparison.Pair, data, 20);
        var result = HeikinAshiComparison.Pair.Ooples(data, 20).Outputs;
        Assert.Equal(new[] { 2.5, 2.5, 5.125, .1875, .09375 }, result["Open"].Values);
        Assert.Equal(new[] { 2.5, 7.75, -4.75, 0, 3 }, result["Close"].Values);
        Assert.Equal(new[] { 4d, 10, 5.125, .1875, 7 }, result["High"].Values);
        Assert.Equal(new[] { 1d, 2.5, -8, 0, -2 }, result["Low"].Values);
        Assert.Equal(data.Volumes, result["Volume"].Values);
        foreach (var output in result.Values)
            Assert.All(output.Present!, v => Assert.True(v));
        var indicator = new HeikinAshiCandles();
        Assert.Same(indicator.Close, indicator.PrimaryOutput);
    }

    [Fact]
    public async Task MaximumAndSubnormalAveragesStayFinite()
    {
        foreach (
            var price in new[]
            {
                double.MaxValue,
                -double.MaxValue,
                double.Epsilon,
                -double.Epsilon,
            }
        )
        {
            var result = await VolumePriceComparisonTests.Run(
                new HeikinAshiCandles(),
                (price, price, price, 1),
                (price, price, price, 2)
            );
            for (var slot = 0; slot < 4; slot++)
                Assert.Equal(new[] { price, price }, result[slot]);
            Assert.Equal(new[] { 1d, 2 }, result[4]);
        }
        var indicator = new HeikinAshiCandles();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    new[]
                    {
                        new Bar(
                            DateTime.UnixEpoch,
                            double.MaxValue,
                            double.MaxValue,
                            -double.MaxValue,
                            -double.MaxValue,
                            7
                        ),
                    }
                )
            )
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(0, run[indicator.Open].ToArray().Single());
        Assert.Equal(0, run[indicator.Close].ToArray().Single());
        Assert.Equal(double.MaxValue, run[indicator.High].ToArray().Single());
        Assert.Equal(-double.MaxValue, run[indicator.Low].ToArray().Single());
        var tiny = new HeikinAshiCandles();
        using var tinyRun = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    new[]
                    {
                        new Bar(DateTime.UnixEpoch, double.Epsilon, 2 * double.Epsilon, 0, 0, 1),
                    }
                )
            )
            .ConfigureIndicators(tiny)
            .BuildAsync();
        Assert.Equal(0, tinyRun[tiny.Open].ToArray().Single());
        Assert.Equal(double.Epsilon, tinyRun[tiny.Close].ToArray().Single());
    }

    [Fact]
    public async Task ChainingReplacesOnlyCloseAndPublishesCloseAsPrimary()
    {
        var data = HeikinAshiComparison.Fixture();
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new HeikinAshiCandles();
        indicator.Of(source);
        var downstream = new PriceCircularTransform(PriceCircularOperation.Sine);
        downstream.Of(indicator);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(source, indicator, downstream)
            .BuildAsync();
        var replacement = run[source.Value].ToArray();
        var expected = HeikinAshiComparison
            .Pair.Ooples(
                CompetitorData.FromOhlcv(
                    data.Opens,
                    data.Highs,
                    data.Lows,
                    replacement,
                    data.Volumes
                ),
                20
            )
            .Outputs;
        for (var slot = 0; slot < 5; slot++)
            Assert.Equal(
                expected[HeikinAshiComparison.Names[slot]].Values,
                run[indicator.Outputs[slot]].ToArray()
            );
        var close = run[indicator.Close].ToArray();
        var actual = run[downstream.Value].ToArray();
        for (var i = 0; i < close.Length; i++)
            Assert.True(
                CircularComparison.Budget.Accepts(
                    CircularComparison.ReferenceValue("Sin", close[i])!.Value,
                    actual[i]
                )
            );
    }

    [Fact]
    public void NativeSortedGenericQuoteAndRepeatedCandleRoutesRetainFields()
    {
        var data = HeikinAshiComparison.Fixture();
        var expected = data.Quotes.GetHeikinAshi().ToArray();
        var reverse = data.Quotes.AsEnumerable().Reverse().GetHeikinAshi().ToArray();
        Assert.Equal(expected.Select(r => r.Date), reverse.Select(r => r.Date));
        Assert.Equal(
            expected.Select(r => (r.Open, r.High, r.Low, r.Close, r.Volume)),
            reverse.Select(r => (r.Open, r.High, r.Low, r.Close, r.Volume))
        );
        var second = expected.GetHeikinAshi().ToArray();
        var asQuotes = expected
            .Select(r => new Quote
            {
                Date = r.Date,
                Open = r.Open,
                High = r.High,
                Low = r.Low,
                Close = r.Close,
                Volume = r.Volume,
            })
            .GetHeikinAshi()
            .ToArray();
        Assert.Equal(
            second.Select(r => (r.Open, r.High, r.Low, r.Close, r.Volume)),
            asQuotes.Select(r => (r.Open, r.High, r.Low, r.Close, r.Volume))
        );
        Assert.Empty(Array.Empty<Quote>().GetHeikinAshi());
        Assert.Single(data.Quotes.Take(1).GetHeikinAshi());
        Assert.Throws<OverflowException>(() => new[]
            {
                new Quote
                {
                    Date = DateTime.UnixEpoch,
                    Open = decimal.MaxValue,
                    High = decimal.MaxValue,
                    Low = decimal.MaxValue,
                    Close = decimal.MaxValue,
                },
            }.GetHeikinAshi().ToArray());
        var small = CompetitorData.FromOhlcv(
            [double.Epsilon],
            [double.Epsilon],
            [double.Epsilon],
            [double.Epsilon],
            [1]
        );
        Assert.Equal(0, HeikinAshiComparison.Pair.Competitor(small, 20).Outputs["Close"].Values[0]);
        Assert.Equal(
            double.Epsilon,
            HeikinAshiComparison.Pair.Ooples(small, 20).Outputs["Close"].Values[0]
        );
        ComparisonVerifier.Check(HeikinAshiComparison.Pair, small, 20);
    }

    [Fact]
    public void EveryPriceVolumeAndPresenceCorruptionIsRejected()
    {
        var pair = HeikinAshiComparison.Pair;
        var data = HeikinAshiComparison.Fixture();
        foreach (var name in HeikinAshiComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                if (presence)
                    output.Present![0] = false;
                else
                    output.Values[0] += 1;
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
    }
}
