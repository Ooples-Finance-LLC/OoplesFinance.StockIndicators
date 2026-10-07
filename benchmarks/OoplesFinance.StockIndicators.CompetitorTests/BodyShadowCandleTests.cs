using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class BodyShadowCandleTests
{
    public static IEnumerable<object[]> Names => BodyShadowComparison.Names.Select(name => new object[] { name });

    public static IEnumerable<object[]> GoldenCases()
    {
        var bullish = new Dictionary<string, int[]>
        {
            ["Marubozu"] = [0],
            ["BeltHold"] = [0, 4],
            ["ClosingMarubozu"] = [0, 3],
            ["SpinningTop"] = [5, 6, 9, 11],
            ["HighWave"] = [5, 9, 11],
            ["LongLine"] = [0, 1, 4, 10],
            ["ShortLine"] = [5, 6, 7, 8, 11, 12]
        };
        foreach (var name in BodyShadowComparison.Names)
        {
            for (var index = 0; index < BodyShadowComparison.Candidates.Length; index++)
            {
                var positive = bullish[name].Contains(index);
                yield return new object[] { name, index, false, positive ? 100d : 0d };
                var negative = name == "ClosingMarubozu" ? index is 0 or 4 : name == "BeltHold" ? index is 0 or 3 : positive;
                // Equal body endpoints remain bullish in both orientations.
                yield return new object[] { name, index, true, negative ? index >= 11 ? 100d : -100d : 0d };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task PublicRuntimeMatchesHandCheckedBodyAndShadowCases(string name, int index, bool reverse, double expected)
    {
        var candidate = BodyShadowComparison.Candidates[index];
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 4, 10, 0, 6)).Append(Make(10,
            reverse ? candidate.Close : candidate.Open, candidate.High, candidate.Low, reverse ? candidate.Open : candidate.Close)).ToArray();
        var actual = await Run(BodyShadowComparison.Create(name), bars);
        Assert.All(actual.Take(10), value => Assert.Equal(0, value));
        Assert.Equal(expected, actual[^1]);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task IndependentPublicContractChecksFormulaAndLifecycle(string name)
    {
        var indicator = BodyShadowComparison.Create(name);
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(
            indicator.GetType(), "default", () => BodyShadowComparison.Create(name)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task ExtremeFiniteBodyAndShadowWindowsDoNotOverflow(string name)
    {
        var longBody = name is "Marubozu" or "ClosingMarubozu" or "LongLine" or "BeltHold";
        var previous = longBody ? Make(0, 0, double.MaxValue, -double.MaxValue, 0)
            : Make(0, -double.MaxValue / 2, double.MaxValue, -double.MaxValue, double.MaxValue / 2);
        var current = longBody ? Make(1, 0, double.MaxValue, 0, double.MaxValue)
            : name == "ShortLine" ? Make(1, 0, 1, 0, 1) : Make(1, 0, double.MaxValue, -double.MaxValue, 1);
        Assert.Equal(new[] { 0d, 100d }, await Run(BodyShadowComparison.Create(name, 1), [previous, current]));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task BinaryScalingIntoSubnormalPricesPreservesClassification(string name)
    {
        var index = name switch { "Marubozu" or "ClosingMarubozu" or "LongLine" or "BeltHold" => 0, _ => 5 };
        var candle = BodyShadowComparison.Candidates[index];
        var unit = 8 * double.Epsilon;
        var bars = new[] { Make(0, 4 * unit, 10 * unit, 0, 6 * unit),
            Make(1, candle.Open * unit, candle.High * unit, candle.Low * unit, candle.Close * unit) };
        Assert.Equal(new[] { 0d, 100d }, await Run(BodyShadowComparison.Create(name, 1), bars));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ValidatesPeriodsAndAllocatesOnlyObservedHistory(string name)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyShadowComparison.Create(name, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyShadowComparison.Create(name, -1));
        var indicator = BodyShadowComparison.Create(name, int.MaxValue);
        var factory = indicator.GetType().GetMethod("CreateState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
        state.Reset();
        Assert.Equal(0, state.Update(Make(0, 4, 10, 0, 6)));
    }

    [Fact]
    public void PinnedPackageThresholdsAndLookbacksMatchTheComparedConfiguration()
    {
        foreach (var kind in new[] { TALib.Core.CandleSettingType.BodyLong, TALib.Core.CandleSettingType.BodyShort })
        {
            var setting = TALib.Core.CandleSettings.Get(kind);
            Assert.Equal(TALib.Core.CandleRangeType.RealBody, setting.RangeType);
            Assert.Equal(10, setting.AveragePeriod);
            Assert.Equal(1, setting.Factor);
        }
        var shortShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowShort);
        Assert.Equal(TALib.Core.CandleRangeType.Shadows, shortShadow.RangeType);
        Assert.Equal(10, shortShadow.AveragePeriod);
        Assert.Equal(1, shortShadow.Factor);
        var veryLongShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowVeryLong);
        Assert.Equal(TALib.Core.CandleRangeType.RealBody, veryLongShadow.RangeType);
        Assert.Equal(0, veryLongShadow.AveragePeriod);
        Assert.Equal(2, veryLongShadow.Factor);
        Assert.Equal(new[] { 10, 10, 10, 10, 10, 10, 10 }, new[] { TALib.Candles.MarubozuLookback(), TALib.Candles.ClosingMarubozuLookback(),
            TALib.Candles.SpinningTopLookback(), TALib.Candles.HighWaveLookback(), TALib.Candles.LongLineLookback(), TALib.Candles.ShortLineLookback(), TALib.Candles.BeltHoldLookback() });
    }

    private static Bar Make(int index, double open, double high, double low, double close) =>
        new(DateTime.UnixEpoch.AddDays(index), open, high, low, close, 1);

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
