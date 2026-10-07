using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PriceCandleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullNamedOutputsAndParameters(bool marubozu)
    {
        foreach (var percent in marubozu ? new[] { 80d, 95d, 100d } : new[] { 0d, .1, .5 })
            ComparisonVerifier.Check(
                PriceCandleComparison.Create(marubozu, percent),
                PriceCandleComparison.Fixture(),
                20
            );
        var pair = PriceCandleComparison.Create(marubozu);
        var data = CompetitorData.FromOhlc([1], [20], [0], [20]);
        var values = pair.Ooples(data, 20).Outputs;
        Assert.Equal(20, values["Size"].Values[0]);
        Assert.Equal(19, values["Body"].Values[0]);
        Assert.Equal(0, values["UpperWick"].Values[0]);
        Assert.Equal(1, values["LowerWick"].Values[0]);
        Assert.Equal(.95, values["BodyFraction"].Values[0]);
        Assert.Equal(0, values["UpperWickFraction"].Values[0]);
        Assert.Equal(.05, values["LowerWickFraction"].Values[0]);
        Assert.Equal(1, values["IsBullish"].Values[0]);
        Assert.Equal(0, values["IsBearish"].Values[0]);
        Assert.Equal(marubozu ? 100 : 0, values["Match"].Values[0]);
        Assert.Equal(marubozu, values["Price"].Present![0]);
        if (marubozu)
            Assert.Equal(20, values["Price"].Values[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroRangeAndPricePresenceAreExplicit(bool marubozu)
    {
        var data = CompetitorData.FromBodies([0, 5], [0, 5]);
        var pair = PriceCandleComparison.Create(marubozu);
        ComparisonVerifier.Check(pair, data, 20);
        var output = pair.Ooples(data, 20).Outputs;
        Assert.Equal(marubozu ? new[] { -100d, -100d } : new[] { 0d, 1d }, output["Match"].Values);
        Assert.Equal(
            marubozu ? new[] { true, true } : new[] { false, true },
            output["Price"].Present
        );
        foreach (var name in new[] { "BodyFraction", "UpperWickFraction", "LowerWickFraction" })
            Assert.Equal(new[] { 1d, 1d }, output[name].Values);
    }

    [Fact]
    public void DecimalAndBinaryBoundaryContractsCannotMaskCorruption()
    {
        var data = CompetitorData.FromBodies([1000], [1001]);
        // Exact supplied 0.1 percent is slightly greater than 1/1000; Skender's
        // decimal ratio converted to binary64 also happens to match here.
        var exact = PriceCandleComparison.Create(false, Math.BitDecrement(.1));
        Assert.Equal(0, exact.Ooples(data, 20).Outputs["Match"].Values[0]);
        Assert.Equal(1, exact.Competitor(data, 20).Outputs["Match"].Values[0]);
        ComparisonVerifier.Check(exact, data, 20);
        static ComparisonSeries Corrupt(ComparisonSeries source) =>
            new(
                source.Outputs.ToDictionary(
                    entry => entry.Key,
                    entry =>
                        entry.Key == "Match" ? entry.Value with { Values = [99d] } : entry.Value
                )
            );
        var corrupt = exact with
        {
            Library = (input, period) => Corrupt(exact.Ooples(input, period)),
        };
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(corrupt, data, 20));
        corrupt = exact with
        {
            Competitor = (input, period) => Corrupt(exact.Competitor(input, period)),
        };
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(corrupt, data, 20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeSourceMetadataAndSortingAreRetained(bool marubozu)
    {
        var data = PriceCandleComparison.Fixture();
        var input = data.Quotes.AsEnumerable().Reverse();
        var rows = (marubozu ? input.GetMarubozu() : input.GetDoji()).ToArray();
        Assert.Equal(data.Dates, rows.Select(r => r.Date));
        Assert.Equal(data.Dates, rows.Select(r => r.Candle.Date));
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(data.Quotes[i].Open, rows[i].Candle.Open);
            Assert.Equal(data.Quotes[i].High, rows[i].Candle.High);
            Assert.Equal(data.Quotes[i].Low, rows[i].Candle.Low);
            Assert.Equal(data.Quotes[i].Close, rows[i].Candle.Close);
            Assert.Equal(data.Quotes[i].Volume, rows[i].Candle.Volume);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentReferenceAndLifecycle(bool marubozu)
    {
        foreach (var percent in marubozu ? new[] { 80d, 95d, 100d } : new[] { 0d, .1, .5 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(PriceCandlePattern),
                    marubozu ? "marubozu" : "relative doji",
                    () => new PriceCandlePattern(PriceCandleComparison.Kind(marubozu), percent)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
        foreach (
            var percent in new[]
            {
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
                -1d,
                101d,
            }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PriceCandlePattern(PriceCandleComparison.Kind(marubozu), percent)
            );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriceCandlePattern((PriceCandleKind)99)
        );
    }

    [Fact]
    public async Task SubnormalPriceDifferenceDoesNotCollapseToATie()
    {
        var indicator = new PriceCandlePattern(percent: 0);
        var input = new[]
        {
            new Bar(
                DateTime.UnixEpoch,
                double.Epsilon,
                2 * double.Epsilon,
                0,
                2 * double.Epsilon,
                1
            ),
        };
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(input))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(0, run[indicator.Match].ToArray()[0]);
        Assert.Equal(double.Epsilon, run[indicator.Body].ToArray()[0]);
        Assert.Equal(.5, run[indicator.BodyFraction].ToArray()[0]);
    }
}
