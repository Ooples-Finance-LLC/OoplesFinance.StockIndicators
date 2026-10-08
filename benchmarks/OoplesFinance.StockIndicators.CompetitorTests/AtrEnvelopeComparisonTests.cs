using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AtrEnvelopeComparisonTests
{
    [Theory]
    [InlineData(0, 3, 2, 2)]
    [InlineData(0, 2, 4, .5)]
    [InlineData(1, 2, 4, 2)]
    [InlineData(2, 1, 1, -1)]
    [InlineData(2, 3, 2, 0)]
    [InlineData(0, int.MaxValue, 2, 2)]
    [InlineData(1, 2, int.MaxValue, 2)]
    public async Task IndependentLifecycleContracts(int variant, int cp, int ap, double multiplier)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowAtrBands),
                "ATR envelopes",
                () =>
                    new WindowAtrBands(
                        cp,
                        ap,
                        multiplier,
                        AtrEnvelopeComparison.Mode(variant),
                        variant == 0
                    )
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Bar[] Raw(double[] closes, double[]? highs = null, double[]? lows = null) =>
        closes
            .Select(
                (v, i) =>
                    new Bar(DateTime.UnixEpoch.AddDays(i), v, highs?[i] ?? v, lows?[i] ?? v, v, 0)
            )
            .ToArray();

    [Fact]
    public void CentersSeedsAndIndependentStartupMasks()
    {
        var bars = Raw([1, 4, 1, 4, 1, 4]);
        Assert.Equal(2, AtrEnvelopeComparison.Owned(bars, 3, 2, 2, 0).Outputs["Center"].Values[2]);
        Assert.Equal(
            1.75,
            AtrEnvelopeComparison.Owned(bars, 3, 2, 2, 2).Outputs["Center"].Values[2]
        );
        foreach (var variant in new[] { 0, 1, 2 })
        {
            var result = AtrEnvelopeComparison.Owned(bars, 2, 4, 2, variant).Outputs;
            Assert.Equal(
                variant == 1
                    ? new[] { false, true, true, true, true, true }
                    : new[] { false, false, false, true, true, true },
                result["Center"].Present
            );
            foreach (var name in new[] { "Upper", "Lower" })
                Assert.Equal(
                    new[] { false, false, false, false, true, true },
                    result[name].Present
                );
        }
        var flat = Raw([0, 0, 0], [1, 1, 1], [-1, -1, -1]);
        var bands = AtrEnvelopeComparison.Owned(flat, 2, 2, 2, 0).Outputs;
        Assert.Equal(4, bands["Upper"].Values[2]);
        Assert.Equal(-4, bands["Lower"].Values[2]);
        Assert.False(bands["Width"].Present![2]);
    }

    [Fact]
    public void ExtendedAtrBandDifferenceAndOptionalWidthBoundaries()
    {
        var max = double.MaxValue;
        var bars = Raw([max / 4, max / 4, max / 4], [max, max, max], [-max, -max, -max]);
        var result = AtrEnvelopeComparison.Owned(bars, 2, 2, .375, 0).Outputs;
        Assert.Equal(max, result["Upper"].Values[2]);
        Assert.Equal(-max / 2, result["Lower"].Values[2]);
        Assert.Equal(6, result["Width"].Values[2]);
        var small = Raw([double.Epsilon, double.Epsilon, double.Epsilon], [1, 1, 1], [-1, -1, -1]);
        Assert.Throws<IndicatorOutputException>(() =>
            AtrEnvelopeComparison.Owned(small, 2, 2, 1, 0)
        );
        Assert.True(
            double.IsFinite(
                AtrEnvelopeComparison.Owned(small, 2, 2, 1, 2).Outputs["Upper"].Values[2]
            )
        );
        foreach (
            var fixture in new[]
            {
                bars,
                Raw([double.Epsilon, 2 * double.Epsilon, double.Epsilon]),
                Raw([1, Math.BitIncrement(1), 1]),
            }
        )
        foreach (var variant in new[] { 0, 1, 2 })
        foreach (var periods in new[] { (2, 2), (1, 1), (int.MaxValue, 2), (2, int.MaxValue) })
            ComparisonVerifier.Compare(
                AtrEnvelopeComparison.Series(
                    AtrEnvelopeComparison.Reference(
                        fixture,
                        periods.Item1,
                        periods.Item2,
                        .125,
                        variant
                    ),
                    variant
                ),
                AtrEnvelopeComparison.Owned(fixture, periods.Item1, periods.Item2, .125, variant),
                "ATR envelope extremes",
                IndicatorErrorBudget.Exact
            );
    }

    [Fact]
    public void NativeDefaultsSortingAndParameterBoundaries()
    {
        var data = CompetitorData.Create(50);
        Assert.Equal(
            data.Quotes.GetKeltner(20, 2, 10)
                .Select(r => (r.Centerline, r.UpperBand, r.LowerBand, r.Width)),
            data.Quotes.GetKeltner().Select(r => (r.Centerline, r.UpperBand, r.LowerBand, r.Width))
        );
        Assert.Equal(
            data.Quotes.GetStarcBands(5, 2, 10)
                .Select(r => (r.Centerline, r.UpperBand, r.LowerBand)),
            data.Quotes.GetStarcBands(5).Select(r => (r.Centerline, r.UpperBand, r.LowerBand))
        );
        Assert.Equal(
            data.Quotes.GetKeltner(3, 2, 2).Select(r => (r.Date, r.Centerline, r.Width)),
            data.Quotes.AsEnumerable()
                .Reverse()
                .GetKeltner(3, 2, 2)
                .Select(r => (r.Date, r.Centerline, r.Width))
        );
        Assert.Equal(
            data.Quotes.GetStarcBands(3, 2, 2).Select(r => (r.Date, r.Centerline)),
            data.Quotes.AsEnumerable()
                .Reverse()
                .GetStarcBands(3, 2, 2)
                .Select(r => (r.Date, r.Centerline))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetKeltner(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetKeltner(3, 2, 1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetStarcBands(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetStarcBands(3, 0, 2).ToArray()
        );
        Assert.True(double.IsNaN(data.Quotes.GetKeltner(3, double.NaN, 2).Last().UpperBand!.Value));
        Assert.True(
            double.IsPositiveInfinity(
                data.Quotes.GetStarcBands(3, double.PositiveInfinity, 2).Last().UpperBand!.Value
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowAtrBands(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowAtrBands(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowAtrBands(2, 2, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowAtrBands(2, 2, 2, (AtrBandCenterMode)99)
        );
        foreach (var variant in new[] { 0, 1 })
        foreach (var periods in new[] { (2, 3), (3, 2), (20, 10) })
            ComparisonVerifier.Check(
                AtrEnvelopeComparison.Create(variant, periods.Item2),
                data,
                periods.Item1
            );
    }

    [Fact]
    public void TradyGenericTupleIndexedRangesAndConvertedOutputs()
    {
        var data = CompetitorData.Create(30);
        var input = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        foreach (var periods in new[] { (1, 1), (2, 3), (3, 2) })
        foreach (var multiplier in new[] { -1m, 0m, .5m, 2m })
        {
            var expected = AtrEnvelopeComparison.TradyReference(
                input,
                periods.Item1,
                periods.Item2,
                multiplier
            );
            var tuple = new T.KeltnerChannelsByTuple(
                input,
                periods.Item1,
                multiplier,
                periods.Item2
            );
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(
                expected.Select(v =>
                    ((double?)v.LowerChannel, (double?)v.Middle, (double?)v.UpperChannel)
                ),
                tuple
                    .Compute()
                    .Select(v =>
                        ((double?)v.LowerChannel, (double?)v.Middle, (double?)v.UpperChannel)
                    )
            );
            Assert.Equal(
                expected,
                new T.KeltnerChannels(data.Candles, periods.Item1, multiplier, periods.Item2)
                    .Compute()
                    .Select(r => r.Tick)
            );
            Assert.Equal(
                expected,
                new T.KeltnerChannels<
                    int,
                    (decimal? LowerChannel, decimal? Middle, decimal? UpperChannel)
                >(
                    Enumerable.Range(0, input.Length),
                    i => input[i],
                    periods.Item1,
                    multiplier,
                    periods.Item2
                ).Compute()
            );
            var indexes = new[] { 8, 2, 2, 0, 5 };
            Assert.Equal(
                indexes.Select(i => expected[i]),
                tuple.Compute((IEnumerable<int>)indexes)
            );
            Assert.Equal(expected.Skip(2).Take(6), tuple.Compute(startIndex: 2, endIndex: 7));
            foreach (var i in indexes)
                Assert.Equal(expected[i], tuple[i]);
            ComparisonVerifier.Check(
                AtrEnvelopeComparison.Create(2, periods.Item2, (double)multiplier),
                data,
                periods.Item1
            );
        }
    }

    [Fact]
    public void NativeDecimalOverflowAndQuotePrecisionRemainVisible()
    {
        var huge = Enumerable.Repeat((decimal.MaxValue, -decimal.MaxValue, 0m), 3).ToArray();
        Assert.Throws<OverflowException>(() =>
            new T.KeltnerChannelsByTuple(huge, 1, .25m, 1).Compute().ToArray()
        );
        var tiny = CompetitorData.FromOhlc(
            [double.Epsilon, double.Epsilon, double.Epsilon],
            [2 * double.Epsilon, 2 * double.Epsilon, 2 * double.Epsilon],
            [0, 0, 0],
            [double.Epsilon, double.Epsilon, double.Epsilon]
        );
        Assert.Equal(0, tiny.Quotes.GetStarcBands(2, 2, 2).Last().UpperBand);
        Assert.NotEqual(
            0,
            AtrEnvelopeComparison.Owned(tiny.IndicatorBars, 2, 2, 2, 1).Outputs["Upper"].Values[2]
        );
    }

    [Fact]
    public async Task ChainingPreservesCandleRanges()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (var variant in new[] { 0, 1, 2 })
        {
            var indicator = new WindowAtrBands(
                3,
                2,
                2,
                AtrEnvelopeComparison.Mode(variant),
                variant == 0
            );
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = AtrEnvelopeComparison.Reference(bars, 3, 2, 2, variant);
            for (var slot = 0; slot < AtrEnvelopeComparison.Names(variant).Length; slot++)
            {
                Assert.Equal(
                    expected[slot].Select(v => v ?? 0),
                    run[indicator.Outputs[slot]].ToArray()
                );
                Assert.Equal(
                    expected[slot].Select(v => v.HasValue ? 1d : 0),
                    run[indicator.Outputs[slot + 4]].ToArray()
                );
            }
        }
    }

    [Fact]
    public void EveryValuePresenceAndCenterSeedMutationIsDetected()
    {
        foreach (var pair in AtrEnvelopeComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        var trady = AtrEnvelopeComparison.Create(2);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                trady with
                {
                    Library = (d, p) =>
                        AtrEnvelopeComparison.Series(
                            AtrEnvelopeComparison.Reference(d.IndicatorBars, p, p, 2, 0),
                            2
                        ),
                },
                CompetitorData.FromCloses([1, 4, 1, 4, 1]),
                3
            )
        );
    }
}
