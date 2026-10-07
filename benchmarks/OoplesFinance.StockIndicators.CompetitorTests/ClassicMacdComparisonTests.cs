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
public sealed class ClassicMacdComparisonTests
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
    public async Task RationalContractsCoverAllSignalMethodsAndLifecycle(
        ClassicAverageMethod method
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicMacd),
                "classical MACD " + method,
                () =>
                    new ClassicMacd(
                        3,
                        5,
                        3,
                        ClassicAverageMethod.Tema,
                        ClassicAverageMethod.Ema,
                        method,
                        true,
                        2
                    )
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryFastSlowSignalCombinationMatchesIndependentModels()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var data = CompetitorData.Create(90);
        foreach (var fast in Enum.GetValues<ClassicAverageMethod>())
        foreach (var slow in Enum.GetValues<ClassicAverageMethod>())
        foreach (var signal in Enum.GetValues<ClassicAverageMethod>())
            ComparisonVerifier.Check(
                ClassicMacdComparison.Pair(3, 5, 3, fast, slow, signal),
                data,
                20
            );
    }

    [Fact]
    public void ReversedEqualAndIdentityPeriodsKeepAssociatedMethodsAndStartup()
    {
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var periods in new[] { (2, 3, 2), (3, 2, 2), (3, 3, 1), (5, 10, 3) })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            var pair = ClassicMacdComparison.Pair(
                periods.Item1,
                periods.Item2,
                periods.Item3,
                method,
                ClassicAverageMethod.Ema,
                method,
                first,
                suppression
            );
            ComparisonVerifier.Check(pair, CompetitorData.Create(140), 20);
        }
        var owner = new ClassicMacd(10, 3, 2, ClassicAverageMethod.Tema, ClassicAverageMethod.Wma);
        Assert.Equal(3, owner.FastPeriod);
        Assert.Equal(ClassicAverageMethod.Wma, owner.FastMethod);
        Assert.Equal(10, owner.SlowPeriod);
        Assert.Equal(ClassicAverageMethod.Tema, owner.SlowMethod);
    }

    [Fact]
    public void NativeFloatDoubleSubrangesAndIndividualInputAliasesMatch()
    {
        var prices = CompetitorData.Create(180).Closes;
        var methods = Enum.GetValues<ClassicAverageMethod>();
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var start in new[] { 0, 100 })
            for (var i = 0; i < methods.Length; i++)
            {
                using var settings = new ClassicAverageComparison.Settings(first, suppression);
                CheckNative(
                    prices,
                    methods[i],
                    methods[(i + 1) % methods.Length],
                    methods[(i + 2) % methods.Length],
                    first,
                    suppression,
                    start
                );
                CheckNative(
                    prices.Select(v => (float)v).ToArray(),
                    methods[i],
                    methods[(i + 1) % methods.Length],
                    methods[(i + 2) % methods.Length],
                    first,
                    suppression,
                    start
                );
            }
    }

    private static void CheckNative<T>(
        T[] prices,
        ClassicAverageMethod fm,
        ClassicAverageMethod sm,
        ClassicAverageMethod dm,
        bool first,
        int suppression,
        int start
    )
        where T : IFloatingPointIeee754<T>
    {
        var expected = ClassicMacdComparison.NativePacked(
            prices,
            5,
            3,
            3,
            fm,
            sm,
            dm,
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
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.MacdExt<T>(
                    input,
                    new System.Range(start, input.Length - 1),
                    output[0],
                    output[1],
                    output[2],
                    out var range,
                    5,
                    (TaCore.MAType)fm,
                    3,
                    (TaCore.MAType)sm,
                    3,
                    (TaCore.MAType)dm
                )
            );
            Assert.Equal(expected.Start, range.Start.Value);
            Assert.Equal(expected.Values[0].Length, range.End.Value - range.Start.Value);
            for (var j = 0; j < 3; j++)
                Assert.Equal(expected.Values[j], output[j].Take(expected.Values[j].Length));
        }
    }

    [Fact]
    public async Task WideUnpublishedOscillatorsFeedEverySignalWithoutPrematureRejection()
    {
        var prices = Enumerable
            .Range(0, 300)
            .Select(i => i is 21 or 22 ? double.MaxValue : -double.MaxValue)
            .ToArray();
        var bars = BarsOf(prices);
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var expected = ClassicMacdComparison.Reference(
                prices,
                2,
                10,
                40,
                ClassicAverageMethod.Sma,
                ClassicAverageMethod.Sma,
                method,
                false,
                0
            );
            Assert.All(
                expected.SelectMany(v => v).Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            ComparisonVerifier.Compare(
                AlignedMacdComparison.Series(expected),
                ClassicMacdComparison.Owned(
                    bars,
                    2,
                    10,
                    40,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma,
                    method,
                    false,
                    0
                ),
                "wide signal input " + method,
                IndicatorErrorBudget.Exact
            );
        }
        var noWait = ClassicMacdComparison.Reference(
            prices,
            2,
            10,
            1,
            ClassicAverageMethod.Sma,
            ClassicAverageMethod.Sma,
            ClassicAverageMethod.Sma,
            false,
            0
        );
        Assert.True(double.IsPositiveInfinity(noWait[0][22]!.Value));
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicMacd),
                "wide component cancellation",
                () => new ClassicMacd(2, 10, 40)
            ),
            new IndicatorValidationOptions
            {
                AdditionalFixtures =
                [
                    new IndicatorValidationFixture("wide hidden oscillator", bars),
                ],
            }
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new ClassicMacd(2, 10, 1))
                .BuildAsync()
        );
    }

    [Fact]
    public void SubnormalIdentityAndHugePeriodsKeepExactOutputAndLazyHistory()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var prices = new[]
            {
                0d,
                double.Epsilon,
                0,
                3 * double.Epsilon,
                double.Epsilon,
                0,
                4 * double.Epsilon,
            };
            ComparisonVerifier.Compare(
                AlignedMacdComparison.Series(
                    ClassicMacdComparison.Reference(
                        prices,
                        1,
                        2,
                        1,
                        method,
                        method,
                        method,
                        true,
                        0
                    )
                ),
                ClassicMacdComparison.Owned(
                    BarsOf(prices),
                    1,
                    2,
                    1,
                    method,
                    method,
                    method,
                    true,
                    0
                ),
                "subnormal classical MACD",
                IndicatorErrorBudget.Exact
            );
            var huge = new ClassicMacd(
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                method,
                method,
                method,
                true,
                int.MaxValue
            );
            Assert.Equal(int.MaxValue, huge.WarmupBars);
            var output = ClassicMacdComparison.Owned(
                BarsOf([1, 2, 3]),
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                method,
                method,
                method,
                true,
                int.MaxValue
            );
            Assert.All(output.Outputs["Macd"].Present!, v => Assert.False(v));
            var identity = ClassicMacdComparison.Owned(
                BarsOf(prices),
                1,
                1,
                1,
                method,
                method,
                method,
                true,
                int.MaxValue
            );
            Assert.All(identity.Outputs["Macd"].Values, v => Assert.Equal(0, v));
            Assert.All(identity.Outputs["Macd"].Present!, v => Assert.True(v));
        }
    }

    [Fact]
    public void NativeBoundariesAndNonfiniteOutputsRemainVisible()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var output = Enumerable.Range(0, 3).Select(_ => new double[10]).ToArray();
        TaCore.RetCode Call(double[] prices, int fast, int slow, int signal) =>
            Functions.MacdExt<double>(
                prices,
                System.Range.All,
                output[0],
                output[1],
                output[2],
                out _,
                fast,
                TaCore.MAType.Sma,
                slow,
                TaCore.MAType.Sma,
                signal,
                TaCore.MAType.Sma
            );
        Assert.Equal(TaCore.RetCode.OutOfRangeParam, Call([1, 2], 2, 2, 1));
        Assert.Equal(TaCore.RetCode.OutOfRangeParam, Call([1], 2, 2, 1));
        Assert.Equal(TaCore.RetCode.BadParam, Call([1, 2, 3], 1, 2, 1));
        Assert.Equal(TaCore.RetCode.BadParam, Call([1, 2, 3], 2, 2, 0));
        Assert.Equal(TaCore.RetCode.Success, Call([1, 2, 3], 20, 30, 2));
        var extremes = Enumerable.Repeat(double.MaxValue, 10).ToArray();
        Assert.Equal(TaCore.RetCode.Success, Call(extremes, 2, 3, 2));
        var expected = ClassicMacdComparison.NativePacked(
            extremes,
            2,
            3,
            2,
            ClassicAverageMethod.Sma,
            ClassicAverageMethod.Sma,
            ClassicAverageMethod.Sma,
            false,
            0,
            0,
            9
        );
        for (var j = 0; j < 3; j++)
            Assert.Equal(expected.Values[j], output[j].Take(expected.Values[j].Length));
        Assert.Contains(expected.Values[0], double.IsNaN);
        foreach (var invalid in new[] { new[] { 0, 3, 2 }, new[] { 3, 0, 2 }, new[] { 2, 3, 0 } })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ClassicMacd(invalid[0], invalid[1], invalid[2])
            );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicMacd(fastMethod: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicMacd(slowMethod: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicMacd(signalMethod: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicMacd(suppression: -1));
    }

    [Fact]
    public async Task ChainingAndEveryOutputAndConfigurationMutationAreDetected()
    {
        var data = CompetitorData.Create(100);
        var indicator = new ClassicMacd(
            3,
            5,
            3,
            ClassicAverageMethod.Tema,
            ClassicAverageMethod.Ema,
            ClassicAverageMethod.Wma,
            true,
            2
        );
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var expected = ClassicMacdComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 3, false),
            3,
            5,
            3,
            ClassicAverageMethod.Tema,
            ClassicAverageMethod.Ema,
            ClassicAverageMethod.Wma,
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
        var pair = ClassicMacdComparison.Pair(
            3,
            5,
            3,
            ClassicAverageMethod.Tema,
            ClassicAverageMethod.Ema,
            ClassicAverageMethod.Wma,
            true,
            2
        );
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        foreach (var name in AlignedMacdComparison.Names)
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
                ClassicMacdComparison.Pair(
                    3,
                    5,
                    3,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Ema,
                    ClassicAverageMethod.Wma,
                    true,
                    2
                ),
                ClassicMacdComparison.Pair(
                    3,
                    5,
                    3,
                    ClassicAverageMethod.Tema,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Wma,
                    true,
                    2
                ),
                ClassicMacdComparison.Pair(
                    3,
                    5,
                    3,
                    ClassicAverageMethod.Tema,
                    ClassicAverageMethod.Ema,
                    ClassicAverageMethod.Sma,
                    true,
                    2
                ),
                ClassicMacdComparison.Pair(
                    3,
                    5,
                    3,
                    ClassicAverageMethod.Tema,
                    ClassicAverageMethod.Ema,
                    ClassicAverageMethod.Wma,
                    false,
                    2
                ),
            }
        )
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair with { Library = wrong.Library }, data, 20)
            );
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
