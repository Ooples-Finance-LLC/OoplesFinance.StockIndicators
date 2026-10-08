using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class ClassicBandsComparisonTests
{
    [Theory]
    [InlineData(ClassicAverageMethod.Sma)]
    [InlineData(ClassicAverageMethod.Tema)]
    [InlineData(ClassicAverageMethod.Mama)]
    public async Task RationalContractsCoverAllOutputsAndLifecycle(ClassicAverageMethod method)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicDeviationBands),
                "classical deviation bands",
                () => new ClassicDeviationBands(3, 1.5, .5, method, true, 2)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void AllAverageMethodsFactorBranchesAndStartupSettingsMatchIndependentContracts()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            foreach (var factors in new[] { (0d, 0d), (1d, 1d), (1d, 2d), (2d, 1d), (.5, 3d) })
            foreach (var p in new[] { 2, 3, 10, 40 })
            {
                var pair = ClassicBandsComparison.Pair(
                    method,
                    factors.Item1,
                    factors.Item2,
                    first,
                    suppression
                );
                ComparisonVerifier.Check(pair, CompetitorData.Create(100), p);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 70), p);
            }
        }
    }

    [Fact]
    public void GenericFloatDoubleSubrangesAndEachInputOutputAliasPreserveNativeStages()
    {
        var prices = CompetitorData.Create(160).Closes;
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var p in new[] { 3, 10, 40 })
        foreach (var start in new[] { 0, 90 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            CheckNative(prices, p, method, first, suppression, start);
            CheckNative(
                prices.Select(v => (float)v).ToArray(),
                p,
                method,
                first,
                suppression,
                start
            );
        }
    }

    private static void CheckNative<T>(
        T[] prices,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int start
    )
        where T : IFloatingPointIeee754<T>
    {
        var expected = ClassicBandsComparison.NativePacked(
            prices,
            p,
            method,
            1,
            2,
            first,
            suppression,
            start,
            prices.Length - 1
        );
        for (var alias = -1; alias < 3; alias++)
        {
            var input = prices.ToArray();
            var output = Enumerable
                .Range(0, 3)
                .Select(j => j == alias ? input : new T[input.Length])
                .ToArray();
            var code = Functions.Bbands<T>(
                input,
                new System.Range(start, prices.Length - 1),
                output[0],
                output[1],
                output[2],
                out var range,
                p,
                1,
                2,
                (TaCore.MAType)method
            );
            Assert.Equal(TaCore.RetCode.Success, code);
            Assert.Equal(expected.Start, range.Start.Value);
            Assert.Equal(expected.Values[0].Length, range.End.Value - range.Start.Value);
            for (var j = 0; j < 3; j++)
                Assert.Equal(expected.Values[j], output[j].Take(expected.Values[j].Length));
        }
    }

    [Fact]
    public void LongerMamaDeviationWindowExposesNativeCenterMisalignment()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var d = CompetitorData.Create(120);
        const int p = 80;
        var pair = ClassicBandsComparison.Pair(ClassicAverageMethod.Mama);
        ComparisonVerifier.Check(pair, d, p);
        var native = pair.Competitor(d, p).Outputs["MiddleBand"];
        var own = pair.Ooples(d, p).Outputs["MiddleBand"];
        Assert.Equal(Enumerable.Range(0, d.Count).Select(i => i >= 79), native.Present);
        Assert.Equal(native.Present, own.Present);
        var nativeMama = ClassicNativeReference.Packed(
            d.Closes,
            p,
            ClassicAverageMethod.Mama,
            false,
            0,
            0,
            d.Count - 1
        );
        Assert.Equal(nativeMama[0], native.Values[79]);
        var correct = ClassicAverageComparison.Reference(
            d.Closes,
            p,
            ClassicAverageMethod.Mama,
            false,
            0
        );
        Assert.Equal(correct[79], own.Values[79]);
        Assert.NotEqual(native.Values[79], own.Values[79]);
        Assert.Equal(32, Functions.BbandsLookback(p, TaCore.MAType.Mama));
        Assert.Equal(
            79,
            new ClassicDeviationBands(p, method: ClassicAverageMethod.Mama).WarmupBars
        );
    }

    [Fact]
    public void NativeUncenteredOverflowAndCancellationRemainVisible()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var prices = new[] { 1e200, 2e200, 1e200, 2e200 };
        var native = Enumerable.Range(0, 3).Select(_ => new double[prices.Length]).ToArray();
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Bbands<double>(
                prices,
                System.Range.All,
                native[0],
                native[1],
                native[2],
                out var range,
                2
            )
        );
        var reference = ClassicBandsComparison.NativePacked(
            prices,
            2,
            ClassicAverageMethod.Sma,
            2,
            2,
            false,
            0,
            0,
            prices.Length - 1
        );
        for (var j = 0; j < 3; j++)
            Assert.Equal(reference.Values[j], native[j].Take(range.End.Value - range.Start.Value));
        Assert.Equal(native[0][0], native[1][0]);
        Assert.Equal(native[2][0], native[1][0]);
        var own = ClassicBandsComparison.Owned(
            BarsOf(prices),
            2,
            ClassicAverageMethod.Sma,
            2,
            2,
            false,
            0
        );
        ComparisonVerifier.Compare(
            ClassicBandsComparison.Series(
                ClassicBandsComparison.Reference(
                    prices,
                    2,
                    ClassicAverageMethod.Sma,
                    2,
                    2,
                    false,
                    0
                )
            ),
            own,
            "large finite deviation",
            IndicatorErrorBudget.Exact
        );
        Assert.True(own.Outputs["UpperBand"].Values[1] > own.Outputs["MiddleBand"].Values[1]);
        ComparisonVerifier.Check(
            ClassicBandsComparison.Pair(),
            DispersionComparison.CancellationFixture(),
            3
        );
        var flat = Enumerable.Repeat(double.MaxValue, 4).ToArray();
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Bbands<double>(
                flat,
                System.Range.All,
                native[0],
                native[1],
                native[2],
                out range,
                2
            )
        );
        var overflow = ClassicBandsComparison.NativePacked(
            flat,
            2,
            ClassicAverageMethod.Sma,
            2,
            2,
            false,
            0,
            0,
            3
        );
        for (var j = 0; j < 3; j++)
            Assert.Equal(overflow.Values[j], native[j].Take(3));
        Assert.Contains(native[1].Take(3), double.IsInfinity);
    }

    [Fact]
    public void SubnormalAndLargeFiniteBandsUseIndependentGridArithmeticAndBoundedStorage()
    {
        foreach (
            var prices in new[]
            {
                new[] { -double.MaxValue, double.MaxValue, -double.MaxValue },
                new[] { 0d, double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon },
            }
        )
            ComparisonVerifier.Compare(
                ClassicBandsComparison.Series(
                    ClassicBandsComparison.Reference(
                        prices,
                        2,
                        ClassicAverageMethod.Sma,
                        .25,
                        .5,
                        false,
                        0
                    )
                ),
                ClassicBandsComparison.Owned(
                    BarsOf(prices),
                    2,
                    ClassicAverageMethod.Sma,
                    .25,
                    .5,
                    false,
                    0
                ),
                "extreme band stages",
                IndicatorErrorBudget.Exact
            );
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var result = ClassicBandsComparison.Owned(
                BarsOf([1, 2, 3]),
                int.MaxValue,
                method,
                1,
                2,
                true,
                int.MaxValue
            );
            Assert.All(
                result.Outputs.Values,
                v => Assert.All(v.Present!, present => Assert.False(present))
            );
            var identity = ClassicBandsComparison.Owned(
                BarsOf([1, 2, 3]),
                1,
                method,
                1,
                2,
                true,
                int.MaxValue
            );
            foreach (var output in identity.Outputs.Values)
                Assert.Equal(new[] { 1d, 2, 3 }, output.Values);
        }
    }

    [Fact]
    public async Task ChainingAndEveryOutputMaskAndWidthMutationAreVerified()
    {
        var d = CompetitorData.Create(90);
        var indicator = new ClassicDeviationBands(3, 1, 2, ClassicAverageMethod.Ema, true, 2);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = ClassicBandsComparison.Reference(
            FixedWeightedComparison.Stage(d.Closes, 3, false),
            3,
            ClassicAverageMethod.Ema,
            1,
            2,
            true,
            2
        );
        for (var j = 0; j < 3; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[indicator.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[indicator.Outputs[j + 3]].ToArray()
            );
        }
        using var settings = new ClassicAverageComparison.Settings(true, 2);
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var pair = ClassicBandsComparison.Pair(method, 1, 2, true, 2);
            foreach (var name in ClassicBandsComparison.Names)
            foreach (var native in new[] { false, true })
            foreach (var mask in new[] { false, true })
            {
                ComparisonSeries Bad(CompetitorData data, int p)
                {
                    var result = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                    if (mask)
                        result.Outputs[name].Present![^1] = false;
                    else
                        result.Outputs[name].Values[^1] += 1;
                    return result;
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
                        Library = ClassicBandsComparison.Pair(method, 2, 1, true, 2).Library,
                    },
                    d,
                    3
                )
            );
        }
    }

    [Fact]
    public void NativeBoundsInvalidAliasesNaNWidthsAndOwnedValidationAreExplicit()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var input = new[] { 1d, 3, 8 };
        var u = new double[3];
        var m = new double[3];
        var l = new double[3];
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Bbands<double>(input, System.Range.All, u, m, l, out _, 1)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Bbands<double>(input, System.Range.All, u, m, l, out _, 2, -1, 2)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Bbands<double>(input, System.Range.All, u, m, l, out _, 2, 1, -2)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Bbands<double>(
                input,
                System.Range.All,
                u,
                m,
                l,
                out _,
                2,
                1,
                2,
                (TaCore.MAType)999
            )
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Bbands<double>(input, System.Range.All, input, input, input, out _, 2)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Bbands<double>([1d], System.Range.All, u, m, l, out _, 2)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Bbands<double>(input, 4..5, u, m, l, out _, 2)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Bbands<double>(input, System.Range.All, u, m, l, out var empty, 20)
        );
        Assert.Equal(0, empty.End.Value);
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Bbands<double>(
                input,
                System.Range.All,
                u,
                m,
                l,
                out var range,
                2,
                double.NaN,
                double.NaN
            )
        );
        var reference = ClassicBandsComparison.NativePacked(
            input,
            2,
            ClassicAverageMethod.Sma,
            double.NaN,
            double.NaN,
            false,
            0,
            0,
            2
        );
        Assert.Equal(reference.Values[0], u.Take(range.End.Value - range.Start.Value));
        Assert.Equal(reference.Values[2], l.Take(range.End.Value - range.Start.Value));
        Assert.True(double.IsNaN(u[0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicDeviationBands(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicDeviationBands(method: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicDeviationBands(suppression: -1)
        );
        foreach (
            var factor in new[]
            {
                -1d,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            }
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ClassicDeviationBands(upperFactor: factor)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ClassicDeviationBands(lowerFactor: factor)
            );
        }
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
