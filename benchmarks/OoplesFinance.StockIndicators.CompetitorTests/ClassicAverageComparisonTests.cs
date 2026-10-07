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
public sealed class ClassicAverageComparisonTests
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
    public async Task IndependentRationalContractsCoverEveryMethodLifecycle(
        ClassicAverageMethod method
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ClassicMovingAverage),
                "classical average " + method,
                () => new ClassicMovingAverage(3, method, true, 2)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryMethodStartupCompatibilityAndSuppressionMatchesIndependentModels()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            var pair = ClassicAverageComparison.Pair(method, first, suppression);
            foreach (var p in new[] { 1, 2, 3, 10 })
            {
                Assert.Equal(
                    ClassicAverageComparison.First(p, method, suppression),
                    Functions.MaLookback(p, (TaCore.MAType)method)
                );
                ComparisonVerifier.Check(pair, CompetitorData.Create(90), p);
                foreach (var shape in ComparisonVerifier.Shapes)
                    ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 65), p);
            }
        }
    }

    [Fact]
    public void GenericFloatShiftedRangesAndAliasesPreserveNativeStages()
    {
        var prices = CompetitorData.Create(140).Closes;
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var p in new[] { 1, 2, 3, 8 })
        foreach (var start in new[] { 0, 77 })
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
        var expected = ClassicNativeReference.Packed(
            prices,
            p,
            method,
            first,
            suppression,
            start,
            prices.Length - 1
        );
        foreach (var alias in new[] { false, true })
        {
            var input = prices.ToArray();
            var output = alias ? input : new T[prices.Length];
            var code = Functions.Ma<T>(
                input,
                new System.Range(start, prices.Length - 1),
                output,
                out var range,
                p,
                (TaCore.MAType)method
            );
            Assert.Equal(TaCore.RetCode.Success, code);
            Assert.Equal(expected.Length, range.End.Value - range.Start.Value);
            Assert.Equal(expected, output.Take(expected.Length));
            if (expected.Length > 0)
                Assert.Equal(
                    Math.Max(start, ClassicAverageComparison.First(p, method, suppression)),
                    range.Start.Value
                );
        }
    }

    [Fact]
    public void InvalidRangesIdentityAndUnknownMethodsRetainNativeBoundaries()
    {
        using var settings = new ClassicAverageComparison.Settings(true, 9);
        var prices = new[] { 1d, 3, 8 };
        var output = new double[3];
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Ma<double>(prices, System.Range.All, output, out _, 0)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Ma<double>(prices, System.Range.All, output, out _, 2, (TaCore.MAType)999)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Ma<double>(
                prices,
                System.Range.All,
                output,
                out var identity,
                1,
                (TaCore.MAType)999
            )
        );
        Assert.Equal(prices, output);
        Assert.Equal(3, identity.End.Value);
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Ma<double>([1d], System.Range.All, output, out _, 1)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Ma<double>(prices, 4..5, output, out _, 2)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Ma<double>(prices, System.Range.All, output, out var empty, 20)
        );
        Assert.Equal(0, empty.End.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicMovingAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClassicMovingAverage(1, (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassicMovingAverage(suppression: -1));
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var result = ClassicAverageComparison.Owned(
                BarsOf(prices),
                1,
                method,
                true,
                int.MaxValue
            );
            Assert.Equal(prices, result.Outputs["Value"].Values);
        }
    }

    [Fact]
    public void OversizedNativeWmaDivisorRemainsVisible()
    {
        const int p = 65536;
        var prices = Enumerable.Repeat(1d, p).ToArray();
        var output = new double[p];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Ma<double>(
                prices,
                System.Range.All,
                output,
                out var range,
                p,
                TaCore.MAType.Wma
            )
        );
        Assert.Equal(p - 1, range.Start.Value);
        Assert.Equal(p + 1d, output[0]);
        var owned = ClassicAverageComparison
            .Owned(BarsOf(prices), p, ClassicAverageMethod.Wma, false, 0)
            .Outputs["Value"];
        Assert.Equal(1, owned.Values[^1]);
        Assert.True(owned.Present![^1]);
    }

    [Fact]
    public void HugeLookbacksAndExtremeFiniteInputsStayBounded()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var values = Enumerable
                .Range(0, 45)
                .Select(i => (i % 3 - 1) * double.Epsilon)
                .ToArray();
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    ClassicAverageComparison.Reference(values, 3, method, false, 0)
                ),
                ClassicAverageComparison.Owned(BarsOf(values), 3, method, false, 0),
                "subnormal classical average",
                IndicatorErrorBudget.Exact
            );
            var huge = ClassicAverageComparison
                .Owned(BarsOf([1, 2, 3]), int.MaxValue, method, true, int.MaxValue)
                .Outputs["Value"];
            Assert.All(huge.Present!, v => Assert.False(v));
            var flat = Enumerable.Repeat(double.MaxValue, 40).ToArray();
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    ClassicAverageComparison.Reference(flat, 3, method, true, 0)
                ),
                ClassicAverageComparison.Owned(BarsOf(flat), 3, method, true, 0),
                "extreme classical average",
                IndicatorErrorBudget.Exact
            );
        }
    }

    [Fact]
    public async Task ChainingAndValuePresenceMethodAndSeedMutationsAreDetected()
    {
        var d = CompetitorData.Create(70);
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var indicator = new ClassicMovingAverage(3, method, true, 2);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(d.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = ClassicAverageComparison.Reference(
                FixedWeightedComparison.Stage(d.Closes, 3, false),
                3,
                method,
                true,
                2
            );
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Average].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
            using var settings = new ClassicAverageComparison.Settings(true, 2);
            var pair = ClassicAverageComparison.Pair(method, true, 2);
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
                        3
                    )
                );
            }
            var wrong =
                method == ClassicAverageMethod.Sma
                    ? ClassicAverageMethod.Wma
                    : ClassicAverageMethod.Sma;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = ClassicAverageComparison.Pair(wrong, true, 2).Library,
                    },
                    d,
                    3
                )
            );
            if (
                method
                is ClassicAverageMethod.Ema
                    or ClassicAverageMethod.Dema
                    or ClassicAverageMethod.Tema
            )
                Assert.Throws<InvalidOperationException>(() =>
                    ComparisonVerifier.Check(
                        pair with
                        {
                            Library = ClassicAverageComparison.Pair(method, false, 2).Library,
                        },
                        d,
                        3
                    )
                );
        }
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
