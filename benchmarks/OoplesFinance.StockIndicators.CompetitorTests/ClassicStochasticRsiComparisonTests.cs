using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA strength settings")]
public sealed class ClassicStochasticRsiComparisonTests
{
    [Theory]
    [InlineData(ClassicAverageMethod.Sma)]
    [InlineData(ClassicAverageMethod.Ema)]
    [InlineData(ClassicAverageMethod.Wma)]
    [InlineData(ClassicAverageMethod.Dema)]
    [InlineData(ClassicAverageMethod.Tema)]
    [InlineData(ClassicAverageMethod.Trima)]
    [InlineData(ClassicAverageMethod.Kama)]
    [InlineData(ClassicAverageMethod.Mama)]
    [InlineData(ClassicAverageMethod.T3)]
    public async Task RationalContractsCoverEverySignalAndLifecycle(ClassicAverageMethod method)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicStochasticRsi),
                "classical stochastic RSI " + method,
                () => new ClassicStochasticRsi(3, 3, 3, method, true, 1, 2)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryMethodAndIndependentSuppressionSettingMatchesBothModels()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { (0, 0), (1, 2), (3, 0) })
        foreach (var periods in new[] { (2, 1, 1), (3, 2, 2), (14, 5, 3) })
        {
            using var settings = new ClassicStochasticRsiComparison.Settings(
                first,
                suppression.Item1,
                suppression.Item2
            );
            var pair = ClassicStochasticRsiComparison.Pair(
                periods.Item1,
                periods.Item2,
                periods.Item3,
                method,
                first,
                suppression.Item1,
                suppression.Item2
            );
            ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 75), 20);
        }
    }

    [Fact]
    public void NativeGenericArithmeticShiftedRangesAndAliasesAreExact()
    {
        var prices = CompetitorData.Create(180).Closes;
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { (0, 0), (1, 2), (3, 0) })
        foreach (var start in new[] { 0, 90 })
        {
            using var settings = new ClassicStochasticRsiComparison.Settings(
                first,
                suppression.Item1,
                suppression.Item2
            );
            CheckNative(prices, method, first, suppression.Item1, suppression.Item2, start);
            CheckNative(
                prices.Select(v => (float)v).ToArray(),
                method,
                first,
                suppression.Item1,
                suppression.Item2,
                start
            );
        }
    }

    private static void CheckNative<T>(
        T[] prices,
        ClassicAverageMethod method,
        bool first,
        int rsiSuppression,
        int averageSuppression,
        int start
    )
        where T : IFloatingPointIeee754<T>
    {
        var expected = ClassicStochasticRsiComparison.NativePacked(
            prices,
            3,
            3,
            3,
            method,
            first,
            rsiSuppression,
            averageSuppression,
            start,
            prices.Length - 1
        );
        foreach (var alias in new[] { 0, 1, 2, 3 })
        {
            var input = prices.ToArray();
            var k = alias == 1 ? input : new T[input.Length];
            var d =
                alias == 2 ? input
                : alias == 3 ? k
                : new T[input.Length];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.StochRsi<T>(
                    input,
                    new System.Range(start, input.Length - 1),
                    k,
                    d,
                    out var range,
                    3,
                    3,
                    3,
                    (TaCore.MAType)method
                )
            );
            Assert.Equal(expected.Start, range.Start.Value);
            Assert.Equal(expected.Values[0].Length, range.End.Value - range.Start.Value);
            Assert.Equal(expected.Values[0], k.Take(expected.Values[0].Length));
            Assert.Equal(expected.Values[alias == 3 ? 0 : 1], d.Take(expected.Values[1].Length));
        }
    }

    [Fact]
    public void MetastockZeroTailIsPreservedAsANativeDefect()
    {
        using var settings = new ClassicStochasticRsiComparison.Settings(true, 0, 0);
        var input = new[] { 1d, 3, 2, 5, 4, 8, 1, 7, 6, 9 };
        var rsi = new double[input.Length];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Rsi<double>(input, System.Range.All, rsi, out var rsiRange, 3)
        );
        Assert.Equal(
            ClassicStochasticRsiComparison.NativeRsi(input, 3, true, 0, 0, input.Length - 1),
            rsi.Take(rsiRange.End.Value - rsiRange.Start.Value)
        );
        Assert.Equal(input.Length - 1, rsiRange.End.Value);
        var expected = ClassicStochasticRsiComparison.NativePacked(
            input,
            3,
            2,
            1,
            ClassicAverageMethod.Sma,
            true,
            0,
            0,
            0,
            input.Length - 1
        );
        var k = new double[input.Length];
        var d = new double[input.Length];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.StochRsi<double>(input, System.Range.All, k, d, out var range, 3, 2, 1)
        );
        Assert.Equal(expected.Values[0], k.Take(range.End.Value - range.Start.Value));
        Assert.Equal(0, expected.Values[0][^1]);
        var owned = ClassicStochasticRsiComparison.Reference(
            input,
            3,
            2,
            1,
            ClassicAverageMethod.Sma,
            true,
            0,
            0
        );
        Assert.Equal(100d, owned[0][^1]);
        Assert.Equal(input.Length, range.End.Value);
    }

    [Fact]
    public void NativeUnderflowAndOverflowRemainVisibleWhileOwnedOutputsStayFinite()
    {
        using var settings = new ClassicStochasticRsiComparison.Settings(false, 0, 0);
        foreach (
            var input in new[]
            {
                new[]
                {
                    0d,
                    double.Epsilon,
                    0,
                    2 * double.Epsilon,
                    0,
                    3 * double.Epsilon,
                    0,
                    4 * double.Epsilon,
                    0,
                    5 * double.Epsilon,
                },
                Enumerable
                    .Range(0, 20)
                    .Select(i => i % 2 == 0 ? -double.MaxValue : double.MaxValue)
                    .ToArray(),
            }
        )
        {
            var expected = ClassicStochasticRsiComparison.NativePacked(
                input,
                2,
                2,
                2,
                ClassicAverageMethod.Sma,
                false,
                0,
                0,
                0,
                input.Length - 1
            );
            var k = new double[input.Length];
            var d = new double[input.Length];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.StochRsi<double>(input, System.Range.All, k, d, out var range, 2, 2, 2)
            );
            Assert.Equal(expected.Values[0], k.Take(range.End.Value - range.Start.Value));
            Assert.Equal(expected.Values[1], d.Take(range.End.Value - range.Start.Value));
            ComparisonVerifier.Compare(
                ClassicStochasticRsiComparison.Series(
                    ClassicStochasticRsiComparison.Reference(
                        input,
                        2,
                        2,
                        2,
                        ClassicAverageMethod.Sma,
                        false,
                        0,
                        0
                    )
                ),
                ClassicStochasticRsiComparison.Owned(
                    BarsOf(input),
                    2,
                    2,
                    2,
                    ClassicAverageMethod.Sma,
                    false,
                    0,
                    0
                ),
                "extreme stochastic RSI",
                IndicatorErrorBudget.Exact
            );
            if (input.Any(v => Math.Abs(v) == double.MaxValue))
                Assert.Contains(expected.Values[0], double.IsNaN);
        }
    }

    [Fact]
    public void NativeShortIntermediateAndPublicLimitsAreExplicit()
    {
        using var settings = new ClassicStochasticRsiComparison.Settings(false, 0, 0);
        var input = new[] { 1d, 2, 3 };
        var k = new double[3];
        var d = new double[3];
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.StochRsi<double>(input, System.Range.All, k, d, out _, 2, 1, 1)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.StochRsi<double>([1d], System.Range.All, k, d, out _, 2, 1, 1)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.StochRsi<double>(input, System.Range.All, k, d, out _, 1, 1, 1)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.StochRsi<double>(input, System.Range.All, k, d, out _, 2, 0, 1)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.StochRsi<double>(input, System.Range.All, k, d, out _, 2, 1, 0)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.StochRsi<double>(input, 4..5, k, d, out _, 2, 1, 1)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.StochRsi<double>(input, System.Range.All, k, d, out var empty, 20, 1, 1)
        );
        Assert.Equal(0, empty.End.Value);
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var owned = ClassicStochasticRsiComparison.Owned(
                BarsOf(input),
                1,
                1,
                1,
                method,
                true,
                0,
                int.MaxValue
            );
            Assert.Equal(new[] { false, true, true }, owned.Outputs["K"].Present!);
            Assert.All(owned.Outputs["K"].Values.Skip(1), value => Assert.Equal(0, value));
            var huge = new ClassicStochasticRsi(
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                method,
                true,
                int.MaxValue,
                int.MaxValue
            );
            Assert.Equal(int.MaxValue, huge.WarmupBars);
            var values = ClassicStochasticRsiComparison.Owned(
                BarsOf(input),
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                method,
                true,
                int.MaxValue,
                int.MaxValue
            );
            Assert.All(values.Outputs["K"].Present!, value => Assert.False(value));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicStochasticRsi(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicStochasticRsi(stochasticPeriod: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicStochasticRsi(signalPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicStochasticRsi(method: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicStochasticRsi(rsiSuppression: -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicStochasticRsi(averageSuppression: -1)
        );
    }

    [Fact]
    public async Task ChainingAndEveryValuePresenceAndParameterMutationAreDetected()
    {
        var data = CompetitorData.Create(100);
        var indicator = new ClassicStochasticRsi(3, 3, 3, ClassicAverageMethod.Ema, true, 1, 2);
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = ClassicStochasticRsiComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 3, false),
            3,
            3,
            3,
            ClassicAverageMethod.Ema,
            true,
            1,
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
        using var settings = new ClassicStochasticRsiComparison.Settings(true, 1, 2);
        var pair = ClassicStochasticRsiComparison.Pair(
            3,
            3,
            3,
            ClassicAverageMethod.Ema,
            true,
            1,
            2
        );
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        foreach (var name in ClassicStochasticRsiComparison.Names)
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (mask)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
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
                ClassicStochasticRsiComparison.Pair(3, 3, 3, ClassicAverageMethod.Ema, false, 1, 2),
                ClassicStochasticRsiComparison.Pair(3, 3, 3, ClassicAverageMethod.Sma, true, 1, 2),
                ClassicStochasticRsiComparison.Pair(3, 3, 3, ClassicAverageMethod.Ema, true, 0, 2),
                ClassicStochasticRsiComparison.Pair(3, 2, 3, ClassicAverageMethod.Ema, true, 1, 2),
            }
        )
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair with { Library = wrong.Library }, data, 20)
            );
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
