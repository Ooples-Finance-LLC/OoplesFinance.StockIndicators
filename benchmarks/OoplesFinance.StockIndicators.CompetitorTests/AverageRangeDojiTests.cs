using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AverageRangeDojiTests
{
    public static IEnumerable<object[]> Names => AverageRangeDojiComparison.Names.Select(name => new object[] { name });

    [Fact]
    public void PinnedTaLibDefaultsMatchTheComparedFormula()
    {
        var body = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.BodyDoji);
        var shortShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowVeryShort);
        foreach (var setting in new[] { body, shortShadow })
        {
            Assert.Equal(TALib.Core.CandleRangeType.HighLow, setting.RangeType);
            Assert.Equal(10, setting.AveragePeriod);
            Assert.Equal(0.1, setting.Factor);
        }
        var longShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowLong);
        Assert.Equal(TALib.Core.CandleRangeType.RealBody, longShadow.RangeType);
        Assert.Equal(0, longShadow.AveragePeriod);
        Assert.Equal(1, longShadow.Factor);
        Assert.Equal(11, TALib.Candles.DragonflyDojiLookback());
        Assert.Equal(10, TALib.Candles.TakuriLineLookback());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task DefaultContractChecksFormulaAndLifecycle(string name)
    {
        var indicator = AverageRangeDojiComparison.Create(name);
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(
            indicator.GetType(), "default", () => AverageRangeDojiComparison.Create(name)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task RecognizesPositiveNegativeAndThresholdEqualityCases(string name)
    {
        var indicator = AverageRangeDojiComparison.Create(name);
        var actual = await Run(indicator, AverageRangeDojiComparison.Fixture().IndicatorBars);
        var expected = new double[21];
        var positions = name switch
        {
            "DragonflyDoji" or "TakuriLine" => new[] { 11, 15 },
            "GravestoneDoji" => [12, 16, 20],
            _ => [10, 11, 12, 13, 14, 15, 16, 19, 20]
        };
        foreach (var index in positions) expected[index] = 100;
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public async Task ThresholdUsesPriorRangesAndIncludesBodyEquality(int period)
    {
        var bars = Enumerable.Range(0, period).Select(i => Make(i, 5, 10, 0, 5))
            .Append(Make(period, 0, 1, 0, 1)).ToArray();
        var actual = await Run(new AverageRangeDoji(period), bars);
        Assert.All(actual.Take(period), value => Assert.Equal(0, value));
        Assert.Equal(100, actual[^1]); // Prior threshold is 1; including current range would reduce it.
    }

    [Fact]
    public async Task ExpiredRangeStopsContributingToThreshold()
    {
        var bars = new[] { Make(0, 0, 100, 0, 0), Make(1, 0, 0, 0, 0), Make(2, 0, 1, 0, 1), Make(3, 0, 1, 0, 1) };
        Assert.Equal(new[] { 0d, 0, 100, 0 }, await Run(new AverageRangeDoji(2), bars));
    }

    [Fact]
    public async Task LongShadowEqualityDoesNotQualify()
    {
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 5, 10, 0, 5))
            .Append(Make(10, 0, 2, 0, 1)).ToArray();
        Assert.Equal(100, (await Run(new AverageRangeDoji(), bars))[^1]);
        Assert.Equal(0, (await Run(new AverageRangeLongLeggedDoji(), bars))[^1]);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task FiniteInputsCanHaveUnrepresentableRangesWithoutLosingTheSignal(string name)
    {
        var indicator = AverageRangeDojiComparison.Create(name);
        var price = name switch { "DragonflyDoji" or "TakuriLine" => double.MaxValue, "GravestoneDoji" => -double.MaxValue, _ => 0 };
        var bars = Enumerable.Range(0, indicator.WarmupBars + 1)
            .Select(i => Make(i, price, double.MaxValue, -double.MaxValue, price)).ToArray();
        Assert.Equal(100, (await Run(indicator, bars))[^1]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-0.125, 100)]
    public async Task TakuriRequiresLowerShadowStrictlyLongerThanTwiceBody(double low, double expected)
    {
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 5, 10, 0, 5))
            .Append(Make(10, 2, 3, low, 3)).ToArray();
        Assert.Equal(expected, (await Run(new TakuriLineCandle(), bars))[^1]);
    }

    [Fact]
    public async Task SubnormalBodyEqualityIsPreserved()
    {
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 0, 10 * double.Epsilon, 0, 0))
            .Append(Make(10, 0, double.Epsilon, 0, double.Epsilon)).ToArray();
        Assert.Equal(100, (await Run(new AverageRangeDoji(), bars))[^1]);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void PeriodsAreValidatedWithoutEagerHistoryAllocation(string name)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AverageRangeDojiComparison.Create(name, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AverageRangeDojiComparison.Create(name, -1));
        var largest = name == "DragonflyDoji" ? int.MaxValue - 1 : int.MaxValue;
        var indicator = AverageRangeDojiComparison.Create(name, largest);
        var factory = indicator.GetType().GetMethod("CreateState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        Assert.Equal(0, state.Update(Make(0, 5, 10, 0, 5)));
        state.Reset();
        Assert.Equal(0, state.Update(Make(0, 5, 10, 0, 5)));
        if (name == "DragonflyDoji") Assert.Throws<ArgumentOutOfRangeException>(() => AverageRangeDojiComparison.Create(name, int.MaxValue));
    }

    private static Bar Make(int i, double open, double high, double low, double close) =>
        new(DateTime.UnixEpoch.AddDays(i), open, high, low, close, 1);

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
