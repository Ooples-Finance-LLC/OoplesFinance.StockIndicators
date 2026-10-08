using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class VolumePriceComparisonTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    public async Task IndependentRationalContractCoversLifecycleAndExtremes(
        int? period,
        bool typical
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(VolumeWeightedPrice),
                "weighted price",
                () => new VolumeWeightedPrice(period, typical)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    internal static async Task<double[][]> Run(
        IIndicator indicator,
        params (double High, double Low, double Close, double Volume)[] values
    )
    {
        var bars = values
            .Select(
                (v, i) =>
                    new Bar(
                        DateTime.UnixEpoch.AddDays(i),
                        v.Close,
                        v.High,
                        v.Low,
                        v.Close,
                        v.Volume
                    )
            )
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task TypicalPriceIsNotRoundedBeforeWeighting()
    {
        var result = await Run(new VolumeWeightedPrice(), (1, 0, 0, 1), (2, 0, 0, 2));
        Assert.Equal(1d / 3, result[0][0]);
        Assert.Equal(5d / 9, result[0][1]);
        Assert.NotEqual(((1d / 3) + (2d / 3) * 2) / 3, result[0][1]);
        Assert.Equal(new[] { 1d, 1 }, result[1]);
        var closes = await Run(
            new VolumeWeightedPrice(typicalPrice: false),
            (1, 0, 0, 1),
            (2, 0, 0, 2)
        );
        Assert.Equal(new[] { 0d, 0 }, closes[0]);
    }

    [Fact]
    public async Task RollingStartupEvictionAndZeroMassRecoverIndependently()
    {
        var values = new[]
        {
            (1d, 1d, 1d, 0d),
            (3d, 3d, 3d, 0d),
            (5d, 5d, 5d, 2d),
            (7d, 7d, 7d, 0d),
            (9d, 9d, 9d, 0d),
            (11d, 11d, 11d, 3d),
        };
        var rolling = await Run(new VolumeWeightedPrice(2, false), values);
        Assert.Equal(new[] { 0d, 0, 5, 5, 0, 11 }, rolling[0]);
        Assert.Equal(new[] { 0d, 0, 1, 1, 0, 1 }, rolling[1]);
        var cumulative = await Run(new VolumeWeightedPrice(typicalPrice: false), values);
        Assert.Equal(new[] { 0d, 0, 5, 5, 5, 43d / 5 }, cumulative[0]);
        Assert.Equal(new[] { 0d, 0, 1, 1, 1, 1 }, cumulative[1]);
    }

    [Fact]
    public async Task InclusiveAnchorCountsOnlyParticipatingCandles()
    {
        var values = new[]
        {
            (1d, 1d, 1d, 1d),
            (3d, 3d, 3d, 1d),
            (5d, 5d, 5d, 1d),
            (7d, 7d, 7d, 1d),
        };
        var result = await Run(
            new VolumeWeightedPrice(2, false, DateTime.UnixEpoch.AddDays(1)),
            values
        );
        Assert.Equal(new[] { 0d, 0, 4, 6 }, result[0]);
        Assert.Equal(new[] { 0d, 0, 1, 1 }, result[1]);
        var between = await Run(
            new VolumeWeightedPrice(startDate: DateTime.UnixEpoch.AddDays(1).AddTicks(1)),
            values
        );
        Assert.Equal(new[] { 0d, 0, 5, 6 }, between[0]);
        var early = await Run(
            new VolumeWeightedPrice(startDate: DateTime.UnixEpoch.AddDays(-1)),
            values
        );
        Assert.Equal(new[] { 1d, 2, 3, 4 }, early[0]);
        var future = await Run(
            new VolumeWeightedPrice(startDate: DateTime.UnixEpoch.AddDays(10)),
            values
        );
        Assert.All(future, row => Assert.All(row, v => Assert.Equal(0, v)));
    }

    [Fact]
    public async Task ExactProductsNormalizeBeforeRoundingAndMaximumPeriodsAreLazy()
    {
        var maximum = double.MaxValue;
        var result = await Run(
            new VolumeWeightedPrice(),
            (maximum, maximum, maximum, maximum),
            (-maximum, -maximum, -maximum, maximum)
        );
        Assert.Equal(new[] { maximum, 0 }, result[0]);
        Assert.Equal(new[] { 1d, 1 }, result[1]);
        var tiny = await Run(new VolumeWeightedPrice(), (1, 0, 1, double.Epsilon));
        Assert.Equal(2d / 3, tiny[0][0]);
        Assert.Equal(1, tiny[1][0]);
        var signed = await Run(
            new VolumeWeightedPrice(typicalPrice: false),
            (maximum, maximum, maximum, 1),
            (maximum, maximum, maximum, -1),
            (2, 2, 2, 1)
        );
        Assert.Equal(new[] { maximum, 0, 2 }, signed[0]);
        Assert.Equal(new[] { 1d, 0, 1 }, signed[1]);
        var cancellation = await Run(
            new VolumeWeightedPrice(typicalPrice: false),
            (maximum, maximum, maximum, 1),
            (-maximum, -maximum, -maximum, -1),
            (-maximum, -maximum, -maximum, 2)
        );
        Assert.Equal(new[] { maximum, 0, 0 }, cancellation[0]);
        Assert.Equal(new[] { 1d, 0, 1 }, cancellation[1]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(
                new VolumeWeightedPrice(typicalPrice: false),
                (maximum, maximum, maximum, 1),
                (-maximum, -maximum, -maximum, -.5)
            )
        );
        var lazy = await Run(
            new VolumeWeightedPrice(int.MaxValue),
            (maximum, maximum, maximum, maximum),
            (1, 0, 1, 1)
        );
        Assert.All(lazy, row => Assert.All(row, v => Assert.Equal(0, v)));
    }

    [Fact]
    public void NativeZeroMassOverflowAndAnchorBoundariesAreExplicit()
    {
        var zeros = VolumePriceComparison.Fixture(true);
        Assert.Throws<DivideByZeroException>(() =>
            new T.VolumeWeightedAveragePrice(zeros.Candles).Compute()
        );
        Assert.Throws<DivideByZeroException>(() =>
            new T.VolumeWeightedAveragePrice(zeros.Candles, 2).Compute()
        );
        Assert.Null(zeros.Quotes.GetVwap().First().Vwap);
        Assert.Null(zeros.Quotes.GetVwma(1).First().Vwma);
        var large = CompetitorData.FromOhlcv([1e20], [1e20], [1e20], [1e20], [1e20]);
        Assert.Throws<OverflowException>(() =>
            new T.VolumeWeightedAveragePrice(large.Candles).Compute()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            zeros.Quotes.GetVwap(zeros.Dates[0].AddTicks(-1)).ToArray()
        );
        Assert.All(zeros.Quotes.GetVwap(zeros.Dates[^1].AddDays(1)), row => Assert.Null(row.Vwap));
        Assert.Empty(Array.Empty<Quote>().GetVwap(DateTime.MinValue));
        foreach (var period in new[] { 0, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VolumeWeightedPrice(period));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                zeros.Quotes.GetVwma(period).ToArray()
            );
            Assert.Throws<DivideByZeroException>(() =>
                new T.VolumeWeightedAveragePrice(zeros.Candles, period).Compute()
            );
        }
    }

    [Fact]
    public void AllDefinitionsAndNativeRoutesHaveIndependentFullOutputs()
    {
        foreach (var pair in VolumePriceComparison.Pairs)
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, VolumePriceComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, VolumePriceComparison.CollapsedFixture(), period);
            if (!pair.Id.StartsWith("Trady.", StringComparison.Ordinal))
                ComparisonVerifier.Check(pair, VolumePriceComparison.Fixture(true), period);
        }
        var data = VolumePriceComparison.Fixture();
        foreach (
            var anchor in new[]
            {
                data.Dates[2],
                data.Dates[2].AddTicks(1),
                data.Dates[^1].AddDays(1),
            }
        )
            ComparisonVerifier.Check(VolumePriceComparison.Vwap(anchor), data, 20);
        foreach (var period in new int?[] { null, 1, 3, 20 })
        {
            ComparisonVerifier.Check(VolumePriceComparison.Trady(period), data, 20);
            var normal = new T.VolumeWeightedAveragePrice(data.Candles, period)
                .Compute()
                .Select(r => r.Tick)
                .ToArray();
            var tuple = new T.VolumeWeightedAveragePriceByTuple(
                data.Candles.Select(c => (c.High, c.Low, c.Close, c.Volume)),
                period
            )
                .Compute()
                .ToArray();
            Assert.Equal(normal, tuple);
        }
    }

    [Fact]
    public void ValuesAndPresenceRejectCorruptionInBothArmsAndWindowModes()
    {
        foreach (var pair in VolumePriceComparison.Pairs.Concat([VolumePriceComparison.Trady(3)]))
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs["Value"];
                var i = data.Count - 1;
                if (presence)
                    output.Present![i] = !output.Present[i];
                else
                    output.Values[i] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    VolumePriceComparison.Fixture(),
                    3
                )
            );
        }
    }
}
