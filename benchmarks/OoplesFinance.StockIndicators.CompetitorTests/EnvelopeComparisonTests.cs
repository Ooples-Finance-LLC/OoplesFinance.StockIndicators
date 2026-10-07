using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class EnvelopeComparisonTests
{
    public static IEnumerable<object[]> Modes =>
        Enum.GetValues<EnvelopeAverage>().Select(x => new object[] { x });

    [Theory, MemberData(nameof(Modes))]
    public async Task EveryModeHasFullOutputsAndLifecycle(EnvelopeAverage average)
    {
        foreach (var period in new[] { 3, 9, 20 })
        foreach (var percent in new[] { double.Epsilon, 2.5, 100, 200 })
            ComparisonVerifier.Check(
                EnvelopeComparison.Create(average, percent),
                EnvelopeComparison.Fixture(),
                period
            );
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowAverageEnvelope),
                average.ToString(),
                () => new WindowAverageEnvelope(3, average: average)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory, MemberData(nameof(Modes))]
    public void QuoteTupleReusableAndSmallestPeriodRoutes(EnvelopeAverage average)
    {
        var period = average is EnvelopeAverage.Alma or EnvelopeAverage.Epma or EnvelopeAverage.Hma
            ? 2
            : 1;
        var type = Enum.Parse<MaType>(average.ToString(), true);
        var data = CompetitorData.FromCloses([1, Math.BitIncrement(1d), 2, 4, 8, 3, 9]);
        ComparisonVerifier.Check(EnvelopeComparison.Create(average), data, period);
        var tuples = data.Closes.Select((x, i) => (DateTime.UnixEpoch.AddDays(i), x)).ToArray();
        var rows = tuples.GetMaEnvelopes(period, movingAverageType: type).ToArray();
        ComparisonVerifier.Compare(
            EnvelopeComparison.Reference(data.Closes, period, 2.5, average, true),
            EnvelopeComparison.FromRows(rows),
            "tuple envelope",
            IndicatorErrorBudget.Exact
        );
        ComparisonVerifier.Compare(
            EnvelopeComparison.FromRows(rows),
            EnvelopeComparison.FromRows(
                tuples.GetSma(1).GetMaEnvelopes(period, movingAverageType: type)
            ),
            "reusable envelope",
            IndicatorErrorBudget.Exact
        );
        Assert.Equal(tuples.Select(t => t.Item1), rows.Select(r => r.Date));
        if (period == 2)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WindowAverageEnvelope(1, average: average)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                data.Quotes.GetMaEnvelopes(1, movingAverageType: type).ToArray()
            );
        }
    }

    [Fact]
    public void NegativeCentersAreNotSortedAndNearHundredOffsetsRetainResiduals()
    {
        var negative = EnvelopeComparison
            .Owned(BarsFor([-10, -10]), 1, 2.5, EnvelopeAverage.Sma)
            .Outputs;
        Assert.Equal(new[] { -10.25, -10.25 }, negative["UpperEnvelope"].Values);
        Assert.Equal(new[] { -9.75, -9.75 }, negative["LowerEnvelope"].Values);
        foreach (var percent in new[] { Math.BitDecrement(100d), 100d, Math.BitIncrement(100d) })
        {
            var data = CompetitorData.FromCloses([1, -1, 2, -2]);
            ComparisonVerifier.Check(
                EnvelopeComparison.Create(EnvelopeAverage.Sma, percent),
                data,
                1
            );
            var lower = EnvelopeComparison
                .Owned(data.IndicatorBars, 1, percent, EnvelopeAverage.Sma)
                .Outputs["LowerEnvelope"]
                .Values;
            Assert.Equal(Math.Sign(100 - percent), Math.Sign(lower[0]));
            Assert.Equal(-Math.Sign(100 - percent), Math.Sign(lower[1]));
        }
    }

    [Theory, MemberData(nameof(Modes))]
    public void TinyWideAndLazyMaximumPeriods(EnvelopeAverage average)
    {
        foreach (
            var prices in new[]
            {
                new[] { double.Epsilon, 0, -double.Epsilon, 2 * double.Epsilon, 0, double.Epsilon },
                Enumerable.Repeat(double.MaxValue / 8, 6).ToArray(),
            }
        )
        foreach (var period in new[] { 3, int.MaxValue })
            ComparisonVerifier.Compare(
                EnvelopeComparison.Reference(prices, period, 2.5, average, false),
                EnvelopeComparison.Owned(BarsFor(prices), period, 2.5, average),
                "wide/lazy envelopes",
                IndicatorErrorBudget.Exact
            );
        var empty = EnvelopeComparison.Owned([], 3, 2.5, average);
        Assert.All(empty.Outputs.Values, output => Assert.Empty(output.Values));
    }

    [Fact]
    public void ExtremePercentageHasFiniteZeroAndTinyResultsButTrueOverflowIsRejected()
    {
        foreach (var prices in new[] { new[] { 0d, 0 }, new[] { double.Epsilon, double.Epsilon } })
            ComparisonVerifier.Compare(
                EnvelopeComparison.Reference(
                    prices,
                    1,
                    double.MaxValue,
                    EnvelopeAverage.Sma,
                    false
                ),
                EnvelopeComparison.Owned(BarsFor(prices), 1, double.MaxValue, EnvelopeAverage.Sma),
                "huge percentage",
                IndicatorErrorBudget.Exact
            );
        Assert.Throws<IndicatorOutputException>(() =>
            EnvelopeComparison.Owned(BarsFor([double.MaxValue]), 1, 100, EnvelopeAverage.Sma)
        );
        foreach (var percent in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new WindowAverageEnvelope(3, percent));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowAverageEnvelope(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowAverageEnvelope(3, average: (EnvelopeAverage)99)
        );
        var data = EnvelopeComparison.Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetMaEnvelopes(3, 0).ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetMaEnvelopes(3, movingAverageType: (MaType)99).ToArray()
        );
        var native = data.Quotes.GetMaEnvelopes(3, double.NaN).ToArray();
        Assert.True(native[2].UpperEnvelope.HasValue);
        Assert.True(double.IsNaN(native[2].UpperEnvelope!.Value));
        Assert.True(double.IsNaN(native[2].LowerEnvelope!.Value));
        Assert.True(double.IsFinite(native[2].Centerline!.Value));
    }

    [Fact]
    public async Task ChainingFeedsTheAverageState()
    {
        var indicator = new WindowAverageEnvelope(1, 50);
        indicator.Of(new Sma(2));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(BarsFor([1, 3, 5, 7])))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 2, 4, 6 }, run[indicator.Centerline].ToArray());
        Assert.Equal(new[] { 0d, 3, 6, 9 }, run[indicator.UpperEnvelope].ToArray());
        Assert.Equal(new[] { 0d, 1, 2, 3 }, run[indicator.LowerEnvelope].ToArray());
    }

    [Fact]
    public void EveryOutputAndPresenceRejectsCorruption()
    {
        foreach (var mode in Enum.GetValues<EnvelopeAverage>())
        foreach (var name in EnvelopeComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            var pair = EnvelopeComparison.Create(mode);
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (presence)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    EnvelopeComparison.Fixture(),
                    3
                )
            );
        }
        var wrong = EnvelopeComparison.Create(EnvelopeAverage.Hma) with
        {
            Library = EnvelopeComparison.Create(EnvelopeAverage.Sma).Library,
        };
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(wrong, EnvelopeComparison.Fixture(), 3)
        );
    }

    private static Bar[] BarsFor(double[] prices) =>
        prices.Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1)).ToArray();
}
