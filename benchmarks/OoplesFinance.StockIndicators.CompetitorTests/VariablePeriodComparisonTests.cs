using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class VariablePeriodComparisonTests
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
    public async Task RationalContractsCoverVariableSelectionAndLifecycle(
        ClassicAverageMethod method
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(VariablePeriodClassicAverage),
                "variable periods " + method,
                () =>
                    new VariablePeriodClassicAverage(
                        VariablePeriodComparison.Selection,
                        2,
                        5,
                        method,
                        true,
                        2
                    )
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void EveryMethodLimitSeedAndSuppressionMatchesIndependentModels()
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var limits in new[] { (2, 2), (2, 5), (3, 10) })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            var pair = VariablePeriodComparison.Pair(
                limits.Item1,
                limits.Item2,
                method,
                first,
                suppression
            );
            ComparisonVerifier.Check(pair, CompetitorData.Create(130), 20);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 100), 20);
        }
    }

    [Fact]
    public void FloatDoubleShiftedRangesAndBothInputAliasesMatchNativeReference()
    {
        var prices = CompetitorData.Create(180).Closes;
        var periods = Enumerable
            .Range(0, prices.Length)
            .Select(i =>
                new[]
                {
                    -100d,
                    2.9,
                    3.1,
                    7.99,
                    1e100,
                    double.NaN,
                    double.PositiveInfinity,
                    double.NegativeInfinity,
                }[i % 8]
            )
            .ToArray();
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        foreach (var first in new[] { false, true })
        foreach (var suppression in new[] { 0, 2 })
        foreach (var start in new[] { 0, 100 })
        {
            using var settings = new ClassicAverageComparison.Settings(first, suppression);
            CheckNative(prices, periods, method, first, suppression, start);
            CheckNative(
                prices.Select(v => (float)v).ToArray(),
                periods.Select(v => (float)v).ToArray(),
                method,
                first,
                suppression,
                start
            );
        }
    }

    private static void CheckNative<T>(
        T[] prices,
        T[] periods,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int start
    )
        where T : IFloatingPointIeee754<T>
    {
        var expected = VariablePeriodComparison.NativePacked(
            prices,
            periods,
            2,
            10,
            method,
            first,
            suppression,
            start,
            prices.Length - 1
        );
        foreach (var alias in new[] { 0, 1, 2 })
        {
            var input = prices.ToArray();
            var selection = periods.ToArray();
            var output =
                alias == 1 ? input
                : alias == 2 ? selection
                : new T[prices.Length];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.Mavp<T>(
                    input,
                    selection,
                    new System.Range(start, prices.Length - 1),
                    output,
                    out var range,
                    2,
                    10,
                    (TaCore.MAType)method
                )
            );
            Assert.Equal(expected.Start, range.Start.Value);
            Assert.Equal(expected.Values.Length, range.End.Value - range.Start.Value);
            Assert.Equal(expected.Values, output.Take(expected.Values.Length));
        }
    }

    [Fact]
    public void LateAndReturningPeriodsPreserveTheCommonSeedBoundary()
    {
        var data = CompetitorData.Create(120);
        foreach (
            var method in new[]
            {
                ClassicAverageMethod.Ema,
                ClassicAverageMethod.Dema,
                ClassicAverageMethod.Tema,
                ClassicAverageMethod.Kama,
                ClassicAverageMethod.T3,
            }
        )
        foreach (var first in new[] { false, true })
        {
            var switchIndex = (int)ClassicAverageComparison.First(10, method, 2) + 3;
            var selections = Enumerable
                .Range(0, data.Count)
                .Select(i =>
                    i < switchIndex ? 10d
                    : i < switchIndex + 20 ? 3.99
                    : 10d
                )
                .ToArray();
            var bars = data
                .IndicatorBars.Select(
                    (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, selections[i])
                )
                .ToArray();
            var expected = VariablePeriodComparison.Reference(
                data.Closes,
                selections,
                2,
                10,
                method,
                first,
                2
            );
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(expected),
                VariablePeriodComparison.Owned(bars, 2, 10, method, first, 2, b => b.Volume),
                "late/returning selection",
                IndicatorErrorBudget.Exact
            );
            var unaligned = ClassicAverageComparison.Reference(data.Closes, 3, method, first, 2);
            if (!(first && method == ClassicAverageMethod.Ema))
                Assert.NotEqual(unaligned[switchIndex], expected[switchIndex]);
        }
    }

    [Fact]
    public void FiniteSelectionClampingExtremeValuesAndHugePeriodsAreExplicit()
    {
        foreach (
            var prices in new[]
            {
                new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue },
                new[] { 0d, double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon },
            }
        )
        foreach (var selection in new[] { -double.MaxValue, 1.99, 2.99, double.MaxValue })
        {
            var expected = VariablePeriodComparison.Reference(
                prices,
                Enumerable.Repeat(selection, prices.Length).ToArray(),
                1,
                3,
                ClassicAverageMethod.Sma,
                false,
                0
            );
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(expected),
                VariablePeriodComparison.Owned(
                    BarsOf(prices),
                    1,
                    3,
                    ClassicAverageMethod.Sma,
                    false,
                    0,
                    _ => selection
                ),
                "finite clamp",
                IndicatorErrorBudget.Exact
            );
        }
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            var shortResult = VariablePeriodComparison.Owned(
                BarsOf([1, 2, 3]),
                1,
                int.MaxValue,
                method,
                true,
                int.MaxValue
            );
            Assert.All(shortResult.Outputs["Value"].Present!, value => Assert.False(value));
            var identity = VariablePeriodComparison.Owned(
                BarsOf([1, 2, 3]),
                1,
                1,
                method,
                true,
                int.MaxValue
            );
            Assert.Equal(new[] { 1d, 2, 3 }, identity.Outputs["Value"].Values);
        }
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var input = Enumerable.Repeat(double.MaxValue, 6).ToArray();
        var output = new double[input.Length];
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mavp<double>(
                input,
                Enumerable.Repeat(2d, 6).ToArray(),
                System.Range.All,
                output,
                out var range,
                2,
                2
            )
        );
        var reference = VariablePeriodComparison.NativePacked(
            input,
            Enumerable.Repeat(2d, 6).ToArray(),
            2,
            2,
            ClassicAverageMethod.Sma,
            false,
            0,
            0,
            5
        );
        Assert.Equal(reference.Values, output.Take(range.End.Value - range.Start.Value));
        Assert.True(double.IsPositiveInfinity(output[0]));
        Assert.All(
            VariablePeriodComparison
                .Owned(BarsOf(input), 2, 2, ClassicAverageMethod.Sma, false, 0)
                .Outputs["Value"]
                .Values.Skip(1),
            value => Assert.Equal(double.MaxValue, value)
        );
    }

    [Fact]
    public void InvalidSelectionDoesNotAdvanceTheStateAndResetDropsCachedHistory()
    {
        var invalid = false;
        var calls = 0;
        var indicator = new VariablePeriodClassicAverage(
            b =>
            {
                calls++;
                return invalid ? double.NaN : 2;
            },
            2,
            3
        );
        var state = (IMultiOutputState)
            typeof(VariablePeriodClassicAverage)
                .GetMethod("CreateState", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(indicator, null)!;
        var bars = BarsOf([1, 2, 3, 4]);
        var result = new double[2];
        state.Update(bars[0], result);
        state.Update(bars[1], result);
        Assert.Equal(0, calls);
        invalid = true;
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bars[2], result));
        invalid = false;
        state.Update(bars[2], result);
        Assert.Equal(new[] { 2.5, 1 }, result);
        state.Update(bars[3], result);
        Assert.Equal(new[] { 3.5, 1 }, result);
        state.Reset();
        state.Update(bars[0], result);
        Assert.Equal(new[] { 0d, 0 }, result);
        state.Update(bars[1], result);
        state.Update(bars[2], result);
        Assert.Equal(new[] { 2.5, 1 }, result);
        ((IDisposable)state).Dispose();
    }

    [Fact]
    public void NativeBoundariesAndOwnerValidationAreRecorded()
    {
        using var settings = new ClassicAverageComparison.Settings(false, 0);
        var input = new[] { 1d, 2, 3 };
        var output = new double[3];
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Mavp<double>(input, input, System.Range.All, output, out _, 2, 30)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Mavp<double>(input, input, System.Range.All, output, out _, 1, 2)
        );
        Assert.Equal(
            TaCore.RetCode.BadParam,
            Functions.Mavp<double>(
                input,
                input,
                System.Range.All,
                output,
                out _,
                2,
                2,
                (TaCore.MAType)999
            )
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Mavp<double>([1d], [2d], System.Range.All, output, out _, 2, 2)
        );
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.Mavp<double>(input, [2d], 0..2, output, out _, 2, 2)
        );
        Assert.Throws<ArgumentException>(() =>
            Functions.Mavp<double>(input, input, System.Range.All, output, out _, 3, 2)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.Mavp<double>(input, input, System.Range.All, output, out var none, 2, 4)
        );
        Assert.Equal(0, none.End.Value);
        Assert.Throws<ArgumentNullException>(() => new VariablePeriodClassicAverage(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VariablePeriodClassicAverage(_ => 2, 0, 3)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VariablePeriodClassicAverage(_ => 2, 4, 3)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VariablePeriodClassicAverage(_ => 2, method: (ClassicAverageMethod)999)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VariablePeriodClassicAverage(_ => 2, suppression: -1)
        );
    }

    [Fact]
    public async Task ChainedSelectionsAndValueMaskMethodSeedMutationsAreDetected()
    {
        var data = CompetitorData.Create(130);
        var indicator = new VariablePeriodClassicAverage(
            VariablePeriodComparison.Selection,
            2,
            5,
            ClassicAverageMethod.Tema,
            true,
            2
        );
        indicator.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var chained = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var expected = VariablePeriodComparison.Reference(
            chained,
            BarsOf(chained).Select(VariablePeriodComparison.Selection).ToArray(),
            2,
            5,
            ClassicAverageMethod.Tema,
            true,
            2
        );
        Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Average].ToArray());
        Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0), run[indicator.IsDefined].ToArray());
        using var settings = new ClassicAverageComparison.Settings(true, 2);
        var pair = VariablePeriodComparison.Pair(2, 5, ClassicAverageMethod.Tema, true, 2);
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (mask)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
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
                VariablePeriodComparison.Pair(2, 5, ClassicAverageMethod.Tema, false, 2),
                VariablePeriodComparison.Pair(2, 5, ClassicAverageMethod.Sma, true, 2),
                VariablePeriodComparison.Pair(3, 5, ClassicAverageMethod.Tema, true, 2),
            }
        )
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(pair with { Library = wrong.Library }, data, 20)
            );
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
