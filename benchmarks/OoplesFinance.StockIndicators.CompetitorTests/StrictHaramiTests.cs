using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class StrictHaramiTests
{
    public static IEnumerable<object[]> Names =>
        StrictHaramiComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Names))]
    public void GoldenContainmentColorsDojiAndOpenCloseTrend(string name)
    {
        var data = StrictHaramiComparison.Fixture();
        var pair = StrictHaramiComparison.Pair(name);
        var values = pair.Competitor(data, 3).Outputs["Value"].Values;
        for (var i = 0; i < StrictHaramiComparison.Candidates.Length; i++)
        {
            var positive = name switch
            {
                "Harami" => i is 0 or 3 or 5,
                "BullishHarami" => i is 0 or 5,
                _ => false,
            };
            var negative = name is "Harami" or "BearishHarami" && i is 0 or 5;
            Assert.Equal(positive ? 1 : 0, values[i * 10 + 4]);
            Assert.Equal(negative ? 1 : 0, values[i * 10 + 9]);
        }
        ComparisonVerifier.Check(pair, data, 3);
        Assert.Equal(name == "Harami" ? double.NaN : 0, values[0]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferencesLifecycleAndAllShadowModes(string name)
    {
        foreach (var period in new[] { 1, 3, 20 })
        foreach (var shadows in new[] { false, true })
        {
            var indicator = StrictHaramiComparison.Create(name, period, shadows);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "containment",
                    () => StrictHaramiComparison.Create(name, period, shadows)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
            ComparisonVerifier.Check(
                StrictHaramiComparison.Pair(name, shadows),
                StrictHaramiComparison.Fixture(period),
                period
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TupleWrappersRunPlainHaramiInsteadOfDirectionalLogic(bool shadows)
    {
        var data = StrictHaramiComparison.Fixture();
        var input = data.Candles.Select(c => (c.Open, c.High, c.Low, c.Close)).ToArray();
        var expected = new TC.HaramiByTuple(input, shadows).Compute().ToArray();
        Assert.Equal(
            expected,
            new TC.Harami<(decimal, decimal, decimal, decimal), bool?>(
                input,
                x => x,
                shadows
            ).Compute()
        );
        foreach (var period in new[] { 1, 3, 20 })
        {
            Assert.Equal(expected, new TC.BullishHaramiByTuple(input, shadows, period).Compute());
            Assert.Equal(expected, new TC.BearishHaramiByTuple(input, shadows, period).Compute());
        }
        var actual = await Run(new StrictHaramiCandle(shadows), data.IndicatorBars);
        Assert.Null(expected[0]);
        Assert.Equal(expected.Skip(1).Select(v => v!.Value ? 1d : 0), actual.Skip(1));
    }

    [Fact]
    public void GenericDirectionalRoutesIgnoreShadowArgumentAndUseBodyTrend()
    {
        var data = StrictHaramiComparison.Fixture();
        var input = data.Candles.Select(c => (c.Open, c.High, c.Low, c.Close)).ToArray();
        foreach (var name in new[] { "BullishHarami", "BearishHarami" })
        foreach (var period in new[] { 1, 3, 20 })
        {
            var expected = StrictHaramiComparison
                .Pair(name)
                .Competitor(data, period)
                .Outputs["Value"]
                .Values;
            foreach (var shadows in new[] { false, true })
            {
                var values =
                    name == "BullishHarami"
                        ? new TC.BullishHarami<(decimal, decimal, decimal, decimal), bool?>(
                            input,
                            x => x,
                            shadows,
                            period
                        ).Compute()
                        : new TC.BearishHarami<(decimal, decimal, decimal, decimal), bool?>(
                            input,
                            x => x,
                            shadows,
                            period
                        ).Compute();
                Assert.Equal(
                    expected,
                    values.Select(v =>
                        v.HasValue
                            ? v.Value
                                ? 1d
                                : 0
                            : double.NaN
                    )
                );
            }
        }
    }

    [Fact]
    public async Task PublicShadowOptionIsHonoredAndTouchingShadowsFail()
    {
        var data = StrictHaramiComparison.Fixture();
        Assert.Equal(1, (await Run(new BullishHaramiPattern(), data.IndicatorBars))[4]);
        Assert.Equal(
            0,
            (await Run(new BullishHaramiPattern(containedShadows: true), data.IndicatorBars))[4]
        );
        var b = new[] { Make(0, 10, 12, 0, 2), Make(1, 3, 12, 1, 9) };
        Assert.Equal(1, (await Run(new StrictHaramiCandle(), b))[1]);
        Assert.Equal(0, (await Run(new StrictHaramiCandle(true), b))[1]);
        b[1] = Make(1, 3, 11, 0, 9);
        Assert.Equal(0, (await Run(new StrictHaramiCandle(true), b))[1]);
        b[1] = Make(1, 3, 11, 1, 9);
        Assert.Equal(1, (await Run(new StrictHaramiCandle(true), b))[1]);
    }

    [Fact]
    public async Task ExtremeTinyAndEqualTrendEndpoints()
    {
        var m = double.MaxValue;
        var e = double.Epsilon;
        Assert.Equal(
            1,
            (await Run(new StrictHaramiCandle(), [Make(0, m, m, -m, -m), Make(1, -e, e, -e, e)]))[1]
        );
        var bars = new[] { Make(0, 12, 20, 0, 4), Make(1, 10, 21, -1, 2), Make(2, 3, 9, 3, 9) };
        Assert.Equal(1, (await Run(new BullishHaramiPattern(1), bars))[2]);
        bars[0] = Make(0, 10, 20, 0, 4); // equal opens break the trend
        Assert.Equal(0, (await Run(new BullishHaramiPattern(1), bars))[2]);
        bars[0] = Make(0, 12, 20, 0, 2); // equal closes also break it
        Assert.Equal(0, (await Run(new BullishHaramiPattern(1), bars))[2]);
        Assert.Equal(new double[3], await Run(new BullishHaramiPattern(int.MaxValue), bars));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BullishHaramiPattern(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BearishHaramiPattern(0));
    }

    [Fact]
    public void NewFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, StrictHaramiComparison.Fixture(), 3);
    }

    private static Bar Make(int i, double o, double h, double l, double c) =>
        new(DateTime.UnixEpoch.AddDays(i), o, h, l, c, 1);

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
