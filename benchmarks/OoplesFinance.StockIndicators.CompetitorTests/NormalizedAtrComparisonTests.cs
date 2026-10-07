using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class NormalizedAtrComparisonTests
{
    private static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [10, 14, 11, 0, -3],
            [12, 16, 13, 0, -1],
            [8, 12, 9, 0, -5],
            [10, 14, 11, 0, -3]
        );

    [Fact]
    public void NativeLaterZeroOverwritesFirstValueAndDoesNotWriteCurrentSlot()
    {
        var data = Fixture();
        var buffer = Enumerable.Repeat(123456d, data.Count).ToArray();
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Natr<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                buffer,
                out var range,
                2
            )
        );
        Assert.Equal((2, 3), range.GetOffsetAndLength(data.Count));
        Assert.Equal(0, buffer[0]);
        Assert.Equal(123456, buffer[1]);
        var pair = NormalizedAtrComparison.Pair;
        var prefix = data.Take(3);
        Assert.Equal(50, pair.Competitor(prefix, 2).Outputs["Value"].Values[2]);
        Assert.Equal(0, pair.Competitor(data, 2).Outputs["Value"].Values[2]);
        Assert.Equal(50, pair.Ooples(prefix, 2).Outputs["Value"].Values[2]);
        Assert.Equal(50, pair.Ooples(data, 2).Outputs["Value"].Values[2]);
        Assert.Equal(0, pair.Ooples(data, 2).Outputs["Value"].Values[3]);
        Assert.Equal(-662.5 / 3, pair.Ooples(data, 2).Outputs["Value"].Values[4]);
        ComparisonVerifier.Check(pair, data, 2);
    }

    [Fact]
    public void PeriodOneNativeResultIsRawRangeWhileOurResultRemainsNormalized()
    {
        var data = Fixture();
        var pair = NormalizedAtrComparison.Pair;
        Assert.Equal(6, pair.Competitor(data, 1).Outputs["Value"].Values[1]);
        Assert.Equal(600d / 14, pair.Ooples(data, 1).Outputs["Value"].Values[1]);
        ComparisonVerifier.Check(pair, data, 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task IndependentReferenceLifecycleAndOverflowRejection(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(NormalizedSeededAverageTrueRange),
                "normalized ATR",
                () => new NormalizedSeededAverageTrueRange(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task OversizedRangeAndAverageCanProduceFiniteNormalizedOutput()
    {
        var bars = new[]
        {
            new Bar(
                DateTime.UnixEpoch,
                double.MaxValue,
                double.MaxValue,
                double.MaxValue,
                double.MaxValue,
                0
            ),
            new Bar(
                DateTime.UnixEpoch.AddDays(1),
                double.MaxValue,
                double.MaxValue,
                -double.MaxValue,
                double.MaxValue,
                0
            ),
        };
        var indicator = new NormalizedSeededAverageTrueRange(1);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 200 }, run[indicator.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task MaximumPeriodIsLazyAndSubnormalInputsKeepTheirRatio()
    {
        var bars = Enumerable
            .Range(0, 3)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                double.Epsilon,
                2 * double.Epsilon,
                0,
                double.Epsilon,
                0
            ))
            .ToArray();
        var tiny = new NormalizedSeededAverageTrueRange(1);
        var large = new NormalizedSeededAverageTrueRange(int.MaxValue);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(tiny, large)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 200, 200 }, run[tiny.Outputs[0]].ToArray());
        Assert.Equal(new[] { 0d, 0, 0 }, run[large.Outputs[0]].ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new NormalizedSeededAverageTrueRange(0));
    }

    [Fact]
    public void SeparateReferencesStillRejectCorruptionOnEitherSide()
    {
        var pair = NormalizedAtrComparison.Pair;
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                result.Outputs["Value"].Values[period] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    Fixture(),
                    2
                )
            );
        }
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, SeededAtrComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
    }
}
