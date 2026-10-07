using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SmoothedAccumulationComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentRationalContractsCoverLifecycleAndLargePeriods(bool first)
    {
        foreach (var c in new[] { (1, 2), (2, 3), (3, 2), (2, 2), (int.MaxValue, int.MaxValue) })
        foreach (var details in new[] { false, true })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(SmoothedAccumulationOscillator),
                    "smoothed flow",
                    () =>
                        new SmoothedAccumulationOscillator(
                            c.Item1,
                            c.Item2,
                            first,
                            first ? 2 : 0,
                            details
                        )
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void EveryOutputAndStartupMatchesIndependentReferences()
    {
        foreach (var first in new[] { false, true })
        foreach (
            var c in first
                ? new[] { (2, 2), (2, 3), (7, 3), (3, 10) }
                : new[] { (1, 2), (2, 3), (3, 10) }
        )
        foreach (var suppression in first ? new[] { 0, 2 } : new[] { 0 })
        {
            using var settings = new TripleRateComparison.Settings(false, suppression);
            var pair = SmoothedAccumulationComparison.Pair(first, c.Item1, c.Item2, suppression);
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
            ComparisonVerifier.Check(pair, AccumulationDistributionComparison.Fixture(), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 80), 20);
        }
    }

    [Fact]
    public void LinearAccumulationHasHalfUnitDifferenceAndReversingPeriodsNegates()
    {
        var bars = Enumerable
            .Range(0, 12)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 2, 0, 2, 1))
            .ToArray();
        var mean = SmoothedAccumulationComparison.Owned(bars, false, 2, 3).Outputs["Oscillator"];
        Assert.Equal(new[] { false, false }.Concat(Enumerable.Repeat(true, 10)), mean.Present);
        Assert.All(mean.Values.Skip(2), v => Assert.Equal(0.5, v));
        var a = SmoothedAccumulationComparison.Owned(bars, true, 2, 3).Outputs["Oscillator"];
        var b = SmoothedAccumulationComparison.Owned(bars, true, 3, 2).Outputs["Oscillator"];
        Assert.Equal(a.Values.Skip(2).Select(v => -v), b.Values.Skip(2));
        var invalid = bars.Select(b => new Bar(b.Time, 1, 1, 3, 1, 2)).ToArray();
        Assert.All(
            SmoothedAccumulationComparison
                .Owned(invalid, false, 2, 3)
                .Outputs["MoneyFlowVolume"]
                .Values,
            v => Assert.Equal(2, v)
        );
        Assert.All(
            SmoothedAccumulationComparison
                .Owned(invalid, true, 2, 3)
                .Outputs["Oscillator"]
                .Values.Skip(2),
            v => Assert.Equal(0, v)
        );
    }

    [Fact]
    public void HiddenOverflowCanCancelButPublishedOverflowIsRejected()
    {
        foreach (var high in new[] { 2d, double.Epsilon })
        {
            var bars = Enumerable
                .Range(0, 4)
                .Select(i => new Bar(
                    DateTime.UnixEpoch.AddDays(i),
                    1,
                    high,
                    0,
                    high == 2 ? 2 : double.MaxValue,
                    double.MaxValue
                ))
                .ToArray();
            Assert.All(
                SmoothedAccumulationComparison
                    .Owned(bars, true, 2, 2)
                    .Outputs["Oscillator"]
                    .Values.Skip(1),
                v => Assert.Equal(0, v)
            );
            Assert.Throws<IndicatorOutputException>(() =>
                SmoothedAccumulationComparison.Owned(bars, false, 2, 2)
            );
        }
    }

    [Fact]
    public void NativeRangeAliasesFloatAndCompatibilityArePinned()
    {
        var d = CompetitorData.Create(100);
        foreach (var compatibility in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new TripleRateComparison.Settings(compatibility, suppression);
            Assert.Equal(9 + suppression, Functions.AdOscLookback(3, 10));
            var expected = SmoothedAccumulationComparison.NativePacked(
                d.Highs,
                d.Lows,
                d.Closes,
                d.Volumes,
                3,
                10,
                suppression,
                40,
                80
            );
            for (var alias = 0; alias < 4; alias++)
            {
                var inputs = new[]
                {
                    (double[])d.Highs.Clone(),
                    (double[])d.Lows.Clone(),
                    (double[])d.Closes.Clone(),
                    (double[])d.Volumes.Clone(),
                };
                Assert.Equal(
                    TaCore.RetCode.Success,
                    Functions.AdOsc<double>(
                        inputs[0],
                        inputs[1],
                        inputs[2],
                        inputs[3],
                        40..80,
                        inputs[alias],
                        out var range,
                        3,
                        10
                    )
                );
                Assert.Equal(40..81, range);
                Assert.Equal(expected, inputs[alias].Take(41));
            }
            var h = d.Highs.Select(v => (float)v).ToArray();
            var l = d.Lows.Select(v => (float)v).ToArray();
            var c = d.Closes.Select(v => (float)v).ToArray();
            var v = d.Volumes.Select(v => (float)v).ToArray();
            var output = new float[100];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.AdOsc<float>(h, l, c, v, 40..80, output, out var frange, 10, 3)
            );
            Assert.Equal(40..81, frange);
            Assert.Equal(
                SmoothedAccumulationComparison.NativePacked(h, l, c, v, 10, 3, suppression, 40, 80),
                output.Take(41)
            );
        }
    }

    [Fact]
    public void InvalidParametersAndNativeQuoteOrderingAreExplicit()
    {
        using var settings = new TripleRateComparison.Settings(false, 0);
        var d = CompetitorData.Create(40);
        Assert.Equal(
            d.Quotes.GetChaikinOsc().Select(r => r.Oscillator),
            d.Quotes.AsEnumerable().Reverse().GetChaikinOsc().Select(r => r.Oscillator)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetChaikinOsc(0, 10).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Quotes.GetChaikinOsc(3, 3).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmoothedAccumulationOscillator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmoothedAccumulationOscillator(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SmoothedAccumulationOscillator(suppression: 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SmoothedAccumulationOscillator(firstValue: true, suppression: -1)
        );
        Assert.Equal(-1, Functions.AdOscLookback(1, 10));
        var output = new double[40];
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.AdOsc<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                d.Volumes,
                System.Range.All,
                output,
                out _,
                1,
                10
            )
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.AdOsc<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                d.Volumes,
                System.Range.All,
                output,
                out var empty,
                int.MaxValue,
                int.MaxValue
            )
        );
        Assert.Equal(0..0, empty);
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.AdOsc<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                d.Volumes,
                90..100,
                output,
                out _,
                3,
                10
            )
        );
    }

    [Fact]
    public async Task ChainingPreservesCandleFieldsAndMutationsAreDetected()
    {
        using var settings = new TripleRateComparison.Settings(false, 0);
        var d = CompetitorData.Create(60);
        var closes = FixedWeightedComparison.Stage(d.Closes, 3, false);
        var modified = CompetitorData.FromOhlcv(d.Opens, d.Highs, d.Lows, closes, d.Volumes);
        var indicator = new SmoothedAccumulationOscillator(3, 10, includeDetails: true);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = SmoothedAccumulationComparison.Reference(modified, false, 3, 10, 0, false);
        for (var j = 0; j < 4; j++)
        {
            var column = expected.Outputs[SmoothedAccumulationComparison.Names[j]];
            Assert.Equal(
                column.Values.Select((v, i) => column.Present![i] ? v : 0),
                run[indicator.Outputs[j]].ToArray()
            );
            Assert.Equal(
                column.Present!.Select(p => p ? 1d : 0),
                run[indicator.Outputs[j + 4]].ToArray()
            );
        }
        foreach (var first in new[] { false, true })
        {
            var pair = SmoothedAccumulationComparison.Pair(first);
            foreach (
                var name in first ? new[] { "Oscillator" } : SmoothedAccumulationComparison.Names
            )
            foreach (var native in new[] { false, true })
            foreach (var presence in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int p)
                {
                    var r = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                    if (presence)
                        r.Outputs[name].Present![^1] = false;
                    else
                        r.Outputs[name].Values[^1] += 1;
                    return r;
                }
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                        d,
                        20
                    )
                );
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = SmoothedAccumulationComparison.Pair(first, 2, 7).Ooples,
                    },
                    d,
                    20
                )
            );
        }
    }
}
