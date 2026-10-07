using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class TaAdaptiveComparisonTests
{
    [Fact]
    public async Task FastFlatAndDelayedPublicationHaveIndependentLifecycleContracts()
    {
        foreach (var p in new[] { 1, 2, 10 })
        foreach (var suppression in new[] { 0, 2 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(SeededAdaptiveAverage),
                    "TA adaptive counterpart",
                    () =>
                        new SeededAdaptiveAverage(
                            p,
                            resetFlat: false,
                            fastFlat: true,
                            outputDelay: 1L + suppression
                        )
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void CompatibilityIsIgnoredAndSuppressionRetainsAllInternalUpdates()
    {
        foreach (var compatibility in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var emaSettings = new TripleRateComparison.Settings(compatibility, 7);
            using var settings = new TaAdaptiveComparison.Settings(suppression);
            var pair = TaAdaptiveComparison.Pair(suppression);
            foreach (var p in new[] { 2, 3, 10, 30 })
            {
                Assert.Equal(p + suppression, Functions.KamaLookback(p));
                ComparisonVerifier.Check(pair, CompetitorData.Create(90), p);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 80), p);
            }
        }
        var d = CompetitorData.FromCloses([0, 10, 0, 0, 0, 0, 0]);
        var fast = TaAdaptiveComparison.Owned(d.IndicatorBars, 2, 0).Outputs["Kama"];
        var slow = TradyAdaptiveComparison.Owned(d.IndicatorBars, 2, 2, 30).Outputs["Kama"];
        Assert.Equal(new[] { false, false, true, true, true, true, true }, fast.Present);
        Assert.True(fast.Values[4] < slow.Values[4]);
        var delayed = TaAdaptiveComparison.Owned(d.IndicatorBars, 2, 2).Outputs["Kama"];
        Assert.Equal(new[] { false, false, false, false, true, true, true }, delayed.Present);
        Assert.Equal(fast.Values.Skip(4), delayed.Values.Skip(4));
    }

    [Fact]
    public void NativeSubrangesFloatAndInputAliasesUseIndependentRollingStages()
    {
        var d = CompetitorData.Create(100);
        foreach (var p in new[] { 2, 3, 20 })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var start in new[] { 0, 40 })
        {
            using var settings = new TaAdaptiveComparison.Settings(suppression);
            var expected = TaAdaptiveComparison.NativePacked(d.Closes, p, suppression, start, 80);
            var input = (double[])d.Closes.Clone();
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Kama<double>(input, start..80, input, out var range, p)
            );
            Assert.Equal(Math.Max(start, p + suppression)..81, range);
            Assert.Equal(expected, input.Take(expected.Length));
            var f = d.Closes.Select(v => (float)v).ToArray();
            var output = new float[100];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Kama<float>(f, start..80, output, out var frange, p)
            );
            Assert.Equal(range, frange);
            Assert.Equal(
                TaAdaptiveComparison.NativePacked(f, p, suppression, start, 80),
                output.Take(expected.Length)
            );
        }
    }

    [Fact]
    public void NativeBoundariesAndCounterpartDelayOverflowAreExplicit()
    {
        using var settings = new TaAdaptiveComparison.Settings(0);
        var d = CompetitorData.Create(30);
        var output = new double[30];
        Assert.Equal(-1, Functions.KamaLookback(1));
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Kama<double>(d.Closes, System.Range.All, output, out _, 1)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Kama<double>(d.Closes, System.Range.All, output, out var empty, int.MaxValue)
        );
        Assert.Equal(0..0, empty);
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Kama<double>(d.Closes, 40..50, output, out _, 2)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Kama<double>(new[] { 1d }, System.Range.All, output, out _, 2)
        );
        using (var overflow = new TaAdaptiveComparison.Settings(2))
        {
            Assert.True(Functions.KamaLookback(int.MaxValue) < 0);
            Assert.Throws<IndexOutOfRangeException>(() =>
                Functions.Kama<double>(d.Closes, System.Range.All, output, out _, int.MaxValue)
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededAdaptiveAverage(outputDelay: -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededAdaptiveAverage(2, outputDelay: long.MaxValue)
        );
        var late = new SeededAdaptiveAverage(1, outputDelay: long.MaxValue);
        Assert.Equal(int.MaxValue, late.WarmupBars);
        var bounded = TaAdaptiveComparison.Owned(d.IndicatorBars, int.MaxValue, int.MaxValue);
        Assert.All(bounded.Outputs["Kama"].Present!, p => Assert.False(p));
    }

    [Fact]
    public async Task ChainingFlatEfficiencyAndDelayMutationsAreDetected()
    {
        using var settings = new TaAdaptiveComparison.Settings(2);
        var d = CompetitorData.Create(60);
        var indicator = new SeededAdaptiveAverage(
            3,
            resetFlat: false,
            fastFlat: true,
            outputDelay: 3
        );
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = SeededAdaptiveComparison.ReferenceValues(
            FixedWeightedComparison.Stage(d.Closes, 3, false),
            3,
            2,
            30,
            false,
            false,
            false,
            true,
            3
        );
        for (var j = 0; j < 2; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 2]].ToArray()
            );
        }
        var pair = TaAdaptiveComparison.Pair(2);
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int p)
            {
                var r = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                if (presence)
                    r.Outputs["Kama"].Present![^1] = false;
                else
                    r.Outputs["Kama"].Values[^1] += 1;
                return r;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    d,
                    3
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = TaAdaptiveComparison.Pair(0).Ooples,
                },
                d,
                3
            )
        );
    }
}
