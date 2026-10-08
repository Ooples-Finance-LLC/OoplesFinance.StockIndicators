using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ContextReversalCandleTests
{
    public static IEnumerable<object[]> Names => ContextReversalComparison.Names.Select(n => new object[] { n });
    public static IEnumerable<object[]> GoldenCases()
    {
        foreach (var name in ContextReversalComparison.Names)
        for (var index = 0; index < ContextReversalComparison.Candidates.Length; index++)
        foreach (var reverse in new[] { false, true })
        {
            var expected = name switch
            {
                "Hammer" when index is 0 or 11 => 100d,
                "HangingMan" when index == 2 => -100d,
                "InvertedHammer" when index == 4 => 100d,
                "ShootingStar" when index == 6 => -100d,
                _ => 0d
            };
            yield return new object[] { name, index, reverse, expected };
        }
    }
    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task HandCheckedShapesContextsAndBothColors(string name, int index, bool reverse, double expected)
    {
        var c = ContextReversalComparison.Candidates[index];
        var bars = Enumerable.Range(0, 11).Select(i => Make(i, 4, 10, 0, 6))
            .Append(Make(11, reverse ? c.Close : c.Open, c.High, c.Low, reverse ? c.Open : c.Close)).ToArray();
        var actual = await Run(ContextReversalComparison.Create(name), bars);
        Assert.All(actual.Take(11), v => Assert.Equal(0, v));
        Assert.Equal(expected, actual[^1]);
    }
    [Theory]
    [InlineData(2, 100)]
    [InlineData(2.125, 0)]
    public async Task NearAverageExcludesImmediatelyPrecedingRange(double bottom, double expected)
    {
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 4, 10, 0, 6))
            .Append(Make(10, 4, 1000, 0, 6)).Append(Make(11, bottom, bottom + 1.5, 0, bottom + 1)).ToArray();
        Assert.Equal(expected, (await Run(new HammerCandle(), bars))[^1]);
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task IndependentContractChecksFormulaAndLifecycle(string name)
    {
        foreach (var periods in new[] { (10, 5), (1, 3), (3, 1) })
        {
            var indicator = ContextReversalComparison.Create(name, periods.Item1, periods.Item2);
            var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(indicator.GetType(),
                "periods", () => ContextReversalComparison.Create(name, periods.Item1, periods.Item2)));
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task SubnormalScalingPreservesSignal(string name)
    {
        var index = name switch { "Hammer" => 0, "HangingMan" => 2, "InvertedHammer" => 4, _ => 6 };
        var c = ContextReversalComparison.Candidates[index];
        var u = 8 * double.Epsilon;
        var bars = Enumerable.Range(0, 11).Select(i => Make(i, 4*u, 10*u, 0, 6*u))
            .Append(Make(11, c.Open*u, c.High*u, c.Low*u, c.Close*u)).ToArray();
        Assert.Equal(name is "Hammer" or "InvertedHammer" ? 100 : -100,
            (await Run(ContextReversalComparison.Create(name), bars))[^1]);
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task UnrepresentablePriorRangesRetainClassification(string name)
    {
        var m = double.MaxValue;
        var previous = Make(1, 0, m, -m, m/2);
        var current = name switch
        {
            "Hammer" => Make(2, -m, -m, -m, -m),
            "HangingMan" => Make(2, m, m, -m, m),
            "InvertedHammer" => Make(2, -m, m, -m, -m),
            _ => Make(2, m*0.75, m, m*0.75, m*0.75)
        };
        // Hammer needs a nonzero lower shadow while remaining near the prior low.
        if (name == "Hammer") current = Make(2, -m*0.75, -m*0.75, -m, -m*0.75);
        var result = await Run(ContextReversalComparison.Create(name, 1, 1), [Make(0, 0, m, -m, m/2), previous, current]);
        Assert.Equal(name is "Hammer" or "InvertedHammer" ? 100 : -100, result[^1]);
    }
    [Theory]
    [MemberData(nameof(Names))]
    public void PeriodValidationAndLazyAllocation(string name)
    {
        foreach (var invalid in new[] { -1, 0, int.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ContextReversalComparison.Create(name, invalid));
            if (name is "Hammer" or "HangingMan")
                Assert.Throws<ArgumentOutOfRangeException>(() => ContextReversalComparison.Create(name, 10, invalid));
        }
        var indicator = ContextReversalComparison.Create(name, int.MaxValue - 1, int.MaxValue - 1);
        Assert.Equal(int.MaxValue, indicator.WarmupBars);
        var factory = indicator.GetType().GetMethod("CreateState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
        state.Reset();
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
    }
    [Fact]
    public void PinnedLookbacksAndNearDefault()
    {
        Assert.Equal(new[] { 11, 11, 11, 11 }, new[] { TALib.Candles.HammerLookback(), TALib.Candles.HangingManLookback(),
            TALib.Candles.InvertedHammerLookback(), TALib.Candles.ShootingStarLookback() });
        var near = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near);
        Assert.Equal(5, near.AveragePeriod);
        Assert.Equal(0.2, near.Factor);
        Assert.Equal(TALib.Core.CandleRangeType.HighLow, near.RangeType);
    }
    private static Bar Make(int i, double open, double high, double low, double close) =>
        new(DateTime.UnixEpoch.AddDays(i), open, high, low, close, 1);
    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
