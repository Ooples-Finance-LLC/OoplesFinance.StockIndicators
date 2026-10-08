using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using TaCore = TALib.Core;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("TA MACD settings", DisableParallelization = true)]
public sealed class AlignedMacdSettingsCollection { }

[Collection("TA MACD settings")]
public sealed class AlignedMacdComparisonTests
{
    private static Bar[] BarsOf(params double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RationalContractsCoverLifecycleAndBoundedStorage(
        bool fixedCoefficients,
        bool first
    )
    {
        foreach (
            var c in fixedCoefficients
                ? new[]
                {
                    (12, 26, 1, 0),
                    (26, 12, 3, 2),
                    (12, 26, int.MaxValue, 0),
                    (12, 26, 3, int.MaxValue),
                }
                : new[]
                {
                    (2, 5, 2, 0),
                    (5, 2, 3, 2),
                    (2, 2, 1, 0),
                    (int.MaxValue, int.MaxValue, 2, 0),
                    (2, int.MaxValue, int.MaxValue, 0),
                    (3, 5, 3, int.MaxValue),
                }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(AlignedEmaMacd),
                    "aligned MACD",
                    () =>
                        new AlignedEmaMacd(
                            c.Item1,
                            c.Item2,
                            c.Item3,
                            first,
                            c.Item4,
                            fixedCoefficients
                        )
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void AllOutputsAndConfigurationsMatchGridAndNativeStages()
    {
        foreach (var fixedCoefficients in new[] { false, true })
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (
            var c in fixedCoefficients
                ? new[] { (12, 26, 2), (12, 26, 9) }
                : new[] { (2, 2, 2), (3, 7, 3), (7, 3, 4), (12, 26, 9) }
        )
        {
            using var settings = new TripleRateComparison.Settings(first, suppression);
            var pair = AlignedMacdComparison.Pair(
                fixedCoefficients,
                c.Item1,
                c.Item2,
                c.Item3,
                first,
                suppression
            );
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 90), 20);
        }
    }

    [Fact]
    public void MeanSeedAlignmentStartupAndFixedCoefficientsAreDistinct()
    {
        var prices = new[]
        {
            2d,
            9,
            1,
            5,
            7,
            11,
            3,
            8,
            12,
            4,
            9,
            13,
            5,
            2,
            8,
            11,
            15,
            3,
            8,
            5,
            12,
            6,
            9,
            13,
            8,
            4,
            11,
            16,
            7,
            12,
            9,
            5,
            13,
            18,
            10,
            4,
            7,
            11,
        };
        var rows = AlignedMacdComparison.Owned(BarsOf(prices), 2, 4, 2, false, 0, false).Outputs;
        Assert.All(rows.Values, r => Assert.Equal(prices.Select((_, i) => i >= 4), r.Present));
        var ordinary = EmaDifferenceSignalComparison.Owned(BarsOf(prices), 0, 2, 4, 2);
        Assert.NotEqual(ordinary.Outputs["Macd"].Values[4], rows["Macd"].Values[4]);
        var fixedRows = AlignedMacdComparison
            .Owned(BarsOf(prices), 12, 26, 3, false, 0, true)
            .Outputs;
        var variable = AlignedMacdComparison
            .Owned(BarsOf(prices), 12, 26, 3, false, 0, false)
            .Outputs;
        Assert.NotEqual(variable["Macd"].Values[^1], fixedRows["Macd"].Values[^1]);
        var delayed = AlignedMacdComparison.Owned(BarsOf(prices), 2, 4, 3, true, 2, false).Outputs;
        Assert.All(delayed.Values, r => Assert.Equal(prices.Select((_, i) => i >= 9), r.Present));
        var reversed = AlignedMacdComparison.Owned(BarsOf(prices), 4, 2, 2, false, 0, false);
        ComparisonVerifier.Compare(
            new ComparisonSeries(rows),
            reversed,
            "period sorting",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public void HiddenOverflowCancelsAndOnlySelectedOutputsAreRejected()
    {
        var prices = Enumerable.Repeat(-double.MaxValue, 19).Append(double.MaxValue).ToArray();
        var bars = BarsOf(prices);
        var result = AlignedMacdComparison
            .Owned(bars, 2, 20, 1, true, 0, false, EmaDifferenceSelection.Histogram)
            .Outputs;
        Assert.All(result["Macd"].Present!, v => Assert.False(v));
        Assert.All(result["Signal"].Present!, v => Assert.False(v));
        Assert.True(result["Histogram"].Present![19]);
        Assert.Equal(0, result["Histogram"].Values[19]);
        Assert.Throws<IndicatorOutputException>(() =>
            AlignedMacdComparison.Owned(bars, 2, 20, 1, true, 0, false)
        );
        foreach (var first in new[] { false, true })
        foreach (var fixedCoefficients in new[] { false, true })
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var constant = AlignedMacdComparison
                .Owned(
                    BarsOf(Enumerable.Repeat(value, 40).ToArray()),
                    fixedCoefficients ? 12 : 2,
                    fixedCoefficients ? 26 : 4,
                    2,
                    first,
                    0,
                    fixedCoefficients
                )
                .Outputs;
            Assert.All(
                constant.Values,
                r => Assert.All(r.Values.Where((_, i) => r.Present![i]), v => Assert.Equal(0, v))
            );
        }
    }

    [Fact]
    public void SubrangesAliasingAndFloatArithmeticHaveCompleteNativeReferences()
    {
        var data = CompetitorData.Create(120);
        foreach (var fixedCoefficients in new[] { false, true })
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new TripleRateComparison.Settings(first, suppression);
            var fast = fixedCoefficients ? 12 : 3;
            var slow = fixedCoefficients ? 26 : 7;
            const int signal = 3;
            var output = new[] { new double[120], new double[120], new double[120] };
            var expected = AlignedMacdComparison.NativePacked(
                data.Closes,
                fast,
                slow,
                signal,
                first,
                suppression,
                fixedCoefficients,
                50,
                95
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                AlignedMacdComparison.Invoke(
                    data.Closes,
                    50..95,
                    output,
                    out var range,
                    fast,
                    slow,
                    signal,
                    fixedCoefficients
                )
            );
            Assert.Equal(50..96, range);
            for (var j = 0; j < 3; j++)
                Assert.Equal(expected[j], output[j].Take(46));
            var lookback = slow + signal - 2 + 2 * suppression;
            Assert.Equal(
                lookback,
                fixedCoefficients
                    ? Functions.MacdFixLookback(signal)
                    : Functions.MacdLookback(fast, slow, signal)
            );
            var full = AlignedMacdComparison.NativePacked(
                data.Closes,
                fast,
                slow,
                signal,
                first,
                suppression,
                fixedCoefficients,
                0,
                119
            );
            for (var aliasSlot = 0; aliasSlot < 3; aliasSlot++)
            {
                var input = (double[])data.Closes.Clone();
                var aliased = new[] { new double[120], new double[120], new double[120] };
                aliased[aliasSlot] = input;
                Assert.Equal(
                    TaCore.RetCode.Success,
                    AlignedMacdComparison.Invoke(
                        input,
                        System.Range.All,
                        aliased,
                        out var all,
                        fast,
                        slow,
                        signal,
                        fixedCoefficients
                    )
                );
                Assert.Equal(lookback..120, all);
                for (var j = 0; j < 3; j++)
                    Assert.Equal(full[j], aliased[j].Take(120 - lookback));
            }
            var floats = data.Closes.Select(v => (float)v).ToArray();
            var actual = new[] { new float[120], new float[120], new float[120] };
            var reference = AlignedMacdComparison.NativePacked(
                floats,
                fast,
                slow,
                signal,
                first,
                suppression,
                fixedCoefficients,
                50,
                95
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                AlignedMacdComparison.Invoke(
                    floats,
                    50..95,
                    actual,
                    out var frange,
                    fast,
                    slow,
                    signal,
                    fixedCoefficients
                )
            );
            Assert.Equal(50..96, frange);
            for (var j = 0; j < 3; j++)
                Assert.Equal(reference[j], actual[j].Take(46));
        }
    }

    [Fact]
    public void NativeSignalOneAndOverflowedLookbacksAreExplicitFailures()
    {
        using var settings = new TripleRateComparison.Settings(false, 0);
        var data = CompetitorData.Create(80);
        var output = new[] { new double[80], new double[80], new double[80] };
        Assert.Equal(24, Functions.MacdFixLookback(1));
        Assert.Equal(24, Functions.MacdFixLookback(0));
        Assert.Equal(4, Functions.MacdLookback(3, 6, 1));
        Assert.Equal(-1, Functions.MacdLookback(1, 6, 3));
        foreach (var fixedCoefficients in new[] { false, true })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AlignedMacdComparison.Invoke(
                    data.Closes,
                    System.Range.All,
                    output,
                    out _,
                    12,
                    26,
                    1,
                    fixedCoefficients
                )
            );
            Assert.Equal(
                TaCore.RetCode.BadParam,
                AlignedMacdComparison.Invoke(
                    data.Closes,
                    System.Range.All,
                    output,
                    out _,
                    12,
                    26,
                    0,
                    fixedCoefficients
                )
            );
            Assert.Equal(
                TaCore.RetCode.OutOfRangeParam,
                AlignedMacdComparison.Invoke(
                    new[] { 1d },
                    System.Range.All,
                    new[] { new double[1], new double[1], new double[1] },
                    out _,
                    12,
                    26,
                    3,
                    fixedCoefficients
                )
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                AlignedMacdComparison.Invoke(
                    new[] { 1d, 2 },
                    System.Range.All,
                    new[] { new double[2], new double[2], new double[2] },
                    out var empty,
                    12,
                    26,
                    3,
                    fixedCoefficients
                )
            );
            Assert.Equal(0..0, empty);
            var f = fixedCoefficients ? 12 : int.MaxValue;
            var s = fixedCoefficients ? 26 : int.MaxValue;
            Assert.Throws<OverflowException>(() =>
                AlignedMacdComparison.Invoke(
                    data.Closes,
                    System.Range.All,
                    output,
                    out _,
                    f,
                    s,
                    int.MaxValue,
                    fixedCoefficients
                )
            );
        }
        Assert.Equal(-4, Functions.MacdLookback(int.MaxValue, int.MaxValue, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedEmaMacd(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedEmaMacd(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedEmaMacd(signalPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedEmaMacd(suppression: -1));
        Assert.Throws<ArgumentException>(() => new AlignedEmaMacd(2, 4, fixedCoefficients: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AlignedEmaMacd(selection: EmaDifferenceSelection.FastAverage)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlignedEmaMacd(selection: 0));
    }

    [Fact]
    public async Task ChainingAndEveryOutputPresenceAndSeedMutation()
    {
        var data = CompetitorData.Create(80);
        var prices = FixedWeightedComparison.Stage(data.Closes, 3, false);
        foreach (var fixedCoefficients in new[] { false, true })
        {
            var fast = fixedCoefficients ? 12 : 3;
            var slow = fixedCoefficients ? 26 : 7;
            var indicator = new AlignedEmaMacd(fast, slow, 3, false, 1, fixedCoefficients);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = AlignedMacdComparison.Reference(
                prices,
                fast,
                slow,
                3,
                false,
                1,
                fixedCoefficients
            );
            for (var j = 0; j < 3; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[j + 3]].ToArray()
                );
            }
            using var settings = new TripleRateComparison.Settings(false, 0);
            var pair = AlignedMacdComparison.Pair(fixedCoefficients, fast, slow, 3);
            foreach (var name in AlignedMacdComparison.Names)
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData d, int p)
                {
                    var r = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                    if (presence)
                        r.Outputs[name].Present![^1] = false;
                    else
                        r.Outputs[name].Values[^1] += 1;
                    return r;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        data,
                        20
                    )
                );
            }
            foreach (
                var wrong in new[]
                {
                    AlignedMacdComparison.Pair(fixedCoefficients, fast, slow, 4),
                    AlignedMacdComparison.Pair(fixedCoefficients, fast, slow, 3, true),
                    AlignedMacdComparison.Pair(fixedCoefficients, fast, slow, 3, false, 1),
                }
            )
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(pair with { Library = wrong.Ooples }, data, 20)
                );
            if (fixedCoefficients)
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        pair with
                        {
                            Library = AlignedMacdComparison.Pair(false, fast, slow, 3).Ooples,
                        },
                        data,
                        20
                    )
                );
        }
    }
}
