using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AccumulationDistributionComparisonTests
{
    [Fact]
    public void IndependentGridRoundingCoversHalfwaySubnormalAndOverflowCases()
    {
        Assert.Equal(.1, MoneyFlowReferenceArithmetic.Round(1, 10));
        Assert.Equal(
            2 * double.Epsilon,
            MoneyFlowReferenceArithmetic.Round(3, 2 * MoneyFlowReferenceArithmetic.Grid)
        );
        Assert.Equal(
            2 * double.Epsilon,
            MoneyFlowReferenceArithmetic.Round(5, 2 * MoneyFlowReferenceArithmetic.Grid)
        );
        Assert.Equal(
            -2 * double.Epsilon,
            MoneyFlowReferenceArithmetic.Round(-3, 2 * MoneyFlowReferenceArithmetic.Grid)
        );
        Assert.Equal(
            9007199254740992d,
            MoneyFlowReferenceArithmetic.Round((BigInteger.One << 53) + 1, 1)
        );
        Assert.Equal(
            9007199254740996d,
            MoneyFlowReferenceArithmetic.Round((BigInteger.One << 53) + 3, 1)
        );
        Assert.True(
            double.IsPositiveInfinity(
                MoneyFlowReferenceArithmetic.Round(
                    MoneyFlowReferenceArithmetic.Units(double.MaxValue) * 2,
                    MoneyFlowReferenceArithmetic.Grid
                )
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecimalInputCollapseIsExplicitAndNeitherOracleAcceptsCorruption(bool adjusted)
    {
        var next = Math.BitIncrement(100d);
        var data = CompetitorData.FromOhlcv(
            [100, next],
            [101, next],
            [99, 100],
            [100, next],
            [10, 4]
        );
        var pair = adjusted
            ? ComparisonPairs.Get("Trady.Indicator.AccumulationDistributionLine")
            : AccumulationDistributionComparison.Skender(null);
        var name = adjusted ? "Value" : "Adl";
        Assert.Equal(adjusted ? 14 : 4, pair.Ooples(data, 20).Outputs[name].Values[1]);
        Assert.Equal(adjusted ? 10 : 0, pair.Competitor(data, 20).Outputs[name].Values[1]);
        ComparisonVerifier.Check(pair, data, 20);
        static ComparisonSeries Bad(ComparisonSeries source) =>
            new(
                source.Outputs.ToDictionary(
                    kv => kv.Key,
                    kv =>
                        kv.Value with
                        {
                            Values = kv
                                .Value.Values.Select(v => double.IsNaN(v) ? v : v + 1)
                                .ToArray(),
                        }
                )
            );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = (d, p) => Bad(pair.Ooples(d, p)),
                },
                data,
                20
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Competitor = (d, p) => Bad(pair.Competitor(d, p)),
                },
                data,
                20
            )
        );
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public void TimedPricesHaveIdenticalDecimalAndBinaryRepresentations(int count)
    {
        var data = AccumulationDistributionComparison.BenchmarkFixture(count);
        foreach (var values in new[] { data.Opens, data.Highs, data.Lows, data.Closes })
        foreach (var value in values)
        {
            Assert.Equal(value, (double)(decimal)value);
            Assert.Equal((decimal)(value * 1024), (decimal)value * 1024);
        }
    }

    [Fact]
    public void InitialFlowAverageAndPriceAdjustedFlatCandlesHaveDistinctGoldens()
    {
        var data = AccumulationDistributionComparison.Fixture();
        foreach (var pair in AccumulationDistributionComparison.Pairs)
            ComparisonVerifier.Check(pair, data, 20);
        var standard = ComparisonPairs.Get("TaLib.Functions.Ad").Ooples(data, 20).Outputs["Value"];
        Assert.Equal(new[] { 0d, 0, 6, 6, 6, 1 }, standard.Values);
        var details = AccumulationDistributionComparison.Skender(2);
        ComparisonVerifier.Check(details, data, 20);
        var output = details.Ooples(data, 20).Outputs;
        Assert.Equal(new[] { 0d, 0, 1, 0, 0, -1 }, output["MoneyFlowMultiplier"].Values);
        Assert.Equal(new[] { 0d, 0, 6, 0, 0, -5 }, output["MoneyFlowVolume"].Values);
        Assert.Equal(standard.Values, output["Adl"].Values);
        Assert.Equal(new[] { 0d, 3, 6, 6, 3.5 }, output["AdlSma"].Values.Skip(1));
        Assert.Equal(new[] { false, true, true, true, true, true }, output["AdlSma"].Present);
        var adjusted = ComparisonPairs
            .Get("Trady.Indicator.AccumulationDistributionLine")
            .Ooples(data, 20)
            .Outputs["Value"];
        Assert.Equal(new[] { 10d, 14, 20, 18 }, adjusted.Values.Take(4));
        Assert.Equal(new[] { true, true, true, true, false, false }, adjusted.Present);
        Assert.All(adjusted.Values.Skip(4), x => Assert.True(double.IsNaN(x)));
    }

    [Fact]
    public void OptionalAverageConfigurationsAndNativeParameterBoundary()
    {
        var data = AccumulationDistributionComparison.Fixture();
        foreach (int? period in new int?[] { null, 1, 2, 20, int.MaxValue })
            ComparisonVerifier.Check(AccumulationDistributionComparison.Skender(period), data, 20);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAdl(0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AccumulationDistributionWithAverage(0)
        );
    }

    [Fact]
    public void TradyTupleRouteAndZeroVolumeUndefinedRatio()
    {
        foreach (
            var data in new[]
            {
                AccumulationDistributionComparison.Fixture(),
                CompetitorData.FromOhlcv([0, 0, 1], [0, 0, 2], [0, 0, 0], [0, 0, 1], [0, 0, 1]),
            }
        )
        {
            var pair = ComparisonPairs.Get("Trady.Indicator.AccumulationDistributionLine");
            ComparisonVerifier.Check(pair, data, 20);
            var tuples = data.Candles.Select(b => (b.High, b.Low, b.Close, b.Volume));
            var rows = new Trady.Analysis.Indicator.AccumulationDistributionLineByTuple(
                tuples
            ).Compute();
            var expected = pair.Competitor(data, 20).Outputs["Value"];
            Assert.Equal(expected.Present, rows.Select(r => r.HasValue));
            Assert.Equal(expected.Values, rows.Select(r => (double?)r ?? double.NaN));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task DetailedLineIndependentReferenceLifecycleAndLazyAverage(int? period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(AccumulationDistributionWithAverage),
                "detailed flow",
                () => new AccumulationDistributionWithAverage(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        var indicator = new AccumulationDistributionWithAverage(period);
        Assert.Same(indicator.Value, indicator.PrimaryOutput);
    }

    [Fact]
    public async Task AdjustedLineIndependentReferenceAndReset()
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(PriceAdjustedAccumulationDistribution),
                "price adjusted flow",
                () => new PriceAdjustedAccumulationDistribution()
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task ExtendedContributionCancelsToFinitePublishedTotal()
    {
        var indicator = new PriceAdjustedAccumulationDistribution();
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, -1, -1, -1, -1, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddDays(1), 1, 1, 1, 1, double.MaxValue),
        };
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { double.MaxValue, -double.MaxValue }, run[indicator.Value].ToArray());
        Assert.Equal(new[] { 1d, 1d }, run[indicator.IsDefined].ToArray());
    }

    [Fact]
    public async Task WideAndSubnormalPriceRangesPreserveFlow()
    {
        var indicator = new AccumulationDistributionWithAverage(1);
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 2),
            new Bar(
                DateTime.UnixEpoch.AddDays(1),
                0,
                double.Epsilon,
                -double.Epsilon,
                -double.Epsilon,
                1
            ),
        };
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 1d, -1d }, run[indicator.Multiplier].ToArray());
        Assert.Equal(new[] { 2d, -1d }, run[indicator.Flow].ToArray());
        Assert.Equal(new[] { 2d, 1d }, run[indicator.Value].ToArray());
        Assert.Equal(new[] { 2d, 1d }, run[indicator.Average].ToArray());
    }

    [Fact]
    public void TaLibMinimumInputIsPinned()
    {
        double[] input = [1];
        var output = new double[1];
        Assert.Equal(
            TALib.Core.RetCode.OutOfRangeParam,
            Functions.Ad<double>(input, input, input, input, System.Range.All, output, out _)
        );
    }
}
