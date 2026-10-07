using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class DelayedPhaseComparisonTests
{
    [Theory]
    [InlineData(.5, .05, 0)]
    [InlineData(.05, .5, 2)]
    public async Task RationalContractsCoverDelayedLifecycleAndReversedLimits(
        double fast,
        double slow,
        int suppression
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(DelayedPhaseAdaptiveAverage),
                "delayed phase",
                () => new DelayedPhaseAdaptiveAverage(fast, slow, suppression)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void AllOutputsLimitOrdersAndSuppressionMatchIndependentModels()
    {
        foreach (var suppression in new[] { 0, 2 })
        foreach (var c in new[] { (.5, .05), (.8, .1), (.05, .5), (.01, .99), (.99, .01) })
        {
            using var settings = new DelayedPhaseComparison.Settings(suppression);
            var pair = DelayedPhaseComparison.Pair(c.Item1, c.Item2, suppression);
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 80), 20);
        }
        var d = CompetitorData.Create(60);
        var baseRows = DelayedPhaseComparison.Owned(d.IndicatorBars, .5, .05, 0).Outputs;
        var delayed = DelayedPhaseComparison.Owned(d.IndicatorBars, .5, .05, 2).Outputs;
        foreach (var name in SeededPhaseComparison.Names)
        {
            Assert.Equal(Enumerable.Range(0, 60).Select(i => i >= 32), baseRows[name].Present);
            Assert.Equal(Enumerable.Range(0, 60).Select(i => i >= 34), delayed[name].Present);
            Assert.Equal(baseRows[name].Values.Skip(34), delayed[name].Values.Skip(34));
        }
    }

    [Fact]
    public void SubrangesAliasesFloatAndCompatibilityPreserveNativeStages()
    {
        var d = CompetitorData.Create(120);
        foreach (var compatibility in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var start in new[] { 0, 51 })
        {
            using var unrelated = new TripleRateComparison.Settings(compatibility, 9);
            using var settings = new DelayedPhaseComparison.Settings(suppression);
            Assert.Equal(32 + suppression, Functions.MamaLookback());
            var expected = DelayedPhaseComparison.NativePacked(
                d.Closes,
                .5,
                .05,
                suppression,
                start,
                100
            );
            for (var slot = 0; slot < 2; slot++)
            {
                var input = (double[])d.Closes.Clone();
                var output = new[] { new double[120], new double[120] };
                output[slot] = input;
                Assert.Equal(
                    TaCore.RetCode.Success,
                    Functions.Mama<double>(input, start..100, output[0], output[1], out var range)
                );
                Assert.Equal(Math.Max(start, 32 + suppression)..101, range);
                for (var j = 0; j < 2; j++)
                    Assert.Equal(expected[j], output[j].Take(expected[j].Length));
            }
            var f = d.Closes.Select(v => (float)v).ToArray();
            var floats = new[] { new float[120], new float[120] };
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Mama<float>(f, start..100, floats[0], floats[1], out _, .05, .5)
            );
            var oracle = DelayedPhaseComparison.NativePacked(f, .05, .5, suppression, start, 100);
            for (var j = 0; j < 2; j++)
                Assert.Equal(oracle[j], floats[j].Take(oracle[j].Length));
        }
    }

    [Fact]
    public void NativeParameterLookbackAndNonfiniteLimitBoundariesArePinned()
    {
        using var settings = new DelayedPhaseComparison.Settings(0);
        var d = CompetitorData.Create(80);
        var a = new double[80];
        var b = new double[80];
        foreach (var c in new[] { (0d, .05), (1d, .05), (.5, 0d), (.5, 1d) })
            Assert.Equal(
                TaCore.RetCode.BadParam,
                Functions.Mama<double>(d.Closes, System.Range.All, a, b, out _, c.Item1, c.Item2)
            );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Mama<double>(new[] { 1d }, System.Range.All, a, b, out _)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Mama<double>(d.Closes, 90..100, a, b, out _)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mama<double>(d.Closes, 0..20, a, b, out var empty)
        );
        Assert.Equal(0..0, empty);
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mama<double>(
                d.Closes,
                System.Range.All,
                a,
                b,
                out var nanRange,
                double.NaN,
                .05
            )
        );
        Assert.All(
            a.Take(nanRange.End.Value - nanRange.Start.Value),
            v => Assert.True(double.IsNaN(v))
        );
        using (var overflow = new DelayedPhaseComparison.Settings(int.MaxValue))
        {
            Assert.True(Functions.MamaLookback() < 0);
            Assert.Throws<IndexOutOfRangeException>(() =>
                Functions.Mama<double>(d.Closes, System.Range.All, a, b, out _)
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DelayedPhaseAdaptiveAverage(double.NaN)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedPhaseAdaptiveAverage(.001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DelayedPhaseAdaptiveAverage(slowLimit: 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DelayedPhaseAdaptiveAverage(suppression: -1)
        );
        Assert.Equal(
            int.MaxValue,
            new DelayedPhaseAdaptiveAverage(suppression: int.MaxValue).WarmupBars
        );
        Assert.All(
            DelayedPhaseComparison.Owned(d.IndicatorBars, .5, .05, int.MaxValue).Outputs.Values,
            v => Assert.All(v.Present!, p => Assert.False(p))
        );
    }

    [Fact]
    public void ExtremeFilterInputsKeepOwnedOutputsFinite()
    {
        foreach (
            var prices in new[]
            {
                Enumerable.Repeat(double.MaxValue, 45).ToArray(),
                Enumerable
                    .Range(0, 45)
                    .Select(i => i % 2 == 0 ? double.MaxValue : -double.MaxValue)
                    .ToArray(),
                Enumerable.Range(0, 45).Select(i => (i % 3 - 1) * double.Epsilon).ToArray(),
            }
        )
        {
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            ComparisonVerifier.Compare(
                SeededPhaseComparison.Series(
                    SeededPhaseComparison.GridReference(prices, .5, .05, false, true)
                ),
                DelayedPhaseComparison.Owned(bars, .5, .05, 0),
                "delayed extreme",
                IndicatorErrorBudget.Exact
            );
        }
    }

    [Fact]
    public async Task ChainingBothMasksAndWrongDelayOrLimitsAreDetected()
    {
        using var settings = new DelayedPhaseComparison.Settings(2);
        var d = CompetitorData.Create(80);
        var indicator = new DelayedPhaseAdaptiveAverage(.5, .05, 2);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = SeededPhaseComparison.GridReference(
            FixedWeightedComparison.Stage(d.Closes, 3, false),
            .5,
            .05,
            false,
            true,
            2
        );
        for (var j = 0; j < 2; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 2]].ToArray()
            );
        }
        var pair = DelayedPhaseComparison.Pair(suppression: 2);
        foreach (var name in SeededPhaseComparison.Names)
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
        foreach (
            var wrong in new[]
            {
                DelayedPhaseComparison.Pair(suppression: 0),
                DelayedPhaseComparison.Pair(.8, .1, 2),
            }
        )
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair with { Library = wrong.Ooples }, d, 20)
            );
    }
}
