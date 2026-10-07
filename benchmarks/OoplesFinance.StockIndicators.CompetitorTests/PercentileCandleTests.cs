using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PercentileCandleTests
{
    public static IEnumerable<object[]> Names => PercentileCandleComparison.Names.Select(n => new object[] { n });
    [Theory]
    [MemberData(nameof(Names))]
    public async Task HandCheckedInterpolatedWindowsTiesAndColor(string name)
    {
        double[] lengths = [1, 2, 3, 4, 4, 0.5, 0];
        var bars = lengths.Select((v, i) => Make(name, i, v, i%2 != 0)).ToArray();
        var expected = name switch
        {
            "LongDay" or "LongUpperShadow" => new[] { 0d, 0, 0, 1, 1, 0, 0 },
            "ShortDay" or "LongLowerShadow" => [0d, 0, 0, 0, 0, 1, 1],
            "BullishLongDay" => [0d, 0, 0, 0, 1, 0, 0],
            "BearishLongDay" => [0d, 0, 0, 1, 0, 0, 0],
            "BearishShortDay" => [0d, 0, 0, 0, 0, 1, 0],
            _ => [0d, 0, 0, 0, 0, 0, 0]
        };
        Assert.Equal(expected, await Run(PercentileCandleComparison.Create(name, 4), bars));
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task PublicContractsVerifyNondefaultPeriodsAndPercentiles(string name)
    {
        foreach (var (period, q) in new[] { (1, 0m), (4, 0.25m), (3, 0.9999999999999999999999999999m), (3, 1m) })
        {
            var indicator = PercentileCandleComparison.Create(name, period, q);
            var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(indicator.GetType(), "percentile",
                () => PercentileCandleComparison.Create(name, period, q)));
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task SubnormalLengthsKeepTheSameRanks(string name)
    {
        var bars = new[] { Make(name, 0, 8, true), Make(name, 1, 16, false), Make(name, 2, 24, true), Make(name, 3, 32, false) };
        var tiny = bars.Select(b => new Bar(b.Time, b.Open*double.Epsilon, b.High*double.Epsilon,
            b.Low*double.Epsilon, b.Close*double.Epsilon, b.Volume)).ToArray();
        Assert.Equal(await Run(PercentileCandleComparison.Create(name, 3), bars), await Run(PercentileCandleComparison.Create(name, 3), tiny));
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task OverflowingLengthsAreOrderedExactly(string name)
    {
        var m = double.MaxValue;
        var above = !name.Contains("Short", StringComparison.Ordinal) && name != "LongLowerShadow";
        var bars = new[] { Extreme(0, above ? 0.75 : 1), Extreme(1, above ? 1 : 0.75) };
        Assert.Equal(new[] { 0d, 1d }, await Run(PercentileCandleComparison.Create(name, 2), bars));
        Bar Extreme(int i, double fraction)
        {
            var open = -m * fraction; var close = m * fraction;
            if (name.StartsWith("Bearish", StringComparison.Ordinal)) (open, close) = (close, open);
            if (name == "LongUpperShadow") open = close = -m * fraction;
            if (name == "LongLowerShadow") open = close = m * fraction;
            return new(DateTime.UnixEpoch.AddDays(i), open, m, -m, close, 1);
        }
    }
    [Theory]
    [MemberData(nameof(Names))]
    public void ArgumentsAndLazyAllocation(string name)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCandleComparison.Create(name, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCandleComparison.Create(name, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCandleComparison.Create(name, 2, -0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCandleComparison.Create(name, 2, 1.1m));
        var indicator = PercentileCandleComparison.Create(name, int.MaxValue, 0.9999999999999999999999999999m);
        var factory = indicator.GetType().GetMethod("CreateState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        Assert.Equal(0, state.Update(Make(name, 0, 3, false)));
        state.Reset();
        Assert.Equal(0, state.Update(Make(name, 0, 3, false)));
    }
    [Theory]
    [InlineData("BearishLongDay")]
    [InlineData("BullishShortDay")]
    [InlineData("BearishShortDay")]
    public void PinnedCompetitorIgnoresParameters(string name)
    {
        var data = PercentileCandleComparison.Fixture();
        ComparisonVerifier.Compare(PercentileCandleComparison.Competitor(name, data, 20),
            PercentileCandleComparison.Competitor(name, data, 1, 0m), "ignored parameters");
        Assert.Contains("ignores", Assert.Single(ComparisonManifest.Create(), row => row.Id == "Trady.Candlestick."+name).CompetitorLimitation!);
    }
    [Theory]
    [InlineData("LongDay")]
    [InlineData("ShortDay")]
    [InlineData("BullishLongDay")]
    [InlineData("LongUpperShadow")]
    [InlineData("LongLowerShadow")]
    public void OtherCompetitorPatternsHonorParameters(string name)
    {
        var data = PercentileCandleComparison.Fixture();
        Assert.False(PercentileCandleComparison.Competitor(name, data, 20).Outputs["Value"].Values.SequenceEqual(
            PercentileCandleComparison.Competitor(name, data, 1, 0m).Outputs["Value"].Values));
    }
    private static Bar Make(string name, int i, double length, bool bearish)
    {
        var close = name.Contains("Shadow", StringComparison.Ordinal) ? bearish ? 19d : 21d : 20 + (bearish ? -length : length);
        return new(DateTime.UnixEpoch.AddDays(i), 20, Math.Max(20, close) + (name == "LongUpperShadow" ? length : 1),
            Math.Min(20, close) - (name == "LongLowerShadow" ? length : 1), close, 1);
    }
    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
