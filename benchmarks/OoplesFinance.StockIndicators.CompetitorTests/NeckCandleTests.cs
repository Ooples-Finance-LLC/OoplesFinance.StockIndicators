using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class NeckCandleTests
{
    public static IEnumerable<object[]> Names =>
        NeckCandleComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Names))]
    public void GoldenBoundariesGapColorAndWhiteDoji(string name)
    {
        var data = NeckCandleComparison.Fixture();
        var pair = ComparisonPairs.Get("TaLib.Candles." + name);
        foreach (
            var output in new[]
            {
                pair.Competitor(data, 20),
                pair.Ooples(data, 20),
                pair.Reference!(data, 20),
            }
        )
            for (var i = 0; i < NeckCandleComparison.Candidates.Length; i++)
            {
                var match = name switch
                {
                    "OnNeck" => i is 1 or 2 or 3 or 11,
                    "InNeck" => i is 5 or 6,
                    _ => i is 7 or 8,
                };
                Assert.Equal(match ? -100 : 0, output.Outputs["Value"].Values[i * 12 + 11]);
            }
        ComparisonVerifier.Check(pair, data, 20);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferenceLifecycleAndUnequalWindows(string name)
    {
        foreach (var (body, equal) in new[] { (1, 1), (1, 3), (3, 1), (10, 5) })
        {
            var indicator = NeckCandleComparison.Create(name, body, equal);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "windows",
                    () => NeckCandleComparison.Create(name, body, equal)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task AnchorRangeDoesNotEnterToleranceAndBodyEqualityIsNotLong(string name)
    {
        var close = name == "OnNeck" ? 1d : 3d;
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 4, 10, 0, 6))
            .Append(Make(10, 8, 1000, 0, 2))
            .Append(Make(11, -2, 10, -3, close))
            .ToArray();
        Assert.Equal(
            name == "Thrusting" ? -100 : 0,
            (await Run(NeckCandleComparison.Create(name), bars))[^1]
        );
        // First satisfy the close rules, so only the body threshold can reject the next case.
        bars[^1] = Make(
            11,
            -2,
            10,
            -3,
            name == "OnNeck" ? 0
                : name == "InNeck" ? 2
                : 3
        );
        Assert.Equal(-100, (await Run(NeckCandleComparison.Create(name), bars))[^1]);
        // Set every historical body equal to the anchor: strict longness must fail.
        for (var i = 0; i < 10; i++)
            bars[i] = Make(i, 8, 10, 0, 2);
        Assert.Equal(0, (await Run(NeckCandleComparison.Create(name), bars))[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task UnrepresentableRangesBodiesAndSubnormalTolerance(string name)
    {
        var m = double.MaxValue;
        var close = name == "Thrusting" ? 0 : -m / 2;
        var bars = new[]
        {
            Make(0, 0, m, -m, 0),
            Make(1, m, m, -m / 2, -m / 2),
            Make(2, -m, 0, -m, close),
        };
        Assert.Equal(-100, (await Run(NeckCandleComparison.Create(name, 1, 1), bars))[^1]);
        var u = 8 * double.Epsilon;
        close =
            (
                name == "OnNeck" ? -.5
                : name == "InNeck" ? 2.5
                : 5
            ) * u;
        bars = Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 4 * u, 10 * u, 0, 6 * u))
            .Append(Make(10, 8 * u, 10 * u, 0, 2 * u))
            .Append(Make(11, -2 * u, 10 * u, -3 * u, close))
            .ToArray();
        Assert.Equal(-100, (await Run(NeckCandleComparison.Create(name), bars))[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ParameterBoundsAndLazyWindowAllocation(string name)
    {
        foreach (var invalid in new[] { -1, 0, int.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NeckCandleComparison.Create(name, invalid, 1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NeckCandleComparison.Create(name, 1, invalid)
            );
        }
        var bars = new[] { Make(0, 4, 10, 0, 6) };
        Assert.Equal(
            new double[1],
            await Run(NeckCandleComparison.Create(name, int.MaxValue - 1, 1), bars)
        );
        Assert.Equal(
            new double[1],
            await Run(NeckCandleComparison.Create(name, 1, int.MaxValue - 1), bars)
        );
    }

    [Fact]
    public void PinnedLookbacksAndToleranceSettings()
    {
        Assert.Equal(11, TALib.Candles.OnNeckLookback());
        Assert.Equal(11, TALib.Candles.InNeckLookback());
        Assert.Equal(11, TALib.Candles.ThrustingLookback());
        Assert.Equal(
            5,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Equal).AveragePeriod
        );
        Assert.Equal(.05, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Equal).Factor);
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, NeckCandleComparison.Fixture(), 20);
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
