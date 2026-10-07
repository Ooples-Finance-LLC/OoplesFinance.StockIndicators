using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RangeAccelerationComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task RationalContractsVerifyLifecycleAndLazyPeriods(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(RangeAccelerationBands),
                "range acceleration",
                () => new RangeAccelerationBands(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void ExactTransformZeroSumAndCompleteStartupArePinned()
    {
        var d = CompetitorData.FromOhlcv([2, 2, 2], [3, 3, 3], [1, 1, 1], [2, 2, 2], [0, 0, 0]);
        var actual = RangeAccelerationComparison.Owned(d.IndicatorBars, 2).Outputs;
        for (var j = 0; j < 3; j++)
        {
            var r = actual[RangeAccelerationComparison.Names[j]];
            Assert.Equal(new[] { false, true, true }, r.Present);
            Assert.Equal(new[] { 9d, 2, -1 }[j], r.Values[1]);
            Assert.Equal(r.Values[1], r.Values[2]);
        }
        var zero = CompetitorData.FromOhlcv([0, 0], [3, 5], [-3, -5], [0, 0], [0, 0]);
        var z = RangeAccelerationComparison.Owned(zero.IndicatorBars, 2).Outputs;
        Assert.Equal(4, z["Upper"].Values[1]);
        Assert.Equal(-4, z["Lower"].Values[1]);
        foreach (var p in new[] { 2, 3, 20 })
        {
            ComparisonVerifier.Check(
                RangeAccelerationComparison.Pair,
                CompetitorData.Create(100),
                p
            );
            ComparisonVerifier.Check(RangeAccelerationComparison.Pair, zero, p);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(
                    RangeAccelerationComparison.Pair,
                    ComparisonVerifier.Fixture(shape, 60),
                    p
                );
        }
    }

    [Fact]
    public void ExtendedInputsCancelBeforeFinalPublication()
    {
        Bar[] bars =
        [
            new(DateTime.UnixEpoch, 0, double.MaxValue, 0, 0, 0),
            new(DateTime.UnixEpoch.AddDays(1), 0, -double.MaxValue, 0, 0, 0),
        ];
        var r = RangeAccelerationComparison.Owned(bars, 2);
        Assert.All(r.Outputs.Values, v => Assert.Equal(0, v.Values[1]));
        Assert.Throws<IndicatorOutputException>(() => RangeAccelerationComparison.Owned(bars, 1));
        var tiny = new[] { new Bar(DateTime.UnixEpoch, 0, double.Epsilon, 0, 0, 0) };
        Assert.Equal(
            5 * double.Epsilon,
            RangeAccelerationComparison.Owned(tiny, 1).Outputs["Upper"].Values[0]
        );
    }

    [Fact]
    public void NativeSubrangesFloatAndAllInputAliasesMatchStagedReference()
    {
        var d = CompetitorData.Create(100);
        foreach (var p in new[] { 2, 3, 20 })
        foreach (var start in new[] { 0, 40 })
        {
            var expected = RangeAccelerationComparison.NativePacked(
                d.Highs,
                d.Lows,
                d.Closes,
                p,
                start,
                80
            );
            for (var source = 0; source < 3; source++)
            for (var destination = 0; destination < 3; destination++)
            {
                var inputs = new[]
                {
                    (double[])d.Highs.Clone(),
                    (double[])d.Lows.Clone(),
                    (double[])d.Closes.Clone(),
                };
                var output = new[] { new double[100], new double[100], new double[100] };
                output[destination] = inputs[source];
                Assert.Equal(
                    TaCore.RetCode.Success,
                    Functions.Accbands<double>(
                        inputs[0],
                        inputs[1],
                        inputs[2],
                        start..80,
                        output[0],
                        output[1],
                        output[2],
                        out var range,
                        p
                    )
                );
                Assert.Equal(Math.Max(start, p - 1)..81, range);
                for (var j = 0; j < 3; j++)
                    Assert.Equal(expected[j], output[j].Take(expected[j].Length));
            }
            var h = d.Highs.Select(v => (float)v).ToArray();
            var l = d.Lows.Select(v => (float)v).ToArray();
            var c = d.Closes.Select(v => (float)v).ToArray();
            var f = new[] { new float[100], new float[100], new float[100] };
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Accbands<float>(h, l, c, start..80, f[0], f[1], f[2], out _, p)
            );
            var oracle = RangeAccelerationComparison.NativePacked(h, l, c, p, start, 80);
            for (var j = 0; j < 3; j++)
                Assert.Equal(oracle[j], f[j].Take(oracle[j].Length));
        }
    }

    [Fact]
    public void NativeParameterAndFiniteInputOverflowLimitationsStayVisible()
    {
        var d = CompetitorData.Create(40);
        var output = new[] { new double[40], new double[40], new double[40] };
        Assert.Throws<ArgumentOutOfRangeException>(() => new RangeAccelerationBands(0));
        Assert.Equal(-1, Functions.AccbandsLookback(1));
        Assert.Equal(19, Functions.AccbandsLookback());
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Accbands<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                output[0],
                output[1],
                output[2],
                out _,
                1
            )
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Accbands<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                output[0],
                output[1],
                output[2],
                out var empty,
                int.MaxValue
            )
        );
        Assert.Equal(0..0, empty);
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Accbands<double>(
                d.Highs,
                d.Lows,
                d.Closes,
                90..100,
                output[0],
                output[1],
                output[2],
                out _,
                2
            )
        );
        double[] h = [double.MaxValue, -double.MaxValue];
        double[] l = [0, 0];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Accbands<double>(
                h,
                l,
                l,
                System.Range.All,
                output[0],
                output[1],
                output[2],
                out _,
                2
            )
        );
        Assert.True(double.IsNaN(output[0][0]));
        Assert.True(double.IsNaN(output[2][0]));
    }

    [Fact]
    public async Task ChainingAndEveryOutputMutationAreChecked()
    {
        var d = CompetitorData.Create(70);
        var indicator = new RangeAccelerationBands(3);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var modified = CompetitorData.FromOhlcv(
            d.Opens,
            d.Highs,
            d.Lows,
            FixedWeightedComparison.Stage(d.Closes, 3, false),
            d.Volumes
        );
        var expected = RangeAccelerationComparison.Reference(modified, 3);
        for (var j = 0; j < 3; j++)
        {
            var r = expected.Outputs[RangeAccelerationComparison.Names[j]];
            Assert.Equal(
                r.Values.Select((v, i) => r.Present![i] ? v : 0),
                run[indicator.Outputs[j]].ToArray()
            );
            Assert.Equal(
                r.Present!.Select(p => p ? 1d : 0),
                run[indicator.Outputs[j + 3]].ToArray()
            );
        }
        var pair = RangeAccelerationComparison.Pair;
        foreach (var name in RangeAccelerationComparison.Names)
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
                    3
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = (data, p) => pair.Ooples(data, p + 1),
                },
                d,
                3
            )
        );
    }
}
