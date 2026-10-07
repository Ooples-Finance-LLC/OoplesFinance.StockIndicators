using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DispersionComparisonTests
{
    [Theory]
    [InlineData(WindowDispersionOutput.Variance, false)]
    [InlineData(WindowDispersionOutput.Variance, true)]
    [InlineData(WindowDispersionOutput.StandardDeviation, false)]
    [InlineData(WindowDispersionOutput.StandardDeviation, true)]
    [InlineData(WindowDispersionOutput.ZScore, false)]
    [InlineData(WindowDispersionOutput.ZScore, true)]
    public async Task CenteredRationalReferenceVerifiesLifecycleAndExtremes(
        WindowDispersionOutput output,
        bool sample
    )
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WindowDispersion),
                    "sample/population dispersion",
                    () => new WindowDispersion(period, output, sample)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    private static async Task<double[]> Run(WindowDispersion indicator, params double[] values)
    {
        var bars = values
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }

    [Fact]
    public async Task SampleAndPopulationUseCurrentWindowMeanAndExpireOldValues()
    {
        Assert.Equal(
            new[] { 0d, 1, 14d / 9, 14d / 9 },
            await Run(new(3, WindowDispersionOutput.Variance), 1, 3, 4, 6)
        );
        Assert.Equal(
            new[] { 0d, 2, 7d / 3, 7d / 3 },
            await Run(new(3, WindowDispersionOutput.Variance, true), 1, 3, 4, 6)
        );
        Assert.Equal(
            new[] { 0d, 1, 1, 1 },
            await Run(new(2, WindowDispersionOutput.ZScore), 1, 3, 4, 6)
        );
        var z = await Run(new(int.MaxValue, WindowDispersionOutput.ZScore, true), 1, 2, 3);
        Assert.Equal(new[] { 0d, Math.Sqrt(.5), 1 }, z);
    }

    [Fact]
    public async Task SubnormalVarianceAndDeviationAreNeverRoundedBeforeNormalizationOrScaling()
    {
        Assert.Equal(
            new[] { 0d, Math.Sqrt(.5) },
            await Run(new(2, WindowDispersionOutput.ZScore, true), 0, double.Epsilon)
        );
        Assert.Equal(
            new[] { 0d, double.Epsilon },
            await Run(new(2, WindowDispersionOutput.StandardDeviation, false, 2), 0, double.Epsilon)
        );
        Assert.Equal(
            new[] { 0d, double.Epsilon },
            await Run(new(2, WindowDispersionOutput.StandardDeviation, true, 2), 0, double.Epsilon)
        );
        var native = new QuanTAlib.Zscore(2);
        native.Calc(new QuanTAlib.TValue(DateTime.UnixEpoch, 0, true));
        Assert.Equal(
            0,
            native
                .Calc(new QuanTAlib.TValue(DateTime.UnixEpoch.AddDays(1), double.Epsilon, true))
                .Value
        );
    }

    [Fact]
    public async Task FiniteStatisticsSurviveOversizedVarianceAndExactScaling()
    {
        Assert.Equal(
            new[] { 0d, double.MaxValue },
            await Run(new(2), -double.MaxValue, double.MaxValue)
        );
        Assert.Equal(
            new[] { 0d, Math.Sqrt(.5) },
            await Run(
                new(2, WindowDispersionOutput.ZScore, true),
                -double.MaxValue,
                double.MaxValue
            )
        );
        var big = Math.ScaleB(1, 800);
        Assert.Equal(
            new[] { 0d, big },
            await Run(
                new(2, WindowDispersionOutput.Variance, false, Math.ScaleB(1, -800)),
                -big,
                big
            )
        );
        Assert.Equal(
            new[] { 0d, 0 },
            await Run(
                new(2, WindowDispersionOutput.Variance, false, 0),
                -double.MaxValue,
                double.MaxValue
            )
        );
        Assert.Equal(
            new[] { 0d, -double.MaxValue },
            await Run(
                new(2, WindowDispersionOutput.StandardDeviation, false, -1),
                -double.MaxValue,
                double.MaxValue
            )
        );
    }

    [Fact]
    public void IndependentComparisonsCoverEveryConfigurationAndBoundary()
    {
        foreach (var pair in DispersionComparison.Pairs)
        foreach (var period in new[] { 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, DispersionComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
        foreach (var sample in new[] { false, true })
        foreach (var id in new[] { "QuanTAlib.Variance", "QuanTAlib.Stddev" })
            ComparisonVerifier.Check(
                DispersionComparison.Create(id, sample),
                DispersionComparison.Fixture(),
                3
            );
        foreach (var multiplier in new[] { -2d, 0, .5, 2 })
            ComparisonVerifier.Check(
                DispersionComparison.Create("TaLib.Functions.StdDev", false, multiplier),
                DispersionComparison.Fixture(),
                3
            );
        foreach (var id in new[] { "TaLib.Functions.Var", "Trady.Indicator.StandardDeviation" })
            ComparisonVerifier.Check(
                DispersionComparison.Create(id),
                DispersionComparison.Fixture(),
                1
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Variance(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Stddev(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Zscore(1));
        var data = DispersionComparison.Fixture();
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            DispersionComparison.TaLib(
                "TaLib.Functions.StdDev",
                data,
                new double[data.Count],
                1,
                1,
                out _
            )
        );
        Assert.Equal(
            TALib.Core.RetCode.OutOfRangeParam,
            DispersionComparison.TaLib(
                "TaLib.Functions.Var",
                CompetitorData.FromCloses([5]),
                new double[1],
                1,
                1,
                out _
            )
        );
    }

    [Fact]
    public void NativeUncenteredMomentCancellationIsExplicitAndDoesNotWeakenOwnedAssertions()
    {
        var data = DispersionComparison.CancellationFixture();
        foreach (var id in new[] { "TaLib.Functions.Var", "TaLib.Functions.StdDev" })
        {
            var pair = DispersionComparison.Create(id);
            ComparisonVerifier.Check(pair, data, 3);
            var ours = pair.Ooples(data, 3).Outputs["Value"].Values[2];
            var theirs = pair.Competitor(data, 3).Outputs["Value"].Values[2];
            Assert.Equal(
                id.EndsWith(".Var", StringComparison.Ordinal) ? 2d / 3 : Math.Sqrt(2d / 3),
                ours
            );
            Assert.NotEqual(ours, theirs);
        }
    }

    [Fact]
    public void TradyObjectGenericAndTupleRoutesAgreeForFiniteInput()
    {
        var data = DispersionComparison.Fixture();
        var closes = data.Closes.Select(v => (decimal)v).ToArray();
        var objectValues = new Trady.Analysis.Indicator.StandardDeviation(data.Candles, 3)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        var generic = new Trady.Analysis.Indicator.StandardDeviation<decimal, decimal?>(
            closes,
            v => v,
            3
        )
            .Compute()
            .ToArray();
        var tuple = new Trady.Analysis.Indicator.StandardDeviationByTuple(closes, 3)
            .Compute()
            .ToArray();
        Assert.Equal(objectValues, generic);
        Assert.Equal(objectValues, tuple);
    }

    [Fact]
    public void EachArmRejectsCorruptionIncludingNativeCancellationCases()
    {
        foreach (var pair in DispersionComparison.Pairs)
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
                    DispersionComparison.Fixture(),
                    3
                )
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowDispersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowDispersion(2, (WindowDispersionOutput)99)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowDispersion(multiplier: double.NaN)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowDispersion(multiplier: double.PositiveInfinity)
        );
    }
}
