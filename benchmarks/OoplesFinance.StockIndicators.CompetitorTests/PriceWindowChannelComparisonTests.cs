using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PriceWindowChannelComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentRationalContractCoversLifecycleAndExtremes(bool prior)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    prior ? typeof(PriorPriceChannel) : typeof(WindowPriceRange),
                    "price window channel",
                    () => prior ? new PriorPriceChannel(period) : new WindowPriceRange(period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    private static async Task<double[][]> Run(
        IIndicator indicator,
        params (double High, double Low, double Close)[] values
    )
    {
        var bars = values
            .Select(
                (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v.Close, v.High, v.Low, v.Close, 0)
            )
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task PriorWindowExcludesCurrentCandleAndExpiresItsOldestExtrema()
    {
        var result = await Run(
            new PriorPriceChannel(2),
            (1, 1, 1),
            (3, 3, 3),
            (100, 100, 100),
            (5, 5, 5),
            (2, 2, 2)
        );
        Assert.Equal(new[] { 0d, 0, 3, 100, 100 }, result[0]);
        Assert.Equal(new[] { 0d, 0, 1, 3, 5 }, result[1]);
        Assert.Equal(new[] { 0d, 0, 2, 51.5, 52.5 }, result[2]);
        Assert.Equal(new[] { 0d, 0, 1, 194d / 103, 38d / 21 }, result[3]);
        Assert.All(result.Skip(4), row => Assert.Equal(new[] { 0d, 0, 1, 1, 1 }, row));
        var current = await Run(
            new WindowPriceRange(2),
            (1, 1, 1),
            (3, 3, 3),
            (100, 100, 100),
            (5, 5, 5),
            (2, 2, 2)
        );
        Assert.Equal(new[] { 0d, 2, 97, 95, 3 }, current[0]);
    }

    [Fact]
    public async Task NegativePricesRetainTrueExtremaAndSignedWidth()
    {
        var result = await Run(new PriorPriceChannel(2), (-5, -5, -5), (-3, -3, -3), (-1, -1, -1));
        Assert.Equal(-3, result[0][2]);
        Assert.Equal(-5, result[1][2]);
        Assert.Equal(-4, result[2][2]);
        Assert.Equal(-.5, result[3][2]);
        var native = PriceWindowChannelComparison
            .PointFixture(-5, -3, -1)
            .Quotes.GetDonchian(2)
            .Last();
        Assert.Equal(0m, native.UpperBand);
        Assert.Equal(-5m, native.LowerBand);
        Assert.Equal(-2.5m, native.Centerline);
        Assert.Equal(-2m, native.Width);
    }

    [Fact]
    public async Task WidthPresenceUsesExactMidpointEvenWhenPublishedCenterUnderflows()
    {
        var tiny = await Run(new PriorPriceChannel(1), (double.Epsilon, 0, 0), (0, 0, 0));
        Assert.Equal(0, tiny[2][1]);
        Assert.Equal(2, tiny[3][1]);
        Assert.Equal(1, tiny[7][1]);
        var zero = await Run(new PriorPriceChannel(1), (1, -1, 0), (2, 0, 1), (0, 0, 0));
        Assert.Equal(new[] { 0d, 1, 1 }, zero[6]);
        Assert.Equal(new[] { 0d, 0, 1 }, zero[7]);
        Assert.Equal(new[] { 0d, 0, 2 }, zero[3]);
    }

    [Fact]
    public async Task NormalizationSurvivesOversizedRangeAndStorageIsLazy()
    {
        var high = double.MaxValue;
        var low = -double.MaxValue / 2;
        var flat = await Run(new PriorPriceChannel(1), (high, high, high), (0, 0, 0));
        Assert.Equal(high, flat[2][1]);
        Assert.Equal(0, flat[3][1]);
        var quotes = Enumerable
            .Range(0, 2)
            .Select(i => new Quote
            {
                Date = DateTime.UnixEpoch.AddDays(i),
                Open = decimal.MaxValue,
                High = decimal.MaxValue,
                Low = decimal.MaxValue,
                Close = decimal.MaxValue,
            });
        Assert.Throws<OverflowException>(() => quotes.GetDonchian(1).ToArray());
        var channel = await Run(new PriorPriceChannel(1), (high, low, 0), (0, 0, 0));
        Assert.Equal(6, channel[3][1]);
        Assert.Equal(high / 4, channel[2][1]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(new WindowPriceRange(1), (high, low, 0))
        );
        var balanced = await Run(new PriorPriceChannel(1), (high, -high, 0), (0, 0, 0));
        Assert.Equal(0, balanced[7][1]);
        Assert.Equal(high, balanced[0][1]);
        var lazy = await Run(new PriorPriceChannel(int.MaxValue), (1, 0, 0), (2, 0, 1));
        Assert.All(lazy, row => Assert.All(row, v => Assert.Equal(0, v)));
        Assert.Equal(
            new[] { 1d, 2 },
            (await Run(new WindowPriceRange(int.MaxValue), (1, 0, 0), (2, 0, 1)))[0]
        );
    }

    [Fact]
    public async Task WilliamsFlatAndSubnormalNativeConventionsAreExplicit()
    {
        var flat = PriceWindowChannelComparison.PointFixture(3, 3, 3);
        var skender = ComparisonPairs.Get("Skender.GetWilliamsR");
        var talib = ComparisonPairs.Get("TaLib.Functions.WillR");
        Assert.Equal(-100, skender.Competitor(flat, 2).Outputs["Value"].Values[2]);
        Assert.Equal(0, talib.Competitor(flat, 2).Outputs["Value"].Values[2]);
        Assert.Equal(-100, talib.Ooples(flat, 2).Outputs["Value"].Values[2]);
        var tiny = await Run(new WilliamsR(2), (double.Epsilon, 0, 0), (double.Epsilon, 0, 0));
        Assert.Equal(-100, tiny[0][1]);
        var output = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            TALib.Functions.WillR<double>(
                new[] { double.Epsilon, double.Epsilon },
                new double[2],
                new double[2],
                System.Range.All,
                output,
                out _,
                2
            )
        );
        Assert.Equal(0, output[0]);
        var huge = await Run(
            new WilliamsR(2),
            (double.MaxValue, -double.MaxValue, 0),
            (double.MaxValue, -double.MaxValue, 0)
        );
        Assert.Equal(-50, huge[0][1]);
        TALib.Functions.WillR<double>(
            new[] { double.MaxValue, double.MaxValue },
            new[] { -double.MaxValue, -double.MaxValue },
            new double[2],
            System.Range.All,
            output,
            out _,
            2
        );
        Assert.Equal(0, output[0]);
    }

    [Fact]
    public void FullComparisonsCoverSignedPricesFlatWindowsAndQuoteCollapse()
    {
        foreach (var pair in PriceWindowChannelComparison.Pairs)
        foreach (var period in new[] { 2, 3, 20 })
        foreach (
            var data in new[]
            {
                PriceWindowChannelComparison.Fixture(),
                PriceWindowChannelComparison.CollapsedQuoteFixture(),
                CompetitorData.FromCloses([5]),
            }
        )
            ComparisonVerifier.Check(pair, data, period);
        foreach (
            var pair in PriceWindowChannelComparison.Pairs.Where(p =>
                p.Id != "TaLib.Functions.WillR"
            )
        )
            ComparisonVerifier.Check(pair, PriceWindowChannelComparison.Fixture(), 1);
        var collapse = PriceWindowChannelComparison.CollapsedQuoteFixture();
        var williams = ComparisonPairs.Get("Skender.GetWilliamsR");
        Assert.Equal(0, williams.Ooples(collapse, 2).Outputs["Value"].Values[1]);
        Assert.Equal(-100, williams.Competitor(collapse, 2).Outputs["Value"].Values[1]);
    }

    [Fact]
    public void TradyObjectGenericAndTupleRoutesAgree()
    {
        var data = PriceWindowChannelComparison.Fixture();
        var normal = new T.HighestHighLowestLowDifference(data.Candles, 3)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        var generic = new T.HighestHighLowestLowDifference<IOhlcv, AnalyzableTick<decimal?>>(
            data.Candles,
            c => (c.High, c.Low),
            3
        )
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        var tuple = new T.HighestHighLowestLowDifferenceByTuple(
            data.Candles.Select(c => (c.High, c.Low)),
            3
        )
            .Compute()
            .ToArray();
        Assert.Equal(normal, generic);
        Assert.Equal(normal, tuple);
    }

    [Fact]
    public void EveryOutputAndChannelPresenceFlagRejectsCorruption()
    {
        foreach (var pair in PriceWindowChannelComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var name in pair.OutputNames!)
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                var index = data.Count - 1;
                if (presence && output.Present is not null)
                    output.Present[index] = !output.Present[index];
                else
                    output.Values[index] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    PriceWindowChannelComparison.Fixture(),
                    3
                )
            );
        }
    }

    [Fact]
    public void PeriodBoundariesAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PriorPriceChannel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowPriceRange(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PriceWindowChannelComparison.Fixture().Quotes.GetDonchian(0).ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PriceWindowChannelComparison.Fixture().Quotes.GetWilliamsR(0).ToArray()
        );
        foreach (var period in new[] { 0, -1 })
            Assert.All(
                new T.HighestHighLowestLowDifference(
                    PriceWindowChannelComparison.Fixture().Candles,
                    period
                ).Compute(),
                row => Assert.Null(row.Tick)
            );
        Assert.All(
            PriceWindowChannelComparison.Fixture().Quotes.GetDonchian(int.MaxValue),
            row => Assert.Null(row.Width)
        );
        var output = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            TALib.Functions.WillR<double>(
                new double[2],
                new double[2],
                new double[2],
                System.Range.All,
                output,
                out _,
                1
            )
        );
    }
}
