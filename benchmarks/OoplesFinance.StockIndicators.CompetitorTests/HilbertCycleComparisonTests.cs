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
public sealed class HilbertCycleComparisonTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 3)]
    [InlineData(false, 3)]
    public async Task RationalContractsCoverReadingsAndLifecycle(bool periodOnly, int suppression)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                periodOnly ? typeof(HilbertCyclePeriod) : typeof(HilbertPhasor),
                "Hilbert readings",
                () => HilbertCycleComparison.Indicator(periodOnly, suppression)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void NativeFloatDoubleSubrangesAndAliasesMatch()
    {
        foreach (var periodOnly in new[] { false, true })
        foreach (var suppression in new[] { 0, 1, 7 })
        foreach (var start in new[] { 0, 1, 100, 101 })
        {
            using var settings = new HilbertCycleComparison.Settings(periodOnly, suppression);
            CheckNative<double>(periodOnly, suppression, start);
            CheckNative<float>(periodOnly, suppression, start);
        }
    }

    private static void CheckNative<T>(bool periodOnly, int suppression, int start)
        where T : IFloatingPointIeee754<T>
    {
        var prices = CompetitorData.Create(190).Closes.Select(T.CreateChecked).ToArray();
        var expected = HilbertCycleComparison.NativePacked(
            prices,
            periodOnly,
            suppression,
            start,
            189
        );
        for (var alias = -1; alias < (periodOnly ? 1 : 2); alias++)
        {
            var input = prices.ToArray();
            var a = alias == 0 ? input : new T[190];
            var b = alias == 1 ? input : new T[190];
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleComparison.NativeCall(
                    input,
                    periodOnly,
                    start..189,
                    a,
                    b,
                    out var range
                )
            );
            Assert.Equal(Math.Max(start, 32 + suppression), range.Start.Value);
            var count = range.End.Value - range.Start.Value;
            Assert.Equal(expected[0], a.Take(count));
            if (!periodOnly)
                Assert.Equal(expected[1], b.Take(count));
        }
        if (!periodOnly)
        {
            var both = new T[190];
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleComparison.NativeCall(
                    prices,
                    false,
                    start..189,
                    both,
                    both,
                    out var range
                )
            );
            Assert.Equal(expected[0], both.Take(range.End.Value - range.Start.Value));
        }
    }

    [Fact]
    public void StartupSuppressionAndHugeSuppressionKeepBoundedState()
    {
        foreach (var periodOnly in new[] { false, true })
        foreach (var suppression in new[] { 0, 3, int.MaxValue })
        {
            var owner = HilbertCycleComparison.Indicator(periodOnly, suppression);
            Assert.Equal((int)Math.Min(int.MaxValue, 32L + suppression), owner.WarmupBars);
            var series = HilbertCycleComparison.Owned(
                ComparisonVerifier.Fixture("zero", 80).CloseBars,
                periodOnly,
                suppression
            );
            foreach (var output in series.Outputs.Values)
                Assert.Equal(
                    Enumerable.Range(0, 80).Select(i => i >= 32L + suppression),
                    output.Present!
                );
            if (!periodOnly)
                foreach (var output in series.Outputs.Values)
                    Assert.All(output.Values.Where(double.IsFinite), v => Assert.Equal(0, v));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new HilbertCyclePeriod(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HilbertPhasor(-1));
        foreach (var periodOnly in new[] { false, true })
        {
            using var settings = new HilbertCycleComparison.Settings(periodOnly, 0);
            var a = new double[40];
            var b = new double[40];
            Assert.Equal(
                TaCore.RetCode.OutOfRangeParam,
                HilbertCycleComparison.NativeCall([1d], periodOnly, System.Range.All, a, b, out _)
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleComparison.NativeCall(
                    [1d, 2, 3],
                    periodOnly,
                    System.Range.All,
                    a,
                    b,
                    out var empty
                )
            );
            Assert.Equal(0, empty.End.Value);
            using var oversized = new HilbertCycleComparison.Settings(periodOnly, int.MaxValue);
            Assert.Equal(
                unchecked(int.MaxValue + 32),
                periodOnly ? Functions.HtDcPeriodLookback() : Functions.HtPhasorLookback()
            );
        }
    }

    [Fact]
    public async Task WideProductsAndSubnormalFiltersFollowOwnedReferences()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        foreach (var shape in new[] { 0, 1, 2 })
        {
            var prices = Enumerable
                .Range(0, 150)
                .Select(i =>
                    shape == 0 ? scale
                    : shape == 1 ? (i % 2 == 0 ? scale : -scale)
                    : scale * Math.Sin(i * .4)
                )
                .ToArray();
            var bars = BarsOf(prices);
            foreach (var periodOnly in new[] { false, true })
            {
                var expected = HilbertCycleComparison.Reference(prices, periodOnly, 0);
                if (
                    expected
                        .SelectMany(row => row)
                        .Any(v => v.HasValue && !double.IsFinite(v.Value))
                )
                    await Assert.ThrowsAsync<IndicatorOutputException>(() =>
                        new StockIndicatorBuilder()
                            .ConfigureSource(Bars.From(bars))
                            .ConfigureIndicators(HilbertCycleComparison.Indicator(periodOnly, 0))
                            .BuildAsync()
                    );
                else
                    ComparisonVerifier.Compare(
                        HilbertCycleComparison.Series(expected, periodOnly),
                        HilbertCycleComparison.Owned(bars, periodOnly, 0),
                        "wide Hilbert " + periodOnly,
                        IndicatorErrorBudget.Exact
                    );
                if (periodOnly)
                    Assert.All(
                        expected[0].Where(v => v.HasValue),
                        v => Assert.InRange(v!.Value, 0, 50.000000000001)
                    );
            }
        }
    }

    [Fact]
    public void NativeNonfiniteOutputsAreRetained()
    {
        var prices = Enumerable.Repeat(double.MaxValue, 100).ToArray();
        foreach (var periodOnly in new[] { false, true })
        {
            using var settings = new HilbertCycleComparison.Settings(periodOnly, 0);
            var expected = HilbertCycleComparison.NativePacked(prices, periodOnly, 0, 0, 99);
            var a = new double[100];
            var b = new double[100];
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleComparison.NativeCall(
                    prices,
                    periodOnly,
                    System.Range.All,
                    a,
                    b,
                    out var range
                )
            );
            Assert.Equal(expected[0], a.Take(range.End.Value - range.Start.Value));
            if (!periodOnly)
                Assert.Equal(expected[1], b.Take(range.End.Value - range.Start.Value));
            Assert.Contains(expected.SelectMany(row => row), double.IsNaN);
        }
    }

    [Fact]
    public async Task ChainingAndValuePresenceAndSuppressionMutationsAreDetected()
    {
        var data = CompetitorData.Create(100);
        foreach (var periodOnly in new[] { false, true })
        {
            var owner = HilbertCycleComparison.Indicator(periodOnly, 3);
            owner.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(owner)
                .BuildAsync();
            var expected = HilbertCycleComparison.Reference(
                FixedWeightedComparison.Stage(data.Closes, 3, false),
                periodOnly,
                3
            );
            var count = periodOnly ? 1 : 2;
            for (var j = 0; j < count; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[owner.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[owner.Outputs[j + count]].ToArray()
                );
            }
            using var settings = new HilbertCycleComparison.Settings(periodOnly, 3);
            var pair = HilbertCycleComparison.Pair(periodOnly, 3);
            foreach (var native in new[] { false, true })
            foreach (var mask in new[] { false, true })
            foreach (var name in HilbertCycleComparison.Names(periodOnly))
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
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = HilbertCycleComparison.Pair(periodOnly, 0).Library,
                    },
                    data,
                    20
                )
            );
        }
    }

    private static Bar[] BarsOf(double[] values) =>
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
