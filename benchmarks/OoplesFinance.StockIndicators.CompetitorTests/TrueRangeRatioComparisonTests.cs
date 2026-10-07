using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TrueRangeRatioComparisonTests
{
    private static Bar[] Raw(double[] closes, double[]? highs = null, double[]? lows = null) =>
        closes
            .Select(
                (v, i) =>
                    new Bar(DateTime.UnixEpoch.AddDays(i), v, highs?[i] ?? v, lows?[i] ?? v, v, 0)
            )
            .ToArray();

    [Theory]
    [InlineData(true, false, 1, 2, 3)]
    [InlineData(true, false, 3, 2, 3)]
    [InlineData(true, false, int.MaxValue, 2, 3)]
    [InlineData(false, false, 1, 2, 3)]
    [InlineData(false, true, 2, 2, 2)]
    [InlineData(false, true, 4, 1, 3)]
    [InlineData(false, true, 1, 1, 1)]
    [InlineData(false, false, 1, 2, int.MaxValue)]
    public async Task IndependentLifecycleContracts(bool vortex, bool ta, int a, int b, int c)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                vortex ? typeof(WindowVortex) : typeof(WindowUltimateOscillator),
                "true-range ratios",
                () => vortex ? new WindowVortex(a) : new WindowUltimateOscillator(a, b, c, ta)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KnownRatiosStartupAndDistinctZeroPolicies()
    {
        var vortex = TrueRangeRatioComparison
            .OwnedVortex(Raw([1, 2, 3], [2, 3, 4], [0, 1, 2]), 2)
            .Outputs;
        Assert.Equal(new[] { false, false, true }, vortex["Positive"].Present);
        Assert.Equal(1.5, vortex["Positive"].Values[2]);
        Assert.Equal(.5, vortex["Negative"].Values[2]);
        var flat = Raw([1, 1, 1, 1], [2, 2, 2, 2], [0, 0, 0, 0]);
        foreach (var ta in new[] { false, true })
            Assert.Equal(
                50,
                TrueRangeRatioComparison.OwnedUltimate(flat, [1, 2, 3], ta).Outputs["Value"].Values[
                    3
                ]
            );
        var zero = Raw([0, 0, 0, 0]);
        Assert.False(TrueRangeRatioComparison.OwnedVortex(zero, 2).Outputs["Positive"].Present![3]);
        Assert.False(
            TrueRangeRatioComparison
                .OwnedUltimate(zero, [1, 2, 3], false)
                .Outputs["Value"]
                .Present![3]
        );
        Assert.True(
            TrueRangeRatioComparison.OwnedUltimate(zero, [1, 2, 3], true).Outputs["Value"].Present![
                3
            ]
        );
        var recovery = Raw([0, 1, 1, 1, 1]);
        Assert.False(
            TrueRangeRatioComparison
                .OwnedUltimate(recovery, [1, 2, 3], false)
                .Outputs["Value"]
                .Present![3]
        );
        Assert.Equal(
            100d / 7,
            TrueRangeRatioComparison
                .OwnedUltimate(recovery, [1, 2, 3], true)
                .Outputs["Value"]
                .Values[3]
        );
        Assert.Equal(
            0,
            TrueRangeRatioComparison
                .OwnedUltimate(recovery, [1, 2, 3], true)
                .Outputs["Value"]
                .Values[4]
        );
    }

    [Fact]
    public void IndependentTinyWideAndMaximumPeriodReferences()
    {
        var max = double.MaxValue;
        foreach (
            var bars in new[]
            {
                Raw([0, 0, 0, 0], [max, max, max, max], [-max, -max, -max, -max]),
                Raw(
                    [0, double.Epsilon, 0, double.Epsilon],
                    [double.Epsilon, 2 * double.Epsilon, double.Epsilon, 2 * double.Epsilon],
                    [0, 0, 0, 0]
                ),
                Raw([1, Math.BitIncrement(1), 1, Math.BitDecrement(1)]),
            }
        )
        {
            foreach (var p in new[] { 1, 2, 3, int.MaxValue })
                ComparisonVerifier.Compare(
                    TrueRangeRatioComparison.VortexSeries(
                        TrueRangeRatioComparison.VortexReference(bars, p, false)
                    ),
                    TrueRangeRatioComparison.OwnedVortex(bars, p),
                    "Vortex extremes",
                    IndicatorErrorBudget.Exact
                );
            foreach (var ta in new[] { false, true })
            foreach (var periods in new[] { new[] { 1, 2, 3 }, new[] { 1, 2, int.MaxValue } })
                ComparisonVerifier.Compare(
                    VolumePriceComparison.Mask(
                        TrueRangeRatioComparison.UltimateReference(bars, periods, ta, false)
                    ),
                    TrueRangeRatioComparison.OwnedUltimate(bars, periods, ta),
                    "Ultimate extremes",
                    IndicatorErrorBudget.Exact
                );
        }
        var sorted = new WindowUltimateOscillator(5, 2, 2, true);
        Assert.Equal(2, sorted.ShortPeriod);
        Assert.Equal(2, sorted.MiddlePeriod);
        Assert.Equal(5, sorted.LongPeriod);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowVortex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowUltimateOscillator(0));
        Assert.Throws<ArgumentException>(() => new WindowUltimateOscillator(2, 2, 3));
    }

    [Fact]
    public void SkenderSortingDefaultsAndParameterBoundaries()
    {
        var data = CompetitorData.Create(50);
        Assert.Equal(
            data.Quotes.GetVortex(3).Select(r => (r.Date, r.Pvi, r.Nvi)),
            data.Quotes.AsEnumerable().Reverse().GetVortex(3).Select(r => (r.Date, r.Pvi, r.Nvi))
        );
        Assert.Equal(
            data.Quotes.GetUltimate().Select(r => (r.Date, r.Ultimate)),
            data.Quotes.AsEnumerable()
                .Reverse()
                .GetUltimate(7, 14, 28)
                .Select(r => (r.Date, r.Ultimate))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetVortex(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetUltimate(2, 2, 3).ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetUltimate(3, 2, 1).ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetUltimate(0, 2, 3).ToArray()
        );
        Assert.All(data.Quotes.GetVortex(int.MaxValue), r => Assert.Null(r.Pvi));
        Assert.All(data.Quotes.GetUltimate(1, 2, int.MaxValue), r => Assert.Null(r.Ultimate));
        ComparisonVerifier.Check(TrueRangeRatioComparison.VortexPair, data, 3);
        ComparisonVerifier.Check(TrueRangeRatioComparison.UltimatePair(false, 2, 3), data, 1);
    }

    [Fact]
    public void TaSortedDuplicatePeriodsRangesAliasingAndFloatRoutes()
    {
        var data = CompetitorData.Create(40);
        var packed = new double[40];
        foreach (
            var p in new[]
            {
                new[] { 1, 2, 3 },
                new[] { 3, 1, 2 },
                new[] { 2, 2, 4 },
                new[] { 4, 2, 2 },
                new[] { 2, 2, 2 },
            }
        )
        {
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.UltOsc<double>(
                    data.Highs,
                    data.Lows,
                    data.Closes,
                    System.Range.All,
                    packed,
                    out var range,
                    p[0],
                    p[1],
                    p[2]
                )
            );
            Assert.Equal(new System.Range(p.Max(), 40), range);
            Assert.Equal(
                TrueRangeRatioComparison
                    .UltimateReference(data.IndicatorBars, p, true, true)
                    .Skip(p.Max())
                    .Select(v => v!.Value),
                packed.Take(40 - p.Max())
            );
            ComparisonVerifier.Check(
                TrueRangeRatioComparison.UltimatePair(true, p[1], p[2]),
                data,
                p[0]
            );
        }
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.UltOsc<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                4..10,
                packed,
                out var subrange,
                1,
                2,
                3
            )
        );
        Assert.Equal(4..11, subrange);
        Assert.Equal(
            TrueRangeRatioComparison
                .UltimateReference(
                    data.IndicatorBars.Skip(1).Take(10).ToArray(),
                    [1, 2, 3],
                    true,
                    true
                )
                .Skip(3)
                .Select(v => v!.Value),
            packed.Take(7)
        );
        var alias = (double[])data.Closes.Clone();
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.UltOsc<double>(
                data.Highs,
                data.Lows,
                alias,
                System.Range.All,
                alias,
                out _,
                1,
                2,
                3
            )
        );
        Assert.Equal(
            TrueRangeRatioComparison
                .UltimateReference(data.IndicatorBars, [1, 2, 3], true, true)
                .Skip(3)
                .Select(v => v!.Value),
            alias.Take(37)
        );
        float[] h = [2, 2, 2, 2],
            l = [0, 0, 0, 0],
            c = [1, 1, 1, 1],
            f = new float[4];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.UltOsc<float>(h, l, c, System.Range.All, f, out _, 1, 2, 3)
        );
        Assert.Equal(50, f[0]);
        Assert.Equal(28, Functions.UltOscLookback());
        Assert.Equal(-1, Functions.UltOscLookback(0, 2, 3));
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.UltOsc<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out _,
                0,
                2,
                3
            )
        );
    }

    [Fact]
    public void NativeAllOneRangeOverflowAndDecimalLimitsRemainVisible()
    {
        double[] h = [2, 2, 2, 2],
            l = [0, 0, 0, 0],
            c = [1, 1, 1, 1],
            output = new double[4];
        Assert.Equal(0, Functions.UltOscLookback(1, 1, 1));
        Assert.Throws<IndexOutOfRangeException>(() =>
            Functions.UltOsc<double>(h, l, c, System.Range.All, output, out _, 1, 1, 1)
        );
        Assert.Equal(
            50,
            TrueRangeRatioComparison
                .OwnedUltimate(Raw(c, h, l), [1, 1, 1], true)
                .Outputs["Value"]
                .Values[1]
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.UltOsc<double>(
                new[] { 2d },
                new[] { 0d },
                new[] { 1d },
                System.Range.All,
                new double[1],
                out _,
                1,
                2,
                3
            )
        );
        var max = double.MaxValue;
        var high = Enumerable.Repeat(max, 4).ToArray();
        var low = Enumerable.Repeat(-max, 4).ToArray();
        var closes = new double[4];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.UltOsc<double>(high, low, closes, System.Range.All, output, out _, 1, 2, 3)
        );
        Assert.False(double.IsFinite(output[0]));
        Assert.Equal(
            50,
            TrueRangeRatioComparison
                .OwnedUltimate(Raw(closes, high, low), [1, 2, 3], true)
                .Outputs["Value"]
                .Values[3]
        );
        var tiny = CompetitorData.FromOhlc(
            [0, 0, 0, 0],
            [double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon],
            [0, 0, 0, 0],
            [0, 0, 0, 0]
        );
        Assert.Null(tiny.Quotes.GetVortex(2).Last().Pvi);
        Assert.Null(tiny.Quotes.GetUltimate(1, 2, 3).Last().Ultimate);
        Assert.True(
            TrueRangeRatioComparison
                .OwnedVortex(tiny.IndicatorBars, 2)
                .Outputs["Positive"]
                .Present![3]
        );
    }

    [Fact]
    public async Task ChainingRetainsHighLowAndChangesPriorClose()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (var vortex in new[] { false, true })
        {
            MultiOutputIndicatorBase indicator = vortex
                ? new WindowVortex(3)
                : new WindowUltimateOscillator(1, 2, 3);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = vortex
                ? TrueRangeRatioComparison.VortexReference(bars, 3, false)
                : new[]
                {
                    TrueRangeRatioComparison.UltimateReference(bars, [1, 2, 3], false, false),
                };
            for (var slot = 0; slot < expected.Length; slot++)
            {
                Assert.Equal(
                    expected[slot].Select(v => v ?? 0),
                    run[indicator.Outputs[slot]].ToArray()
                );
                Assert.Equal(
                    expected[slot].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[slot + expected.Length]].ToArray()
                );
            }
        }
    }

    [Fact]
    public void AllOutputsPresenceAndZeroPolicyMutationsAreDetected()
    {
        foreach (var pair in TrueRangeRatioComparison.Pairs)
        foreach (var name in pair.OutputNames ?? ["Value"])
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
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        var ta = TrueRangeRatioComparison.UltimatePair(true, 2, 3);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                ta with
                {
                    Library = TrueRangeRatioComparison.UltimatePair(false, 2, 3).Ooples,
                },
                CompetitorData.FromOhlc([0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]),
                1
            )
        );
    }
}
