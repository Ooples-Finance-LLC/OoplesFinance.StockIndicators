using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CandleTrendPatternTests
{
    public static IEnumerable<object[]> Names => TradyCandleComparison.Names.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Names))]
    public async Task PublicValidationContractCoversFormulaAndLifecycle(string name)
    {
        var indicator = TradyCandleComparison.Create(name, 3);
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(
            indicator.GetType(), "default", () => TradyCandleComparison.Create(name, 3)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData("BullishEngulfingPattern", false)]
    [InlineData("BearishEngulfingPattern", true)]
    public async Task EngulfingRequiresCompletedPriorTrendAndStrictBodyEdges(string name, bool rising)
    {
        var data = TradyCandleComparison.TrendFixture(3);
        var indicator = TradyCandleComparison.Create(name, 3);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync();
        var values = run[indicator.Outputs[0]].ToArray();
        Assert.Equal(1, values[rising ? 14 : 4]);
        Assert.Equal(0, values[rising ? 19 : 9]); // Equal endpoint.
        Assert.Equal(0, values[rising ? 4 : 14]); // Wrong trend and candle direction.
        Assert.All(values.Take(4), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData("UpTrend", 1d)]
    [InlineData("DownTrend", -1d)]
    public async Task TrendCountsTransitionsAndAnEqualHighBreaksIt(string name, double direction)
    {
        var prices = new[] { 10d, 11d, 12d, 13d, 13d, 14d, 15d, 16d }.Select(v => v * direction).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var indicator = TradyCandleComparison.Create(name, 3);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 0, 0, 1, 0, 0, 0, 1 }, run[indicator.Outputs[0]].ToArray());
    }

    [Theory]
    [InlineData("Bullish", 1d, 0d)]
    [InlineData("Bearish", 0d, 1d)]
    public async Task CandleDirectionPreservesTinyDifferencesAndTreatsDojiAsNeither(string name, double up, double down)
    {
        var opens = new[] { 0d, double.Epsilon, double.MaxValue, -double.MaxValue };
        var closes = new[] { double.Epsilon, 0d, double.MaxValue, double.MaxValue };
        var bars = opens.Select((open, i) => new Bar(DateTime.UnixEpoch.AddDays(i), open,
            Math.Max(open, closes[i]), Math.Min(open, closes[i]), closes[i], 1)).ToArray();
        var indicator = TradyCandleComparison.Create(name, 1);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { up, down, 0, up }, run[indicator.Outputs[0]].ToArray());
    }

    [Theory]
    [InlineData("UpTrend")]
    [InlineData("DownTrend")]
    [InlineData("BullishEngulfingPattern")]
    [InlineData("BearishEngulfingPattern")]
    public void RejectsNonpositivePeriodsWithoutAllocating(string name)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TradyCandleComparison.Create(name, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TradyCandleComparison.Create(name, -1));
        Assert.NotNull(TradyCandleComparison.Create(name, int.MaxValue));
    }
}
