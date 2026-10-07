using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SequenceCandleTests
{
    public static IEnumerable<object[]> Names =>
        SequenceCandleComparison.Names.Select(n => new object[] { n });
    public static IEnumerable<object[]> Cases =>
        Enumerable
            .Range(0, SequenceCandleComparison.GoldenCases.Length)
            .Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(Cases))]
    public void HandCheckedSequencesAndBoundaries(int index)
    {
        var c = SequenceCandleComparison.GoldenCases[index];
        var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
        Assert.Equal(c.Expected, pair.Ooples(c.Data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(c.Expected, pair.Competitor(c.Data, 20).Outputs["Value"].Values[^1]);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task IndependentPublicContractChecksPeriodsAndLifecycle(string name)
    {
        foreach (var period in new[] { 1, 3, 5 })
        {
            var indicator = SequenceCandleComparison.Create(name, period);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "period",
                    () => SequenceCandleComparison.Create(name, period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory]
    [InlineData("MatchingLow")]
    [InlineData("StickSandwich")]
    public async Task EqualWindowExcludesThePatternCandles(string name)
    {
        var bars = Enumerable.Range(0, 5).Select(i => Make(i, 4, 10, 0, 6)).ToList();
        bars.Add(Make(5, 8, 1000, 0, 4));
        if (name == "StickSandwich")
            bars.Add(Make(6, 6, 1000, 5, 8));
        bars.Add(Make(bars.Count, 8, 10, 0, 4.625));
        Assert.Equal(0, (await Run(SequenceCandleComparison.Create(name), bars.ToArray()))[^1]);
    }

    [Fact]
    public async Task FirstNearWindowExcludesFirstStrikeCandle()
    {
        var bars = Enumerable
            .Range(0, 5)
            .Select(i => Make(i, 4, 10, 0, 6))
            .Concat(
                new[]
                {
                    Make(5, 2, 1000, 0, 4),
                    Make(6, -0.125, 10, -0.125, 6),
                    Make(7, 5, 12, 0, 10),
                    Make(8, 11, 12, 0, 1),
                }
            )
            .ToArray();
        Assert.Equal(0, (await Run(new ThreeLineStrikeCandle(), bars))[^1]);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task SubnormalScalingKeepsTheSignal(string name)
    {
        var c = SequenceCandleComparison.GoldenCases.First(c => c.Name == name && c.Expected != 0);
        var unit = 16 * double.Epsilon;
        var bars = c
            .Data.IndicatorBars.Select(b =>
                Make(0, b.Open * unit, b.High * unit, b.Low * unit, b.Close * unit)
            )
            .ToArray();
        for (var i = 0; i < bars.Length; i++)
            bars[i] = Make(i, bars[i].Open, bars[i].High, bars[i].Low, bars[i].Close);
        Assert.Equal(c.Expected, (await Run(SequenceCandleComparison.Create(name), bars))[^1]);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task UnrepresentableRangeWindowsKeepTheSignal(string name)
    {
        var m = double.MaxValue;
        var bars = Enumerable.Range(0, 5).Select(i => Make(i, 0, m, -m, 0)).ToList();
        if (name == "MatchingLow")
            bars.AddRange([Make(5, m, m, -m, 0), Make(6, m, m, -m, m / 16)]);
        if (name == "StickSandwich")
            bars.AddRange([
                Make(5, m, m, -m, 0),
                Make(6, m / 8, m, m / 16, m / 4),
                Make(7, m, m, -m, m / 16),
            ]);
        if (name == "ThreeLineStrike")
            bars.AddRange([
                Make(5, -m / 2, m, -m, -m / 4),
                Make(6, -m / 4, m, -m, 0),
                Make(7, 0, m, -m, m / 4),
                Make(8, m / 2, m, -m, -m),
            ]);
        if (name == "ThreeOutside")
            bars.AddRange([
                Make(5, m / 2, m, -m, 0),
                Make(6, -m / 2, m, -m, m * 0.75),
                Make(7, m, m, -m, m),
            ]);
        Assert.Equal(100, (await Run(SequenceCandleComparison.Create(name), bars.ToArray()))[^1]);
    }

    [Theory]
    [InlineData("MatchingLow", 1)]
    [InlineData("StickSandwich", 2)]
    [InlineData("ThreeLineStrike", 3)]
    public void ValidatesPeriodsAndDoesNotAllocateUnobservedHistory(string name, int offset)
    {
        foreach (var invalid in new[] { 0, -1, int.MaxValue - offset + 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SequenceCandleComparison.Create(name, invalid)
            );
        var indicator = SequenceCandleComparison.Create(name, int.MaxValue - offset);
        var factory = indicator
            .GetType()
            .GetMethod(
                "CreateState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
        state.Reset();
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
    }

    [Fact]
    public void PinnedLookbacksAndToleranceDefaults()
    {
        Assert.Equal(
            new[] { 6, 7, 8, 3 },
            new[]
            {
                TALib.Candles.MatchingLowLookback(),
                TALib.Candles.StickSandwichLookback(),
                TALib.Candles.ThreeLineStrikeLookback(),
                TALib.Candles.ThreeOutsideLookback(),
            }
        );
        Assert.Equal(
            0.05,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Equal).Factor
        );
        Assert.Equal(0.2, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near).Factor);
    }

    [Fact]
    public void NewFixtureChecksEveryCandlePair()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, SequenceCandleComparison.Fixture(), 20);
    }

    private static Bar Make(int i, double open, double high, double low, double close) =>
        new(DateTime.UnixEpoch.AddDays(i), open, high, low, close, 1);

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
