using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ContainmentCandleTests
{
    public static IEnumerable<object[]> Names => ContainmentCandleComparison.Names.Select(n => new object[] { n });
    public static IEnumerable<object[]> GoldenCases()
    {
        foreach (var name in ContainmentCandleComparison.Names)
        for (var i = 0; i < ContainmentCandleComparison.Candidates.Length; i++)
        {
            var expected = name switch
            {
                "Harami" when i is 0 or 3 or 4 or 6 or 9 or 16 or 18 => -100d,
                "Harami" when i is 1 or 2 or 5 or 7 or 17 => 100d,
                "HaramiCross" when i is 0 or 3 or 16 => -100d,
                "HaramiCross" when i is 1 or 2 or 17 => 100d,
                "HomingPigeon" when i == 1 => 100d,
                "DojiStar" when i == 12 => -100d,
                "DojiStar" when i == 14 => 100d,
                _ => 0d
            };
            yield return new object[] { name, i, expected };
        }
    }
    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task HandCheckedColorsContainmentGapsAndThresholdEqualities(string name, int index, double expected)
    {
        var c = ContainmentCandleComparison.Candidates[index];
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 4, 10, 0, 6))
            .Append(Make(10, c.PreviousOpen, 10, 0, c.PreviousClose))
            .Append(Make(11, c.Open, 10, Math.Min(0, Math.Min(c.Open, c.Close)), c.Close)).ToArray();
        var actual = await Run(ContainmentCandleComparison.Create(name), bars);
        Assert.All(actual.Take(11), v => Assert.Equal(0, v));
        Assert.Equal(expected, actual[^1]);
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task IndependentContractsIncludeShortPeriodsAndLifecycle(string name)
    {
        foreach (var period in new[] { 1, 3, 10 })
        {
            var indicator = ContainmentCandleComparison.Create(name, period);
            var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(indicator.GetType(), "period",
                () => ContainmentCandleComparison.Create(name, period)));
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task UnrepresentableBodiesAndRangesStillClassify(string name)
    {
        var m = double.MaxValue;
        var previous = name == "HomingPigeon" ? Make(1, m, m, -m, -m)
            : Make(1, -m, m, -m, name == "DojiStar" ? 0 : m);
        var current = name == "HomingPigeon" ? Make(2, 1, 1, 0, 0)
            : name == "DojiStar" ? Make(2, 1, 2, 1, 2) : Make(2, 0, 1, 0, 1);
        Assert.Equal(new[] { 0d, 0, name == "HomingPigeon" ? 100d : -100d },
            await Run(ContainmentCandleComparison.Create(name, 1), [Make(0, 0, m, -m, 0), previous, current]));
    }
    [Theory]
    [MemberData(nameof(Names))]
    public async Task BinaryScalingIntoSubnormalPricesPreservesSignals(string name)
    {
        var index = name == "HomingPigeon" ? 1 : name == "DojiStar" ? 12 : 0;
        var c = ContainmentCandleComparison.Candidates[index]; var unit = 8 * double.Epsilon;
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 4*unit, 10*unit, 0, 6*unit))
            .Append(Make(10, c.PreviousOpen*unit, 10*unit, 0, c.PreviousClose*unit))
            .Append(Make(11, c.Open*unit, 10*unit, 0, c.Close*unit)).ToArray();
        Assert.Equal(name == "HomingPigeon" ? 100 : -100, (await Run(ContainmentCandleComparison.Create(name), bars))[^1]);
    }
    [Theory]
    [MemberData(nameof(Names))]
    public void ValidatesPeriodsWithoutEagerArrays(string name)
    {
        foreach (var period in new[] { -1, 0, int.MaxValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => ContainmentCandleComparison.Create(name, period));
        var indicator = ContainmentCandleComparison.Create(name, int.MaxValue-1);
        var factory = indicator.GetType().GetMethod("CreateState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
        state.Reset();
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
    }
    [Fact]
    public void PinnedLookbacks()
    {
        Assert.Equal(new[] { 11, 11, 11, 11 }, new[] { TALib.Candles.HaramiLookback(), TALib.Candles.HaramiCrossLookback(),
            TALib.Candles.HomingPigeonLookback(), TALib.Candles.DojiStarLookback() });
    }
    [Fact]
    public void AddedFixtureAgreesForExistingCandlePairs()
    {
        var data = ContainmentCandleComparison.Fixture();
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle)) ComparisonVerifier.Check(pair, data, 20);
    }
    private static Bar Make(int i, double open, double high, double low, double close) => new(DateTime.UnixEpoch.AddDays(i), open, high, low, close, 1);
    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
