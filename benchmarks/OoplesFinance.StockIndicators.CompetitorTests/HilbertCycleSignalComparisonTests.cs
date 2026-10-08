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
public sealed class HilbertCycleSignalComparisonTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 7)]
    [InlineData(1, 7)]
    [InlineData(2, 7)]
    public async Task RationalContractsCoverSignalsAndLifecycle(int kind, int suppression)
    {
        var type = HilbertCycleSignalComparison.Indicator(kind, suppression).GetType();
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                type,
                "Hilbert cycle signal",
                () => HilbertCycleSignalComparison.Indicator(kind, suppression)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void NativeFloatDoubleSubrangesAndAliasesMatch()
    {
        for (var kind = 0; kind < 3; kind++)
            foreach (var suppression in new[] { 0, 1, 7 })
            foreach (var start in new[] { 0, 120, 121 })
            {
                using var settings = new HilbertCycleSignalComparison.Settings(kind, suppression);
                CheckNative<double>(kind, suppression, start);
                CheckNative<float>(kind, suppression, start);
            }
    }

    private static void CheckNative<T>(int kind, int suppression, int start)
        where T : IFloatingPointIeee754<T>
    {
        var prices = CompetitorData.Create(220).Closes.Select(T.CreateChecked).ToArray();
        var expected = HilbertCycleSignalComparison.NativePacked(
            prices,
            kind,
            suppression,
            start,
            219
        );
        for (
            var alias = -1;
            alias
                < (
                    kind == 2 ? 0
                    : kind == 1 ? 2
                    : 1
                );
            alias++
        )
        {
            var input = prices.ToArray();
            var a = alias == 0 ? input : new T[220];
            var b = alias == 1 ? input : new T[220];
            var trend = new int[220];
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleSignalComparison.NativeCall(
                    input,
                    kind,
                    start..219,
                    a,
                    b,
                    trend,
                    out var range
                )
            );
            Assert.Equal(Math.Max(start, 63 + suppression), range.Start.Value);
            var count = range.End.Value - range.Start.Value;
            Assert.Equal(
                expected[0],
                kind == 2 ? trend.Take(count).Select(T.CreateChecked) : a.Take(count)
            );
            if (kind == 1)
                Assert.Equal(expected[1], b.Take(count));
        }
        if (kind == 1)
        {
            var shared = new T[220];
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleSignalComparison.NativeCall(
                    prices,
                    kind,
                    start..219,
                    shared,
                    shared,
                    [],
                    out var range
                )
            );
            Assert.Equal(expected[1], shared.Take(range.End.Value - range.Start.Value));
        }
    }

    [Fact]
    public void WideSubnormalAndZeroInputsRemainFiniteWithExactVotes()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 0d, 1e-200 })
        foreach (var alternating in new[] { false, true })
        {
            var prices = Enumerable
                .Range(0, 180)
                .Select(i => alternating && i % 2 != 0 ? -scale : scale)
                .ToArray();
            for (var kind = 0; kind < 3; kind++)
            {
                var expected = HilbertCycleSignalComparison.Reference(prices, kind, 0);
                Assert.All(
                    expected.SelectMany(r => r).Where(v => v.HasValue),
                    v => Assert.True(double.IsFinite(v!.Value))
                );
                if (kind == 1)
                    Assert.All(
                        expected.SelectMany(r => r).Where(v => v.HasValue),
                        v => Assert.InRange(v!.Value, -1, 1)
                    );
                if (kind == 2)
                    Assert.All(
                        expected[0].Where(v => v.HasValue),
                        v => Assert.Contains(v!.Value, new[] { 0d, 1d })
                    );
                ComparisonVerifier.Compare(
                    HilbertCycleSignalComparison.Series(expected, kind),
                    HilbertCycleSignalComparison.Owned(BarsOf(prices), kind, 0),
                    "wide cycle signal",
                    IndicatorErrorBudget.Exact
                );
            }
        }
    }

    [Fact]
    public void NativeNonfiniteControlsRemainExplicit()
    {
        var prices = Enumerable.Repeat(double.MaxValue, 100).ToArray();
        for (var kind = 0; kind < 3; kind++)
        {
            using var settings = new HilbertCycleSignalComparison.Settings(kind, 0);
            var expected = HilbertCycleSignalComparison.NativePacked(prices, kind, 0, 0, 99);
            var a = new double[100];
            var b = new double[100];
            var trend = new int[100];
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleSignalComparison.NativeCall(
                    prices,
                    kind,
                    System.Range.All,
                    a,
                    b,
                    trend,
                    out var range
                )
            );
            var count = range.End.Value - range.Start.Value;
            Assert.Equal(
                expected[0],
                kind == 2 ? trend.Take(count).Select(v => (double)v) : a.Take(count)
            );
            if (kind == 1)
                Assert.Equal(expected[1], b.Take(count));
            if (kind < 2)
                Assert.All(expected.SelectMany(r => r), v => Assert.True(double.IsNaN(v)));
            else
                Assert.All(expected[0], v => Assert.Equal(1, v));
        }
    }

    [Fact]
    public void StartupHugeSuppressionAndPublicLimitsAreExplicit()
    {
        for (var kind = 0; kind < 3; kind++)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HilbertCycleSignalComparison.Indicator(kind, -1)
            );
            foreach (var suppression in new[] { 0, 7, int.MaxValue })
            {
                var owner = HilbertCycleSignalComparison.Indicator(kind, suppression);
                Assert.Equal((int)Math.Min(int.MaxValue, 63L + suppression), owner.WarmupBars);
                var series = HilbertCycleSignalComparison.Owned(
                    ComparisonVerifier.Fixture("zero", 100).CloseBars,
                    kind,
                    suppression
                );
                foreach (var output in series.Outputs.Values)
                    Assert.Equal(
                        Enumerable.Range(0, 100).Select(i => i >= 63L + suppression),
                        output.Present!
                    );
            }
            using var settings = new HilbertCycleSignalComparison.Settings(kind, 0);
            var a = new double[100];
            var b = new double[100];
            var t = new int[100];
            Assert.Equal(
                TaCore.RetCode.OutOfRangeParam,
                HilbertCycleSignalComparison.NativeCall(
                    [1d],
                    kind,
                    System.Range.All,
                    a,
                    b,
                    t,
                    out _
                )
            );
            Assert.Equal(
                TaCore.RetCode.Success,
                HilbertCycleSignalComparison.NativeCall(
                    [1d, 2, 3],
                    kind,
                    System.Range.All,
                    a,
                    b,
                    t,
                    out var empty
                )
            );
            Assert.Equal(0, empty.End.Value);
            using var large = new HilbertCycleSignalComparison.Settings(kind, int.MaxValue);
            Assert.Equal(
                unchecked(int.MaxValue + 63),
                kind == 0 ? Functions.HtDcPhaseLookback()
                    : kind == 1 ? Functions.HtSineLookback()
                    : Functions.HtTrendModeLookback()
            );
        }
    }

    [Fact]
    public async Task ChainingAndAllOutputAndConfigurationMutationsAreDetected()
    {
        var data = CompetitorData.Create(180);
        for (var kind = 0; kind < 3; kind++)
        {
            var owner = HilbertCycleSignalComparison.Indicator(kind, 7);
            owner.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(owner)
                .BuildAsync();
            var expected = HilbertCycleSignalComparison.Reference(
                FixedWeightedComparison.Stage(data.Closes, 3, false),
                kind,
                7
            );
            for (var j = 0; j < expected.Length; j++)
            {
                Assert.Equal(expected[j].Select(v => v ?? 0), run[owner.Outputs[j]].ToArray());
                Assert.Equal(
                    expected[j].Select(v => v.HasValue ? 1d : 0),
                    run[owner.Outputs[j + expected.Length]].ToArray()
                );
            }
            using var settings = new HilbertCycleSignalComparison.Settings(kind, 7);
            var pair = HilbertCycleSignalComparison.Pair(kind, 7);
            foreach (var name in HilbertCycleSignalComparison.Names(kind))
            foreach (var native in new[] { false, true })
            foreach (var mask in new[] { false, true })
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
                        Library = HilbertCycleSignalComparison.Pair(kind).Library,
                    },
                    data,
                    20
                )
            );
        }
    }

    private static Bar[] BarsOf(double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
