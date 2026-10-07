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
public sealed class DelayedHilbertTrendComparisonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public async Task RationalContractsCoverStartupAndLifecycle(int suppression)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(DelayedHilbertTrendline),
                "delayed Hilbert trendline " + suppression,
                () => new DelayedHilbertTrendline(suppression)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void NativeFloatDoubleSubrangesAndAliasesMatch()
    {
        foreach (var suppression in new[] { 0, 1, 7, 30 })
        foreach (var start in new[] { 0, 1, 120, 121 })
        {
            using var settings = new DelayedHilbertTrendComparison.Settings(suppression);
            CheckNative<double>(suppression, start);
            CheckNative<float>(suppression, start);
        }
    }

    private static void CheckNative<T>(int suppression, int start)
        where T : IFloatingPointIeee754<T>
    {
        var prices = CompetitorData.Create(220).Closes.Select(T.CreateChecked).ToArray();
        var expected = DelayedHilbertTrendComparison.NativePacked(prices, suppression, start, 219);
        foreach (var alias in new[] { false, true })
        {
            var input = prices.ToArray();
            var output = alias ? input : new T[220];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.HtTrendline<T>(input, start..219, output, out var range)
            );
            Assert.Equal(Math.Max(start, 63 + suppression), range.Start.Value);
            Assert.Equal(expected, output.Take(range.End.Value - range.Start.Value));
        }
    }

    [Fact]
    public void PublishedMeansStayFiniteAcrossExtremeAndSubnormalInputs()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        foreach (var shape in new[] { 0, 1, 2 })
        {
            var prices = Enumerable
                .Range(0, 180)
                .Select(i =>
                    shape == 0 ? scale
                    : shape == 1 ? (i % 2 == 0 ? scale : -scale)
                    : scale * Math.Sin(i * .4)
                )
                .ToArray();
            var expected = DelayedHilbertTrendComparison.Reference(prices, 0);
            Assert.All(
                expected.Where(v => v.HasValue),
                v => Assert.True(double.IsFinite(v!.Value))
            );
            ComparisonVerifier.Compare(
                DelayedHilbertTrendComparison.Series(expected),
                DelayedHilbertTrendComparison.Owned(BarsOf(prices), 0),
                "wide trendline",
                IndicatorErrorBudget.Exact
            );
            if (shape == 0)
                Assert.All(expected.Skip(63), v => Assert.Equal(scale, v));
        }
    }

    [Fact]
    public void NativeOverflowControlsAndSubnormalValuesRemainExplicit()
    {
        using var settings = new DelayedHilbertTrendComparison.Settings(0);
        foreach (var scale in new[] { double.MaxValue, double.Epsilon })
        {
            var prices = Enumerable.Repeat(scale, 100).ToArray();
            var expected = DelayedHilbertTrendComparison.NativePacked(prices, 0, 0, 99);
            var output = new double[100];
            Assert.Equal(
                TaCore.RetCode.Success,
                Functions.HtTrendline<double>(prices, System.Range.All, output, out var range)
            );
            Assert.Equal(expected, output.Take(range.End.Value - range.Start.Value));
            if (scale > 1)
                Assert.All(expected, v => Assert.Equal(0, v));
        }
    }

    [Fact]
    public void BoundariesAndSuppressionPreserveLazyFixedStorage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedHilbertTrendline(-1));
        foreach (var suppression in new[] { 0, 7, int.MaxValue })
        {
            var owner = new DelayedHilbertTrendline(suppression);
            Assert.Equal((int)Math.Min(int.MaxValue, 63L + suppression), owner.WarmupBars);
            var output = DelayedHilbertTrendComparison
                .Owned(ComparisonVerifier.Fixture("zero", 100).CloseBars, suppression)
                .Outputs["Trendline"];
            Assert.Equal(
                Enumerable.Range(0, 100).Select(i => i >= 63L + suppression),
                output.Present!
            );
            Assert.All(output.Values.Where(double.IsFinite), v => Assert.Equal(0, v));
        }
        var buffer = new double[100];
        using var settings = new DelayedHilbertTrendComparison.Settings(0);
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.HtTrendline<double>([1d], System.Range.All, buffer, out _)
        );
        Assert.Equal(
            TaCore.RetCode.Success,
            Functions.HtTrendline<double>([1d, 2, 3], System.Range.All, buffer, out var empty)
        );
        Assert.Equal(0, empty.End.Value);
        Assert.Equal(
            TaCore.RetCode.OutOfRangeParam,
            Functions.HtTrendline<double>([1d, 2, 3], 4..5, buffer, out _)
        );
        using var oversized = new DelayedHilbertTrendComparison.Settings(int.MaxValue);
        Assert.Equal(unchecked(int.MaxValue + 63), Functions.HtTrendlineLookback());
    }

    [Fact]
    public async Task ChainingAndValuePresenceAndSuppressionMutationsAreDetected()
    {
        var data = CompetitorData.Create(160);
        var owner = new DelayedHilbertTrendline(7);
        owner.Of(new FixedPeriodWma(3));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(owner)
            .BuildAsync();
        var expected = DelayedHilbertTrendComparison.Reference(
            FixedWeightedComparison.Stage(data.Closes, 3, false),
            7
        );
        Assert.Equal(expected.Select(v => v ?? 0), run[owner.Trendline].ToArray());
        Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0), run[owner.IsDefined].ToArray());
        using var settings = new DelayedHilbertTrendComparison.Settings(7);
        var pair = DelayedHilbertTrendComparison.Pair(7);
        foreach (var native in new[] { false, true })
        foreach (var mask in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (mask)
                    result.Outputs["Trendline"].Present![^1] = false;
                else
                    result.Outputs["Trendline"].Values[^1] += 1;
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
                    Library = DelayedHilbertTrendComparison.Pair().Library,
                },
                data,
                20
            )
        );
        var early = new DelayedHilbertTrendline(0);
        var late = new DelayedHilbertTrendline(7);
        using var both = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(early, late)
            .BuildAsync();
        Assert.Equal(
            both[early.Trendline].ToArray().Skip(70),
            both[late.Trendline].ToArray().Skip(70)
        );
    }

    private static Bar[] BarsOf(double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();
}
