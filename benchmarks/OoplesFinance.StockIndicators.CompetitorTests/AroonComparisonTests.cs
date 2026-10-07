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

public sealed class AroonComparisonTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IndependentRankOracleCoversLifecycleAndExtremes(bool extra, bool oldest)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WindowAroon),
                    "Aroon conventions",
                    () => new WindowAroon(period, extra, oldest)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    private static async Task<double[][]> Run(WindowAroon indicator, params double[] prices)
    {
        var bars = prices
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task WindowLengthAndTiePolicyHaveIndependentEffects()
    {
        var newest = await Run(new(2), 3, 3, 3, 3);
        Assert.Equal(new[] { 0d, 0, 100, 100 }, newest[0]);
        Assert.Equal(newest[0], newest[1]);
        var oldest = await Run(new(2, true, true), 3, 3, 3, 3);
        Assert.Equal(new[] { 0d, 0, 0, 0 }, oldest[0]);
        Assert.Equal(oldest[0], oldest[1]);
        Assert.Equal(new[] { 0d, 0, 1, 1 }, oldest[3]);
        var shorter = await Run(new(2, false, true), 3, 3, 3, 3);
        Assert.Equal(new[] { 0d, 50, 50, 50 }, shorter[0]);
        Assert.Equal(new[] { 0d, 1, 1, 1 }, shorter[3]);
        var expiry = await Run(new(2), 9, 1, 2, 3, 0);
        Assert.Equal(new[] { 0d, 0, 0, 100, 50 }, expiry[0]);
        Assert.Equal(new[] { 0d, 0, 50, 0, 100 }, expiry[1]);
        Assert.Equal(new[] { 0d, 0, -50, 100, -50 }, expiry[2]);
    }

    [Fact]
    public async Task OscillatorRoundsExactDifferenceIndependentlyOfPublishedComponents()
    {
        var prices = new[] { 4d, 5, 3, 1 };
        var owned = await Run(new(3, true, true), prices);
        Assert.Equal(100d / 3, owned[0][3]);
        Assert.Equal(100, owned[1][3]);
        Assert.Equal(-200d / 3, owned[2][3]);
        Assert.NotEqual(owned[0][3] - owned[1][3], owned[2][3]);
        var native = PriceWindowChannelComparison.PointFixture(prices).Quotes.GetAroon(3).Last();
        Assert.Equal(native.AroonUp - native.AroonDown, native.Oscillator);
        Assert.NotEqual(owned[2][3], native.Oscillator);
    }

    [Fact]
    public async Task NegativeExtremaDoNotUseTheNativeSkenderZeroSeed()
    {
        var result = await Run(new(2, true, true), -5, -5, -5);
        Assert.Equal(0, result[0][2]);
        Assert.Equal(0, result[1][2]);
        Assert.Equal(0, result[2][2]);
        var native = PriceWindowChannelComparison
            .PointFixture(-5, -5, -5)
            .Quotes.GetAroon(2)
            .Last();
        Assert.Equal(-50, native.AroonUp);
        Assert.Equal(0, native.AroonDown);
        Assert.Equal(-50, native.Oscillator);
    }

    [Fact]
    public async Task SubnormalAndExtremePricesPreserveOrderingAndMaximumPeriodsAreLazy()
    {
        var tiny = await Run(new(2), 0, double.Epsilon, 0);
        Assert.Equal(50, tiny[0][2]);
        Assert.Equal(100, tiny[1][2]);
        Assert.Equal(-50, tiny[2][2]);
        var huge = await Run(new(2), 0, double.MaxValue, -double.MaxValue);
        Assert.Equal(tiny[0], huge[0]);
        Assert.Equal(tiny[1], huge[1]);
        Assert.Equal(tiny[2], huge[2]);
        foreach (var extra in new[] { false, true })
        {
            var lazy = await Run(new(int.MaxValue, extra), 1, 2, 3);
            Assert.All(lazy, row => Assert.All(row, v => Assert.Equal(0, v)));
        }
    }

    [Fact]
    public void AllNativeDefinitionsMatchTheirIndependentReferences()
    {
        foreach (var pair in AroonComparison.Pairs)
        foreach (var period in new[] { 2, 3, 20 })
        foreach (
            var data in new[]
            {
                AroonComparison.Fixture(),
                PriceWindowChannelComparison.CollapsedQuoteFixture(),
                PriceWindowChannelComparison.PointFixture(3, 3, 3),
                CompetitorData.FromCloses([5]),
            }
        )
            ComparisonVerifier.Check(pair, data, period);
        foreach (
            var pair in AroonComparison.Pairs.Where(p =>
                !p.Id.StartsWith("TaLib.", StringComparison.Ordinal)
            )
        )
            ComparisonVerifier.Check(pair, AroonComparison.Fixture(), 1);
    }

    [Fact]
    public void TradyObjectTupleAndOscillatorGenericRoutesAgree()
    {
        var data = AroonComparison.Fixture();
        var tuples = data.Candles.Select(c => (c.High, c.Low)).ToArray();
        var normal = new T.Aroon(data.Candles, 3).Compute().Select(r => r.Tick).ToArray();
        Assert.Equal(normal, new T.AroonByTuple(tuples, 3).Compute().ToArray());
        var oscillator = new T.AroonOscillator(data.Candles, 3)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        Assert.Equal(oscillator, new T.AroonOscillatorByTuple(tuples, 3).Compute().ToArray());
        Assert.Equal(
            oscillator,
            new T.AroonOscillator<IOhlcv, AnalyzableTick<decimal?>>(
                data.Candles,
                c => (c.High, c.Low),
                3
            )
                .Compute()
                .Select(r => r.Tick)
                .ToArray()
        );
    }

    [Fact]
    public void EveryValueAndPresenceFlagRejectsCorruptionInBothArms()
    {
        foreach (var pair in AroonComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var name in pair.OutputNames!)
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                var i = data.Count - 1;
                if (presence)
                    output.Present![i] = !output.Present[i];
                else
                    output.Values[i] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    AroonComparison.Fixture(),
                    3
                )
            );
        }
    }

    [Fact]
    public void PeriodBoundariesAndWindowStartupAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowAroon(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AroonComparison.Fixture().Quotes.GetAroon(0).ToArray()
        );
        var values = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            TALib.Functions.Aroon<double>(
                values,
                values,
                System.Range.All,
                new double[2],
                new double[2],
                out _,
                1
            )
        );
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            TALib.Functions.AroonOsc<double>(
                values,
                values,
                System.Range.All,
                new double[2],
                out _,
                1
            )
        );
        foreach (var pair in AroonComparison.Pairs)
        {
            var result = pair.Competitor(AroonComparison.Fixture(), int.MaxValue);
            Assert.All(
                result.Outputs.Values,
                row => Assert.All(row.Present!, present => Assert.False(present))
            );
        }
    }
}
