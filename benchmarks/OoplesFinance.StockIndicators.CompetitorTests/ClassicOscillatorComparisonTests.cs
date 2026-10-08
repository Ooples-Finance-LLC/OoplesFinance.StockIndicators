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
public sealed class ClassicOscillatorComparisonTests
{
    [Theory]
    [InlineData(ClassicAverageMethod.Sma, false)]
    [InlineData(ClassicAverageMethod.Ema, true)]
    [InlineData(ClassicAverageMethod.Tema, true)]
    [InlineData(ClassicAverageMethod.T3, true)]
    public async Task RationalContractsCoverDifferenceAndPercentageLifecycle(
        ClassicAverageMethod method,
        bool percent
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicPriceOscillator),
                "classical price oscillator",
                () => new ClassicPriceOscillator(3, 5, method, percent, true, 2)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryMethodPeriodOrderCompatibilityAndSuppressionHasIndependentOutputs()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var percent in new[] { false, true })
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            foreach (var periods in new[] { (2, 3), (3, 2), (3, 3), (5, 10) })
            {
                var pair = ClassicOscillatorComparison.Pair(
                    percent,
                    periods.Item1,
                    periods.Item2,
                    method,
                    first,
                    suppression
                );
                var lookback = percent
                    ? Functions.PpoLookback(periods.Item1, periods.Item2, (TaCore.MAType)method)
                    : Functions.ApoLookback(periods.Item1, periods.Item2, (TaCore.MAType)method);
                Assert.Equal(
                    ClassicAverageComparison.First(
                        Math.Max(periods.Item1, periods.Item2),
                        method,
                        suppression
                    ),
                    lookback
                );
                ComparisonVerifier.Check(pair, CompetitorData.Create(100), 20);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 70), 20);
            }
        }
    }

    [Fact]
    public void FloatAndDoubleSubrangesAndInPlaceBuffersRetainNativeStages()
    {
        var prices = CompetitorData.Create(180).Closes;
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var percent in new[] { false, true })
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var start in new[] { 0, 100 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            CheckNative(prices, percent, method, first, suppression, start);
            CheckNative(
                prices.Select(v => (float)v).ToArray(),
                percent,
                method,
                first,
                suppression,
                start
            );
        }
    }

    private static void CheckNative<T>(
        T[] prices,
        bool percent,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int start
    )
        where T : IFloatingPointIeee754<T>
    {
        var expected = ClassicOscillatorComparison.NativePacked(
            prices,
            percent,
            5,
            3,
            method,
            first,
            suppression,
            start,
            prices.Length - 1
        );
        foreach (var alias in new[] { false, true })
        {
            var input = prices.ToArray();
            var output = alias ? input : new T[input.Length];
            System.Range range;
            var code = percent
                ? Functions.Ppo<T>(
                    input,
                    new System.Range(start, prices.Length - 1),
                    output,
                    out range,
                    5,
                    3,
                    (TaCore.MAType)method
                )
                : Functions.Apo<T>(
                    input,
                    new System.Range(start, prices.Length - 1),
                    output,
                    out range,
                    5,
                    3,
                    (TaCore.MAType)method
                );
            Assert.Equal(TaCore.RetCode.Success, code);
            Assert.Equal(expected.Length, range.End.Value - range.Start.Value);
            Assert.Equal(expected, output.Take(expected.Length));
            if (expected.Length > 0)
                Assert.Equal(
                    Math.Max(start, ClassicAverageComparison.First(5, method, suppression)),
                    range.Start.Value
                );
        }
    }

    [Fact]
    public void ZeroSlowAverageHasAZeroPercentageAndIdenticalAveragesCancel()
    {
        var prices = new[] { 1d, -1, 0, 3, -3, 0, 1, -1 };
        foreach (var percent in new[] { false, true })
        {
            var result = ClassicOscillatorComparison
                .Owned(BarsOf(prices), percent, 2, 3, ClassicAverageMethod.Sma, false, 0)
                .Outputs["Value"];
            Assert.Equal(
                new[] { false, false, true, true, true, true, true, true },
                result.Present
            );
            if (percent)
                Assert.Equal(0, result.Values[2]);
            else
                Assert.Equal(-.5, result.Values[2]);
            foreach (var method in Enum.GetValues<ClassicAverageMethod>())
            {
                var same = ClassicOscillatorComparison
                    .Owned(CompetitorData.Create(100).IndicatorBars, percent, 3, 3, method, true, 2)
                    .Outputs["Value"];
                Assert.All(same.Values.Where((_, i) => same.Present![i]), v => Assert.Equal(0, v));
            }
        }
    }

    [Fact]
    public void FinitePercentageSurvivesOverflowingDifferenceAndNativeNonfiniteStagesStayVisible()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var values = new[]
        {
            -double.MaxValue,
            -double.MaxValue,
            -double.MaxValue,
            double.MaxValue,
            double.MaxValue,
        };
        var actual = ClassicOscillatorComparison.Owned(
            BarsOf(values),
            true,
            2,
            5,
            ClassicAverageMethod.Sma,
            false,
            0
        );
        var expected = VolumePriceComparison.Mask(
            ClassicOscillatorComparison.Reference(
                values,
                true,
                2,
                5,
                ClassicAverageMethod.Sma,
                false,
                0
            )
        );
        ComparisonVerifier.Compare(
            expected,
            actual,
            "oversized difference",
            IndicatorErrorBudget.Exact
        );
        Assert.True(double.IsFinite(actual.Outputs["Value"].Values[^1]));
        Assert.InRange(actual.Outputs["Value"].Values[^1], -600.000000001, -599.999999999);
        var native = new double[values.Length];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Ppo<double>(values, System.Range.All, native, out _, 2, 5)
        );
        Assert.Equal(
            ClassicOscillatorComparison.NativePacked(
                values,
                true,
                2,
                5,
                ClassicAverageMethod.Sma,
                false,
                0,
                0,
                values.Length - 1
            )[0],
            native[0]
        );
        Assert.True(double.IsNaN(native[0]));
        var tiny = Enumerable.Range(0, 45).Select(i => (i % 7 - 3) * double.Epsilon).ToArray();
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var percent in new[] { false, true })
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    ClassicOscillatorComparison.Reference(tiny, percent, 2, 3, method, false, 0)
                ),
                ClassicOscillatorComparison.Owned(BarsOf(tiny), percent, 2, 3, method, false, 0),
                "subnormal oscillator",
                IndicatorErrorBudget.Exact
            );
        var huge = ClassicOscillatorComparison
            .Owned(
                BarsOf([1, 2, 3]),
                true,
                int.MaxValue,
                int.MaxValue,
                ClassicAverageMethod.Tema,
                true,
                int.MaxValue
            )
            .Outputs["Value"];
        Assert.All(huge.Present!, v => Assert.False(v));
    }

    [Fact]
    public void WideExtrapolatedComponentsRemainAvailableToFinitePercentage()
    {
        var prices = Enumerable
            .Repeat(-double.MaxValue, 40)
            .Concat(Enumerable.Repeat(double.MaxValue, 40))
            .ToArray();
        var bound = MoneyFlowReferenceArithmetic.Units(double.MaxValue);
        foreach (
            var method in new[]
            {
                ClassicAverageMethod.Dema,
                ClassicAverageMethod.Tema,
                ClassicAverageMethod.T3,
            }
        )
        {
            var fast = ClassicAverageComparison.ExtendedReference(prices, 3, method, true, 0);
            Assert.Contains(fast, v => v.HasValue && BigInteger.Abs(v.Value) > bound);
            var expected = VolumePriceComparison.Mask(
                ClassicOscillatorComparison.Reference(prices, true, 3, 5, method, true, 0)
            );
            var actual = ClassicOscillatorComparison.Owned(
                BarsOf(prices),
                true,
                3,
                5,
                method,
                true,
                0
            );
            ComparisonVerifier.Compare(
                expected,
                actual,
                "wide extrapolated component",
                IndicatorErrorBudget.Exact
            );
            Assert.All(
                actual.Outputs["Value"].Values.Where((_, i) => actual.Outputs["Value"].Present![i]),
                v => Assert.True(double.IsFinite(v))
            );
        }
    }

    [Fact]
    public async Task ChainingAndOutputPresenceFormulaMutationsAreChecked()
    {
        var d = CompetitorData.Create(90);
        foreach (var percent in new[] { false, true })
        {
            var indicator = new ClassicPriceOscillator(
                3,
                5,
                ClassicAverageMethod.Ema,
                percent,
                true,
                2
            );
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(d.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = ClassicOscillatorComparison.Reference(
                FixedWeightedComparison.Stage(d.Closes, 3, false),
                percent,
                3,
                5,
                ClassicAverageMethod.Ema,
                true,
                2
            );
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
            using var settings = new ClassicAverageComparison.Settings(true, 2);
            foreach (var method in Enum.GetValues<ClassicAverageMethod>())
            {
                var pair = ClassicOscillatorComparison.Pair(percent, 3, 5, method, true, 2);
                foreach (var native in new[] { false, true })
                foreach (var mask in new[] { false, true })
                {
                    ComparisonSeries Bad(CompetitorData data, int p)
                    {
                        var result = native ? pair.Competitor(data, p) : pair.Ooples(data, p);
                        if (mask)
                            result.Outputs["Value"].Present![^1] = false;
                        else
                            result.Outputs["Value"].Values[^1] += 1;
                        return result;
                    }
                    Assert.Throws<InvalidOperationException>(() =>
                        ComparisonVerifier.Check(
                            native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                            d,
                            20
                        )
                    );
                }
            }
            var defaultPair = ClassicOscillatorComparison.Pair(
                percent,
                3,
                5,
                ClassicAverageMethod.Sma,
                true,
                2
            );
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    defaultPair with
                    {
                        Library = ClassicOscillatorComparison
                            .Pair(!percent, 3, 5, ClassicAverageMethod.Sma, true, 2)
                            .Library,
                    },
                    d,
                    20
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    defaultPair with
                    {
                        Library = ClassicOscillatorComparison
                            .Pair(percent, 2, 5, ClassicAverageMethod.Sma, true, 2)
                            .Library,
                    },
                    d,
                    20
                )
            );
        }
    }

    [Fact]
    public void InvalidNativePeriodsRangesAndOwnedConfigurationAreExplicit()
    {
        var input = new[] { 1d, 3, 8 };
        var output = new double[3];
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Apo<double>(input, System.Range.All, output, out _, 1, 3)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Ppo<double>(input, System.Range.All, output, out _, 3, 1)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Ppo<double>(input, System.Range.All, output, out _, 2, 3, (TaCore.MAType)999)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Apo<double>([1d], System.Range.All, output, out _, 2, 3)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Ppo<double>([1d], System.Range.All, output, out _, 2, 3)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Ppo<double>(input, 4..5, output, out _, 2, 3)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Apo<double>(input, System.Range.All, output, out var empty, 12, 26)
        );
        Assert.Equal(0, empty.End.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicPriceOscillator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicPriceOscillator(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicPriceOscillator(method: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicPriceOscillator(suppression: -1)
        );
        var identity = ClassicOscillatorComparison
            .Owned(BarsOf(input), true, 1, 1, ClassicAverageMethod.Mama, true, int.MaxValue)
            .Outputs["Value"];
        Assert.Equal(new[] { 0d, 0, 0 }, identity.Values);
        Assert.All(identity.Present!, v => Assert.True(v));
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
