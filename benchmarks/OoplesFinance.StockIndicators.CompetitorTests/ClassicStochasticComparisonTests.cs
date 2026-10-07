using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class ClassicStochasticComparisonTests
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
    public async Task RationalContractsCoverBothStagesAndLifecycle(ClassicAverageMethod method)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicStochastic),
                "classical stochastic " + method,
                () => new ClassicStochastic(3, 2, 3, method, method, true, 2)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryMethodPairAndCompatibilitySettingMatchesIndependentModels()
    {
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            foreach (var km in Enum.GetValues<ClassicAverageMethod>())
            foreach (var dm in Enum.GetValues<ClassicAverageMethod>())
                ComparisonVerifier.Check(
                    ClassicStochasticComparison.Pair(false, 5, 3, 2, km, dm, first, suppression),
                    CompetitorData.Create(120),
                    20
                );
            foreach (var dm in Enum.GetValues<ClassicAverageMethod>())
                ComparisonVerifier.Check(
                    ClassicStochasticComparison.Pair(
                        true,
                        3,
                        1,
                        2,
                        ClassicAverageMethod.Sma,
                        dm,
                        first,
                        suppression
                    ),
                    CompetitorData.Create(110),
                    20
                );
        }
    }

    [Fact]
    public void NativeFloatDoubleSubrangesAndInputAliasesMatch()
    {
        foreach (var fast in new[] { false, true })
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var start in new[] { 0, 100 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            var methods = Enum.GetValues<ClassicAverageMethod>();
            for (var i = 0; i < methods.Length; i++)
            {
                CheckNative<double>(
                    fast,
                    methods[i],
                    methods[(i + 3) % methods.Length],
                    first,
                    suppression,
                    start
                );
                CheckNative<float>(
                    fast,
                    methods[i],
                    methods[(i + 3) % methods.Length],
                    first,
                    suppression,
                    start
                );
            }
        }
    }

    private static void CheckNative<T>(
        bool fast,
        ClassicAverageMethod km,
        ClassicAverageMethod dm,
        bool first,
        int suppression,
        int start
    )
        where T : IFloatingPointIeee754<T>
    {
        var bars = CompetitorData.Create(180).IndicatorBars;
        var highs = bars.Select(b => T.CreateChecked(b.High)).ToArray();
        var lows = bars.Select(b => T.CreateChecked(b.Low)).ToArray();
        var closes = bars.Select(b => T.CreateChecked(b.Close)).ToArray();
        var expected = ClassicStochasticComparison.NativePacked(
            highs,
            lows,
            closes,
            fast,
            5,
            3,
            2,
            km,
            dm,
            first,
            suppression,
            start,
            179
        );
        for (var alias = -1; alias < 6; alias++)
        {
            var aliasedExpected =
                alias >= 3
                    ? ClassicStochasticComparison.NativePacked(
                        highs,
                        lows,
                        closes,
                        fast,
                        5,
                        3,
                        2,
                        km,
                        dm,
                        first,
                        suppression,
                        start,
                        179,
                        true
                    )
                    : expected;
            var inputs = new[] { highs.ToArray(), lows.ToArray(), closes.ToArray() };
            var a = alias is >= 0 and < 3 ? inputs[alias] : new T[180];
            var b = alias >= 3 ? inputs[alias - 3] : new T[180];
            Assert.Equal(
                TaCore.RetCode.Success,
                ClassicStochasticComparison.NativeCall(
                    inputs[0],
                    inputs[1],
                    inputs[2],
                    fast,
                    start..179,
                    a,
                    b,
                    out var range,
                    5,
                    3,
                    2,
                    km,
                    dm
                )
            );
            Assert.Equal(expected.Start, range.Start.Value);
            var count = range.End.Value - range.Start.Value;
            Assert.True(
                aliasedExpected.Values[0].SequenceEqual(a.Take(count)),
                $"K: {typeof(T).Name} fast={fast} k={km} d={dm} first={first} suppression={suppression} start={start} alias={alias}"
            );
            Assert.True(
                aliasedExpected.Values[1].SequenceEqual(b.Take(count)),
                $"D: {typeof(T).Name} fast={fast} k={km} d={dm} first={first} suppression={suppression} start={start} alias={alias}"
            );
        }
        var same = new T[180];
        Assert.Equal(
            TaCore.RetCode.Success,
            ClassicStochasticComparison.NativeCall(
                highs,
                lows,
                closes,
                fast,
                start..179,
                same,
                same,
                out var shared,
                5,
                3,
                2,
                km,
                dm
            )
        );
        Assert.Equal(expected.Values[0], same.Take(shared.End.Value - shared.Start.Value));
    }

    [Fact]
    public void BothOutputsWaitForSignalAndFlatRangesUseZero()
    {
        var bars = ComparisonVerifier.Fixture("constant", 20).CloseBars;
        var output = ClassicStochasticComparison.Owned(
            bars,
            3,
            2,
            4,
            ClassicAverageMethod.Sma,
            ClassicAverageMethod.Sma,
            false,
            0
        );
        foreach (var name in ClassicStochasticRsiComparison.Names)
        {
            Assert.Equal(
                Enumerable.Range(0, 20).Select(i => i >= 6),
                output.Outputs[name].Present!
            );
            Assert.All(output.Outputs[name].Values.Skip(6), v => Assert.Equal(0, v));
        }
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        foreach (var fast in new[] { false, true })
        foreach (var periods in new[] { (1, 1, 1), (2, 1, 3), (3, 2, 1), (5, 3, 3) })
            ComparisonVerifier.Check(
                ClassicStochasticComparison.Pair(fast, periods.Item1, periods.Item2, periods.Item3),
                CompetitorData.Create(90),
                20
            );
    }

    [Fact]
    public async Task WideHiddenRatiosCancelAndPublishedOverflowIsRejected()
    {
        var bars = Enumerable
            .Range(0, 140)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                0,
                double.Epsilon,
                0,
                i % 2 == 0 ? double.MaxValue : -double.MaxValue,
                0
            ))
            .ToArray();
        foreach (var dm in Enum.GetValues<ClassicAverageMethod>())
        {
            var expected = ClassicStochasticComparison.Reference(
                bars,
                1,
                2,
                3,
                ClassicAverageMethod.Sma,
                dm,
                false,
                0
            );
            Assert.All(
                expected.SelectMany(v => v).Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            ComparisonVerifier.Compare(
                ClassicStochasticComparison.Series(expected),
                ClassicStochasticComparison.Owned(
                    bars,
                    1,
                    2,
                    3,
                    ClassicAverageMethod.Sma,
                    dm,
                    false,
                    0
                ),
                "hidden wide stochastic " + dm,
                IndicatorErrorBudget.Exact
            );
        }
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicStochastic),
                "wide stochastic cancellation",
                () => new ClassicStochastic(1, 2, 3)
            ),
            new IndicatorValidationOptions
            {
                AdditionalFixtures = [new IndicatorValidationFixture("hidden wide ratios", bars)],
            }
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new ClassicStochastic(1, 1, 1))
                .BuildAsync()
        );
    }

    [Fact]
    public void NativeNonfiniteAndSubnormalArithmeticRemainExplicit()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        foreach (var fast in new[] { false, true })
        foreach (var scale in new[] { double.MaxValue, double.Epsilon })
        {
            var highs = Enumerable.Repeat(scale, 40).ToArray();
            var lows = Enumerable.Repeat(-scale, 40).ToArray();
            var closes = Enumerable.Range(0, 40).Select(i => i % 2 == 0 ? scale : -scale).ToArray();
            var expected = ClassicStochasticComparison.NativePacked(
                highs,
                lows,
                closes,
                fast,
                2,
                2,
                2,
                ClassicAverageMethod.Sma,
                ClassicAverageMethod.Sma,
                false,
                0,
                0,
                39
            );
            var a = new double[40];
            var b = new double[40];
            Assert.Equal(
                TaCore.RetCode.Success,
                ClassicStochasticComparison.NativeCall(
                    highs,
                    lows,
                    closes,
                    fast,
                    System.Range.All,
                    a,
                    b,
                    out var range,
                    2,
                    2,
                    2,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma
                )
            );
            var count = range.End.Value - range.Start.Value;
            Assert.Equal(expected.Values[0], a.Take(count));
            Assert.Equal(expected.Values[1], b.Take(count));
            var bars = closes
                .Select(
                    (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, highs[i], lows[i], v, 0)
                )
                .ToArray();
            var reference = ClassicStochasticComparison.Reference(
                bars,
                2,
                fast ? 1 : 2,
                2,
                ClassicAverageMethod.Sma,
                ClassicAverageMethod.Sma,
                false,
                0
            );
            ComparisonVerifier.Compare(
                ClassicStochasticComparison.Series(reference),
                ClassicStochasticComparison.Owned(
                    bars,
                    2,
                    fast ? 1 : 2,
                    2,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma,
                    false,
                    0
                ),
                "extreme ranges",
                IndicatorErrorBudget.Exact
            );
            Assert.All(
                reference.SelectMany(v => v).Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            if (scale > 1)
                Assert.Contains(expected.Values[1], double.IsNaN);
            else
                Assert.All(expected.Values[1], v => Assert.Equal(0, v));
        }
    }

    [Fact]
    public void NativeShortBuffersAndPublicBoundsAreExplicit()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var input = new[] { 1d, 2, 3 };
        var a = new double[3];
        var b = new double[3];
        foreach (var fast in new[] { false, true })
        {
            Assert.Equal(
                TaCore.RetCode.OutOfRangeParam,
                ClassicStochasticComparison.NativeCall(
                    input,
                    input,
                    input,
                    fast,
                    System.Range.All,
                    a,
                    b,
                    out _,
                    3,
                    1,
                    1,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                ClassicStochasticComparison.NativePacked(
                    input,
                    input,
                    input,
                    fast,
                    3,
                    1,
                    1,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma,
                    false,
                    0,
                    0,
                    2
                )
            );
            Assert.Equal(
                TaCore.RetCode.BadParam,
                ClassicStochasticComparison.NativeCall(
                    input,
                    input,
                    input,
                    fast,
                    System.Range.All,
                    a,
                    b,
                    out _,
                    0,
                    1,
                    1,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma
                )
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                ClassicStochasticComparison.NativeCall(
                    input,
                    input,
                    input,
                    fast,
                    System.Range.All,
                    a,
                    b,
                    out var empty,
                    30,
                    3,
                    3,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Sma
                )
            );
            Assert.Equal(0, empty.End.Value);
        }
        var bars = ComparisonVerifier.Fixture("walk", 3).IndicatorBars;
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var huge = new ClassicStochastic(
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                method,
                method,
                true,
                int.MaxValue
            );
            Assert.Equal(int.MaxValue, huge.WarmupBars);
            var output = ClassicStochasticComparison.Owned(
                bars,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                method,
                method,
                true,
                int.MaxValue
            );
            Assert.All(output.Outputs["K"].Present!, v => Assert.False(v));
            var identity = ClassicStochasticComparison.Owned(
                bars,
                1,
                1,
                1,
                method,
                method,
                true,
                int.MaxValue
            );
            Assert.All(identity.Outputs["K"].Present!, v => Assert.True(v));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicStochastic(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicStochastic(kPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicStochastic(dPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicStochastic(kMethod: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicStochastic(dMethod: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicStochastic(suppression: -1));
    }

    [Fact]
    public async Task ChainingAndBothOutputMasksAndConfigurationMutationsAreDetected()
    {
        var data = CompetitorData.Create(100);
        var owner = new ClassicStochastic(
            3,
            2,
            4,
            ClassicAverageMethod.Tema,
            ClassicAverageMethod.Ema,
            true,
            2
        );
        owner.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(owner)
            .BuildAsync();
        var source = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, source[i], b.Volume)
            )
            .ToArray();
        var expected = ClassicStochasticComparison.Reference(
            bars,
            3,
            2,
            4,
            ClassicAverageMethod.Tema,
            ClassicAverageMethod.Ema,
            true,
            2
        );
        for (var j = 0; j < 2; j++)
        {
            Assert.Equal(expected[j].Select(v => v ?? 0), run[owner.Outputs[j]].ToArray());
            Assert.Equal(
                expected[j].Select(v => v.HasValue ? 1d : 0),
                run[owner.Outputs[j + 2]].ToArray()
            );
        }
        using var settings = new ClassicAverageComparison.Settings(true, 2);
        foreach (var fast in new[] { false, true })
        {
            var pair = ClassicStochasticComparison.Pair(
                fast,
                3,
                2,
                4,
                ClassicAverageMethod.Tema,
                ClassicAverageMethod.Ema,
                true,
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
                    ClassicStochasticComparison.Pair(
                        fast,
                        4,
                        2,
                        4,
                        ClassicAverageMethod.Tema,
                        ClassicAverageMethod.Ema,
                        true,
                        2
                    ),
                    ClassicStochasticComparison.Pair(
                        fast,
                        3,
                        2,
                        4,
                        ClassicAverageMethod.Tema,
                        ClassicAverageMethod.Sma,
                        true,
                        2
                    ),
                    ClassicStochasticComparison.Pair(
                        fast,
                        3,
                        2,
                        4,
                        ClassicAverageMethod.Tema,
                        ClassicAverageMethod.Ema,
                        false,
                        2
                    ),
                    ClassicStochasticComparison.Pair(
                        fast,
                        3,
                        2,
                        4,
                        ClassicAverageMethod.Tema,
                        ClassicAverageMethod.Ema,
                        true,
                        0
                    ),
                }
            )
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(pair with { Library = wrong.Library }, data, 20)
                );
            if (!fast)
            {
                var wrong = ClassicStochasticComparison.Pair(
                    false,
                    3,
                    2,
                    4,
                    ClassicAverageMethod.Sma,
                    ClassicAverageMethod.Ema,
                    true,
                    2
                );
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(pair with { Library = wrong.Library }, data, 20)
                );
            }
        }
    }
}
